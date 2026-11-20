namespace fMath.Diagnostics
{
    /// <summary>
    /// FNV-1a 64-bit hash over raw integer values (little-endian byte order, independent of the
    /// platform's endianness). Used for raw-hash determinism checks and world hashes.
    /// </summary>
    public struct fRawHash
    {
        const ulong OFFSET_BASIS = 14695981039346656037UL;
        const ulong PRIME = 1099511628211UL;

        ulong _hash;
        long _count;

        public static fRawHash Create() => new fRawHash { _hash = OFFSET_BASIS };

        public ulong Value => _hash;

        /// <summary>Number of 32-bit words added.</summary>
        public long Count => _count;

        public void Add(int value) => Add(unchecked((uint)value));

        public void Add(uint value)
        {
            ulong h = _hash;
            h = (h ^ (value & 0xFFu)) * PRIME;
            h = (h ^ ((value >> 8) & 0xFFu)) * PRIME;
            h = (h ^ ((value >> 16) & 0xFFu)) * PRIME;
            h = (h ^ (value >> 24)) * PRIME;
            _hash = h;
            _count++;
        }

        public void Add(long value)
        {
            Add(unchecked((uint)value));
            Add(unchecked((uint)(value >> 32)));
        }

        public void Add(ulong value) => Add(unchecked((long)value));
        public void Add(bool value) => Add(value ? 1u : 0u);
        public void Add(ffloat value) => Add(value.RawValue);
        public void Add(funit value) => Add(value.RawValue);
        public void Add(fAngle value) => Add(value.RawValue);
        public void Add(fAngleDelta value) => Add(value.RawValue);

        public void Add(fVector2 v) { Add(v.x.RawValue); Add(v.y.RawValue); }
        public void Add(fVector3 v) { Add(v.x.RawValue); Add(v.y.RawValue); Add(v.z.RawValue); }
        public void Add(fUnitVector2 v) { Add(v.x.RawValue); Add(v.y.RawValue); }
        public void Add(fUnitVector3 v) { Add(v.x.RawValue); Add(v.y.RawValue); Add(v.z.RawValue); }
        public void Add(fWideVector3 v) { Add(v.x); Add(v.y); Add(v.z); }
        public void Add(fQuaternion q) { Add(q.x.RawValue); Add(q.y.RawValue); Add(q.z.RawValue); Add(q.w.RawValue); }
    }
}
