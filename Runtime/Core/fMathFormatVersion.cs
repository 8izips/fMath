using System;

/// <summary>
/// Numeric format version of fMath raw values. Every rollback session, replay, save and snapshot
/// must record it, and peers with different versions must not start a deterministic session.
///
///   V1 = legacy ffloat Q20.12 (not supported; no converter is provided)
///   V2 = ffloat Q16.16 + funit Q1.30 + fAngle Angle32
/// </summary>
public static class fMathFormatVersion
{
    public const int V1 = 1;
    public const int V2 = 2;
    public const int Current = V2;

    /// <summary>Human-readable description of the current raw formats.</summary>
    public const string CurrentDescription = "ffloat Q16.16, funit Q1.30, fAngle uint32 turn";

    public static bool IsCompatible(int remoteFormatVersion) => remoteFormatVersion == Current;
}

/// <summary>
/// Minimum information exchanged in a P2P handshake before a deterministic session starts.
/// All four values must match exactly.
/// </summary>
public readonly struct fDeterministicSessionHeader : IEquatable<fDeterministicSessionHeader>
{
    public readonly int ProtocolVersion;
    public readonly int MathFormatVersion;
    public readonly int GameSimulationVersion;
    public readonly ulong ContentHash;

    public fDeterministicSessionHeader(int protocolVersion, int gameSimulationVersion, ulong contentHash)
        : this(protocolVersion, fMathFormatVersion.Current, gameSimulationVersion, contentHash)
    {
    }

    public fDeterministicSessionHeader(int protocolVersion, int mathFormatVersion, int gameSimulationVersion, ulong contentHash)
    {
        ProtocolVersion = protocolVersion;
        MathFormatVersion = mathFormatVersion;
        GameSimulationVersion = gameSimulationVersion;
        ContentHash = contentHash;
    }

    /// <summary>True when a deterministic session between the two peers may start.</summary>
    public bool IsCompatibleWith(fDeterministicSessionHeader remote)
    {
        return MathFormatVersion == fMathFormatVersion.Current && Equals(remote);
    }

    public bool Equals(fDeterministicSessionHeader other)
    {
        return ProtocolVersion == other.ProtocolVersion && MathFormatVersion == other.MathFormatVersion
            && GameSimulationVersion == other.GameSimulationVersion && ContentHash == other.ContentHash;
    }

    public override bool Equals(object obj) => obj is fDeterministicSessionHeader other && Equals(other);

    public override int GetHashCode() => unchecked(((ProtocolVersion * 397 ^ MathFormatVersion) * 397 ^ GameSimulationVersion) * 397 ^ ContentHash.GetHashCode());

    public override string ToString() => "protocol " + ProtocolVersion + ", fMath v" + MathFormatVersion + ", simulation " + GameSimulationVersion + ", content " + ContentHash.ToString("X16");
}
