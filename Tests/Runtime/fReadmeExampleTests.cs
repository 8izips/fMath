using NUnit.Framework;

namespace fMath.Tests
{
    /// <summary>Keeps the README usage example compiling and behaving as described.</summary>
    [TestFixture]
    public class fReadmeExampleTests
    {
        [Test]
        public void UsageExample()
        {
            ffloat speed = ffloat.FromFraction(35, 10);            // 3.5
            ffloat dt = ffloat.FromFraction(1, 60);
            fVector3 pos = fVector3.FromInt(10, 0, -4);
            fVector3 enemyPos = fVector3.FromInt(12, 0, -3);
            fAngle yaw = fAngle.FromDegrees(90);

            // 移動: 方向(Q30) × 速度 × dt
            fUnitVector2 dir2 = fUnitVector2.FromAngle(yaw);
            pos += new fVector3(dir2.x * speed, ffloat.Zero, dir2.y * speed) * dt;

            // 範囲判定: Sqrtなし（64bit二乗距離の比較）
            bool hit = fVector3.IsWithinDistance(pos, enemyPos, ffloat.FromInt(3));

            // 角度判定: acosなし（dot >= cos(limit)）
            fQuaternion q = fQuaternion.AngleAxis(yaw, fUnitVector3.up);
            fUnitVector3 forward = q * fUnitVector3.forward;
            if (hit && fVector3.TryNormalize(enemyPos - pos, out fUnitVector3 toEnemy))
            {
                hit = fUnitVector3.WithinAngle(forward, toEnemy, fAngle.FromDegrees(45));

                // 回転: 敵を向く回転へ1/4ずつ近づける
                fQuaternion look = fQuaternion.LookRotation(toEnemy, fUnitVector3.up);
                q = fQuaternion.Slerp(q, look, ffloat.FromFraction(1, 4));
            }
            fVector3 rotated = q * fVector3.FromInt(1, 0, 0);

            Assert.That(pos.z.ToDouble(), Is.EqualTo(-4 + 3.5 / 60).Within(1e-4));
            Assert.That(q.IsNormalized, Is.True);
            Assert.That(rotated.magnitude.RawValue, Is.EqualTo(ffloat.One.RawValue).Within(2));
        }
    }
}
