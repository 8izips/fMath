#if UNITY_6000_6_OR_NEWER
using System;
using Unity.Burst;

/// <summary>
/// Synchronous Burst direct calls sharing the scalar integer kernels. When Burst is disabled or
/// unavailable these execute managed code. Within a larger Burst job, call the original scalar
/// APIs to avoid a native boundary per element.
/// Batch calls borrow disjoint buffers for the duration of the call; they allocate and retain nothing.
/// count must be nonnegative; zero permits null pointers and accesses no elements. Otherwise every
/// buffer must contain at least count elements, have natural alignment (4 bytes for math structs),
/// and remain valid/pinned for the entire call. No overlap (including exact in-place use) or concurrent
/// conflicting access is permitted. Pointer validity, lengths and overlap are the caller's responsibility.
/// </summary>
[BurstCompile]
public static class fMathBurst
{
    public static void Sqrt(in ffloat value, out ffloat result) => SqrtKernel(in value, out result, out _);
    public static bool TryNormalize(in fVector3 value, out fUnitVector3 result)
    {
        NormalizeKernel(in value, out result, out bool success, out _);
        return success;
    }
    public static void Slerp(in fQuaternion a, in fQuaternion b, in ffloat t, out fQuaternion result)
        => SlerpKernel(in a, in b, in t, out result, out _);

    public static unsafe void SqrtBatch(ffloat* input, ffloat* output, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        SqrtBatchKernel(input, output, count, out _);
    }
    /// <summary>Writes every output and success byte: 1 for nonzero vectors, 0 for zero vectors.</summary>
    public static unsafe void NormalizeBatch(fVector3* input, fUnitVector3* output, byte* success, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        NormalizeBatchKernel(input, output, success, count, out _);
    }
    public static unsafe void SlerpBatch(fQuaternion* a, fQuaternion* b, ffloat* t, fQuaternion* output, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        SlerpBatchKernel(a, b, t, output, count, out _);
    }

    // The marker observes each actual production entry, including zero-count batches. Tests have
    // existing friend-assembly access; public wrappers need not expose diagnostic state.
    [BurstCompile(CompileSynchronously = true)]
    internal static void SqrtKernel(in ffloat value, out ffloat result, out bool native)
    {
        native = true; MarkManaged(ref native);
        result = ffloat.Sqrt(value);
    }
    [BurstCompile(CompileSynchronously = true)]
    internal static void NormalizeKernel(in fVector3 value, out fUnitVector3 result, out bool success, out bool native)
    {
        native = true; MarkManaged(ref native);
        success = fVector3.TryNormalize(value, out result);
    }
    [BurstCompile(CompileSynchronously = true)]
    internal static void SlerpKernel(in fQuaternion a, in fQuaternion b, in ffloat t, out fQuaternion result, out bool native)
    {
        native = true; MarkManaged(ref native);
        result = fQuaternion.Slerp(a, b, t);
    }
    [BurstCompile(CompileSynchronously = true)]
    internal static unsafe void SqrtBatchKernel(ffloat* input, ffloat* output, int count, out bool native)
    {
        native = true; MarkManaged(ref native);
        for (int i = 0; i < count; i++) output[i] = ffloat.Sqrt(input[i]);
    }
    [BurstCompile(CompileSynchronously = true)]
    internal static unsafe void NormalizeBatchKernel(fVector3* input, fUnitVector3* output, byte* success, int count, out bool native)
    {
        native = true; MarkManaged(ref native);
        for (int i = 0; i < count; i++) success[i] = fVector3.TryNormalize(input[i], out output[i]) ? (byte)1 : (byte)0;
    }
    [BurstCompile(CompileSynchronously = true)]
    internal static unsafe void SlerpBatchKernel(fQuaternion* a, fQuaternion* b, ffloat* t, fQuaternion* output, int count, out bool native)
    {
        native = true; MarkManaged(ref native);
        for (int i = 0; i < count; i++) output[i] = fQuaternion.Slerp(a[i], b[i], t[i]);
    }
    [BurstDiscard]
    static void MarkManaged(ref bool native) => native = false;
}
#endif
