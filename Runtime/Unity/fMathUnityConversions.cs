using System;
using UnityEngine;

namespace fMath.Unity
{
    /// <summary>
    /// Boundary between Unity floats and fMath fixed-point values.
    ///
    /// float -> fixed is for authoring data and input sampling only (done once, before values enter
    /// the simulation); fixed -> float is for presentation only (rendering, UI, audio). Never feed a
    /// converted float back into the deterministic simulation.
    ///
    /// float -> fixed rounds to nearest with ties to even using double arithmetic, which is exact and
    /// identical on every IEEE-754 platform for these operations (float -> double widening, scaling by
    /// a power of two, Math.Round). NaN converts to zero and out-of-range values saturate.
    /// </summary>
    public static class fMathUnityConversions
    {
        const double Q16 = 65536.0;
        const double Q30 = 1073741824.0;
        const double TURN32 = 4294967296.0;

        #region Scalars
        public static ffloat ToFixed(this float value) => ffloat.FromRaw(ToRaw(value, Q16));

        public static ffloat ToFixed(this double value) => ffloat.FromRaw(ToRaw(value, Q16));

        public static float ToFloat(this ffloat value) => (float)(value.RawValue / Q16);

        public static funit ToUnit(this float value) => funit.FromRaw(ToRaw(Math.Max(-1.0, Math.Min(1.0, value)), Q30));

        public static float ToFloat(this funit value) => (float)(value.RawValue / Q30);

        static int ToRaw(double value, double scale)
        {
            if (double.IsNaN(value))
                return 0;
            double scaled = Math.Round(value * scale, MidpointRounding.ToEven);
            if (scaled >= int.MaxValue) return int.MaxValue;
            if (scaled <= int.MinValue) return int.MinValue;
            return (int)scaled;
        }
        #endregion

        #region Angles
        /// <summary>Degrees (any range) to Angle32, rounded to nearest and wrapped.</summary>
        public static fAngle DegreesToAngle(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return fAngle.Zero;
            double turns = degrees / 360.0;
            turns -= Math.Floor(turns);
            double raw = Math.Round(turns * TURN32, MidpointRounding.ToEven);
            return fAngle.FromRaw(raw >= TURN32 ? 0u : (uint)raw);
        }

        public static fAngleDelta DegreesToAngleDelta(float degrees) => DegreesToAngle(degrees).ToDelta();

        /// <summary>Angle in [0, 360).</summary>
        public static float ToDegrees(this fAngle angle) => (float)(angle.RawValue * (360.0 / TURN32));

        /// <summary>Signed angle in [-180, 180).</summary>
        public static float ToDegrees(this fAngleDelta angle) => (float)(angle.RawValue * (360.0 / TURN32));
        #endregion

        #region Vectors
        public static fVector2 ToFixed(this Vector2 v) => new fVector2(v.x.ToFixed(), v.y.ToFixed());

        public static fVector3 ToFixed(this Vector3 v) => new fVector3(v.x.ToFixed(), v.y.ToFixed(), v.z.ToFixed());

        public static Vector2 ToVector2(this fVector2 v) => new Vector2(v.x.ToFloat(), v.y.ToFloat());

        public static Vector3 ToVector3(this fVector3 v) => new Vector3(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());

        public static Vector2 ToVector2(this fUnitVector2 v) => new Vector2(v.x.ToFloat(), v.y.ToFloat());

        public static Vector3 ToVector3(this fUnitVector3 v) => new Vector3(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());

        /// <summary>Direction from a Unity vector (any magnitude); false for a zero / NaN vector.</summary>
        public static bool TryToUnitVector(this Vector3 v, out fUnitVector3 result)
        {
            // IEEE-754 sqrt and division are correctly rounded, so this is identical on every platform.
            double x = v.x, y = v.y, z = v.z;
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (!(length > 0.0) || double.IsInfinity(length))
            {
                result = default;
                return false;
            }
            const double Q28 = Q30 / 4;
            return fUnitVector3.TryFromRaw(ToRaw(x / length, Q28), ToRaw(y / length, Q28), ToRaw(z / length, Q28), out result);
        }
        #endregion

        #region Quaternions
        /// <summary>Unity quaternion to a normalized Q1.30 quaternion (identity for a zero quaternion).</summary>
        public static fQuaternion ToFixed(this Quaternion q)
        {
            // Q1.28 keeps headroom for slightly non-unit float quaternions; normalization rescales.
            const double Q28 = Q30 / 4;
            return fQuaternion.TryFromRaw(ToRaw(q.x, Q28), ToRaw(q.y, Q28), ToRaw(q.z, Q28), ToRaw(q.w, Q28), out fQuaternion result)
                ? result
                : fQuaternion.identity;
        }

        public static Quaternion ToQuaternion(this fQuaternion q) => new Quaternion(q.x.ToFloat(), q.y.ToFloat(), q.z.ToFloat(), q.w.ToFloat());
        #endregion
    }
}
