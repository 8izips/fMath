using System;

namespace fMath.Diagnostics
{
    /// <summary>Per-agent input for one tick (what a rollback netcode would exchange).</summary>
    public struct fAgentInput
    {
        public sbyte MoveX;   // -1, 0, 1 (strafe)
        public sbyte MoveZ;   // -1, 0, 1 (forward / back)
        public bool Attack;
        public bool Jump;

        public static fAgentInput FromBits(uint bits)
        {
            return new fAgentInput
            {
                MoveX = (sbyte)((int)(bits % 3) - 1),
                MoveZ = (sbyte)((int)((bits / 3) % 3) - 1),
                Attack = (bits & 0x100) != 0 && (bits & 0x200) != 0,
                Jump = (bits & 0xF000) == 0x1000,
            };
        }
    }

    /// <summary>Simulation state of one agent: raw fixed-point values only (snapshot friendly).</summary>
    public struct fAgentState
    {
        public fVector3 Position;
        public fVector3 Velocity;
        public fAngle Facing;
        public fQuaternion Rotation;
        public int Health;
        public int AttackCooldown;
        public int HitStun;
        public bool Grounded;
    }

    /// <summary>
    /// Small 8-agent deterministic 3D action simulation used for determinism, rollback and game-like
    /// benchmarks: input application, movement integration, facing (fAngle + quaternion), ground and
    /// wall planes, agent separation, attack range/facing dot tests and knockback.
    /// All state lives in a preallocated array; Tick, SaveSnapshot and LoadSnapshot allocate nothing.
    /// </summary>
    public sealed class fDeterministicAgentSimulation
    {
        public const int AgentCount = 8;

        // tuning (Q16.16 raw / Angle32, fixed at authoring time, no float anywhere)
        static readonly ffloat Dt = ffloat.FromFraction(1, 60);
        static readonly ffloat MoveAcceleration = ffloat.FromInt(40);
        static readonly ffloat MaxSpeed = ffloat.FromInt(7);
        static readonly ffloat GroundFriction = ffloat.FromFraction(85, 100);
        static readonly ffloat Gravity = ffloat.FromInt(-25);
        static readonly ffloat JumpSpeed = ffloat.FromInt(9);
        static readonly ffloat AgentRadius = ffloat.FromFraction(1, 2);
        static readonly ffloat AttackRange = ffloat.FromInt(3);
        static readonly fAngle AttackHalfAngle = fAngle.FromDegrees(45);
        static readonly ffloat KnockbackSpeed = ffloat.FromInt(12);
        static readonly ffloat KnockbackLift = ffloat.FromInt(5);
        static readonly fAngleDelta TurnRatePerTick = fAngleDelta.FromDegrees(12);
        static readonly ffloat RotationSmoothing = ffloat.FromFraction(1, 4);
        static readonly ffloat ArenaHalfSize = ffloat.FromInt(60);

        struct Plane
        {
            public fUnitVector3 Normal;
            public ffloat Offset; // plane: dot(n, p) = offset
        }

        readonly Plane[] _walls;
        fAgentState[] _agents = new fAgentState[AgentCount];
        int _tick;

        public int CurrentTick => _tick;

        public fDeterministicAgentSimulation()
        {
            _walls = new Plane[5];
            _walls[0] = new Plane { Normal = fUnitVector3.right, Offset = -ArenaHalfSize };
            _walls[1] = new Plane { Normal = fUnitVector3.left, Offset = -ArenaHalfSize };
            _walls[2] = new Plane { Normal = fUnitVector3.forward, Offset = -ArenaHalfSize };
            _walls[3] = new Plane { Normal = fUnitVector3.back, Offset = -ArenaHalfSize };
            // a slanted wall cutting one corner
            fVector3.TryNormalize(fVector3.FromInt(-1, 0, -1), out fUnitVector3 diagonal);
            _walls[4] = new Plane { Normal = diagonal, Offset = -ffloat.FromInt(70) };
            Reset();
        }

        public void Reset()
        {
            _tick = 0;
            for (int i = 0; i < AgentCount; i++)
            {
                fAngle around = fAngle.FromTurnsFraction(i, AgentCount);
                fUnitVector2 ring = fUnitVector2.FromAngle(around);
                ffloat radius = ffloat.FromInt(20);
                _agents[i] = new fAgentState
                {
                    Position = new fVector3(ring.x * radius, ffloat.Zero, ring.y * radius),
                    Velocity = fVector3.zero,
                    Facing = around + fAngle.HalfTurn,
                    Rotation = fQuaternion.identity,
                    Health = 1000,
                    Grounded = true,
                };
            }
        }

        public fAgentState GetAgent(int index) => _agents[index];

        /// <summary>Deterministic pseudo-input for (tick, agent) from a seed; integer-only.</summary>
        public static fAgentInput ScriptedInput(ulong seed, int tick, int agent)
        {
            ulong z = seed + (ulong)(tick / 6) * 0x9E3779B97F4A7C15UL + (ulong)agent * 0xD1B54A32D192ED03UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            fAgentInput input = fAgentInput.FromBits((uint)z);
            // attacks and jumps are edge-triggered on the exact tick, not held for the 6-tick window
            ulong e = z ^ ((ulong)tick * 0xA24BAED4963EE407UL);
            e = (e ^ (e >> 29)) * 0xBF58476D1CE4E5B9UL;
            input.Attack = (e & 0x1F) == 0;
            input.Jump = (e & 0x3F0) == 0x100;
            return input;
        }

        public void Tick(fAgentInput[] inputs)
        {
            for (int i = 0; i < AgentCount; i++)
                ApplyInputAndIntegrate(ref _agents[i], inputs[i]);

            ResolveSeparation();

            for (int i = 0; i < AgentCount; i++)
                if (inputs[i].Attack && _agents[i].AttackCooldown == 0 && _agents[i].HitStun == 0)
                    PerformAttack(i);

            _tick++;
        }

        void ApplyInputAndIntegrate(ref fAgentState a, fAgentInput input)
        {
            if (a.AttackCooldown > 0) a.AttackCooldown--;

            if (a.HitStun > 0)
            {
                a.HitStun--;
            }
            else if (input.MoveX != 0 || input.MoveZ != 0)
            {
                // local move direction (x strafe, z forward) rotated by facing in the XZ plane
                fVector2 local = fVector2.FromInt(input.MoveX, input.MoveZ);
                fVector2.TryNormalize(local, out fUnitVector2 localDir);
                fAngle worldAngle = a.Facing + (localDir.ToAngle() - fAngle.QuarterTurn);
                fUnitVector2 world = fUnitVector2.FromAngle(worldAngle);
                fVector3 accel = new fVector3(world.x * MoveAcceleration, ffloat.Zero, world.y * MoveAcceleration);
                a.Velocity += accel * Dt;

                fVector2 planar = new fVector2(a.Velocity.x, a.Velocity.z);
                planar = fVector2.ClampMagnitude(planar, MaxSpeed);
                a.Velocity.x = planar.x;
                a.Velocity.z = planar.y;

                // turn towards the movement direction when moving forward
                if (input.MoveZ > 0)
                    a.Facing = fAngle.MoveTowards(a.Facing, worldAngle, TurnRatePerTick);
            }
            else if (a.Grounded)
            {
                a.Velocity.x *= GroundFriction;
                a.Velocity.z *= GroundFriction;
            }

            if (input.Jump && a.Grounded && a.HitStun == 0)
            {
                a.Velocity.y = JumpSpeed;
                a.Grounded = false;
            }

            a.Velocity.y += Gravity * Dt;
            a.Position += a.Velocity * Dt;

            // ground plane y = 0
            if (a.Position.y <= ffloat.Zero)
            {
                a.Position.y = ffloat.Zero;
                if (a.Velocity.y < ffloat.Zero) a.Velocity.y = ffloat.Zero;
                a.Grounded = true;
            }

            // walls: push out along the normal and remove the velocity component into the wall
            for (int w = 0; w < _walls.Length; w++)
            {
                Plane plane = _walls[w];
                ffloat distance = fVector3.Dot(a.Position, plane.Normal) - plane.Offset;
                if (distance < AgentRadius)
                {
                    a.Position += plane.Normal * (AgentRadius - distance);
                    if (fVector3.Dot(a.Velocity, plane.Normal) < ffloat.Zero)
                        a.Velocity = fVector3.ProjectOnPlane(a.Velocity, plane.Normal);
                }
            }

            // facing quaternion: yaw target plus a lean proportional to planar speed, smoothed by slerp
            fQuaternion yaw = fQuaternion.AngleAxis(a.Facing, fUnitVector3.up);
            fAngle lean = fAngle.FromRaw((uint)Math.Min(0x04000000L, (long)a.Velocity.x.RawValue * a.Velocity.x.RawValue + (long)a.Velocity.z.RawValue * a.Velocity.z.RawValue >> 26));
            fQuaternion target = yaw * fQuaternion.AngleAxis(lean, fUnitVector3.right);
            a.Rotation = fQuaternion.Slerp(a.Rotation, target, RotationSmoothing);
        }

        void ResolveSeparation()
        {
            ffloat minDistance = AgentRadius * 2;
            for (int i = 0; i < AgentCount; i++)
            {
                for (int j = i + 1; j < AgentCount; j++)
                {
                    fVector3 pi = _agents[i].Position, pj = _agents[j].Position;
                    if (!fVector3.IsWithinDistance(pi, pj, minDistance))
                        continue;
                    fVector3 diff = pj - pi;
                    diff.y = ffloat.Zero;
                    fUnitVector3 dir = diff.NormalizedOr(fUnitVector3.FromRawUnchecked(fTrig.Cos(fAngle.FromTurnsFraction(i * 3 + j, 17)).RawValue, 0, fTrig.Sin(fAngle.FromTurnsFraction(i * 3 + j, 17)).RawValue));
                    ffloat overlap = (minDistance - fVector3.Distance(pi, pj)) * ffloat.Half;
                    fVector3 push = dir * overlap;
                    _agents[i].Position -= push;
                    _agents[j].Position += push;
                }
            }
        }

        void PerformAttack(int attacker)
        {
            ref fAgentState a = ref _agents[attacker];
            a.AttackCooldown = 20;
            fUnitVector2 facing2 = fUnitVector2.FromAngle(a.Facing);
            fUnitVector3 forward = fUnitVector3.FromRawUnchecked(facing2.x.RawValue, 0, facing2.y.RawValue);

            for (int i = 0; i < AgentCount; i++)
            {
                if (i == attacker)
                    continue;
                ref fAgentState target = ref _agents[i];
                if (!fVector3.IsWithinDistance(a.Position, target.Position, AttackRange))
                    continue;
                fVector3 toTarget = target.Position - a.Position;
                toTarget.y = ffloat.Zero;
                if (!toTarget.TryNormalize(out fUnitVector3 dir))
                    dir = forward;
                if (!fUnitVector3.WithinAngle(forward, dir, AttackHalfAngle))
                    continue;

                target.Health -= 10;
                target.HitStun = 12;
                target.Grounded = false;
                target.Velocity = dir * KnockbackSpeed + fVector3.up * KnockbackLift;
                target.Facing = fTrig.Atan2(-dir.z.ToFfloat(), -dir.x.ToFfloat());
            }
        }

        #region Snapshot / hash
        /// <summary>Snapshot buffer: raw state of every agent plus the tick counter.</summary>
        public sealed class Snapshot
        {
            internal readonly fAgentState[] Agents = new fAgentState[AgentCount];
            internal int Tick;
        }

        public void SaveSnapshot(Snapshot snapshot)
        {
            Array.Copy(_agents, snapshot.Agents, AgentCount);
            snapshot.Tick = _tick;
        }

        public void LoadSnapshot(Snapshot snapshot)
        {
            Array.Copy(snapshot.Agents, _agents, AgentCount);
            _tick = snapshot.Tick;
        }

        public ulong ComputeWorldHash()
        {
            fRawHash h = fRawHash.Create();
            h.Add(_tick);
            for (int i = 0; i < AgentCount; i++)
            {
                ref fAgentState a = ref _agents[i];
                h.Add(a.Position); h.Add(a.Velocity); h.Add(a.Facing); h.Add(a.Rotation);
                h.Add(a.Health); h.Add(a.AttackCooldown); h.Add(a.HitStun); h.Add(a.Grounded);
            }
            return h.Value;
        }
        #endregion
    }
}
