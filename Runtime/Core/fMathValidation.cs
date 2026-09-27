using System;
using System.Diagnostics;
using System.Threading;

public enum fMathValidationKind
{
    /// <summary>A final value was not representable and was saturated.</summary>
    Saturation = 0,
    DivideByZero = 1,
    NegativeSqrt = 2,
    NormalizeZeroVector = 3,
    InvalidQuaternion = 4,
    /// <summary>A Q1.30 unit scalar left the [-1, 1] domain where one was required.</summary>
    UnitOutOfRange = 5,
    InvalidArgument = 6,
}

/// <summary>
/// Validation-build diagnostics.
///
/// Release behaviour never depends on this class: every invalid input already has a documented,
/// deterministic result (saturation, zero, identity ...). When the scripting define
/// <c>FMATH_VALIDATE</c> is set, the call sites are compiled in and every such event is counted and
/// forwarded to <see cref="Handler"/>, so development and CI builds can surface overflow, divide by
/// zero, negative sqrt, zero-vector normalization and invalid quaternions.
/// Without the define the calls are removed by the compiler ([Conditional]) and cost nothing.
/// </summary>
public static class fMathValidation
{
    const int KindCount = 7;
    static readonly int[] _counts = new int[KindCount];

    /// <summary>Optional callback, e.g. to log or throw in a validation build.</summary>
    public static Action<fMathValidationKind, string> Handler;

    public static bool IsEnabled
    {
        get
        {
#if FMATH_VALIDATE
            return true;
#else
            return false;
#endif
        }
    }

    public static int GetCount(fMathValidationKind kind) => Volatile.Read(ref _counts[(int)kind]);

    public static int TotalCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < KindCount; i++)
                total += Volatile.Read(ref _counts[i]);
            return total;
        }
    }

    public static void ResetCounts()
    {
        for (int i = 0; i < KindCount; i++)
            Interlocked.Exchange(ref _counts[i], 0);
    }

    [Conditional("FMATH_VALIDATE")]
    internal static void Report(fMathValidationKind kind, string context)
    {
        Interlocked.Increment(ref _counts[(int)kind]);
        Handler?.Invoke(kind, context);
    }
}
