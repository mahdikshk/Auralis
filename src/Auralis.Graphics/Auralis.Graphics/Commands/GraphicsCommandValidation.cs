using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;

namespace Auralis.Graphics.Commands;

/// <summary>Shared validation rules for command and timeline operations.</summary>
internal static class GraphicsCommandValidation
{
    internal static void ValidateIndexedIndirectIndexBufferOffset(ulong indexBufferOffset)
    {
        if (indexBufferOffset != 0)
            throw new GraphicsValidationException(
                "Indexed indirect drawing does not support a non-zero index-buffer offset.");
    }

    internal static void ValidateTextureCopyNotAliased(
        IGraphicsTexture source,
        IGraphicsTexture destination)
    {
        if (ReferenceEquals(source, destination))
            throw new GraphicsValidationException("A texture cannot be copied onto itself.");
    }

    internal static void ValidateCpuFenceSignalDoesNotOvertakeGpu(
        ulong cpuSignalValue,
        ulong? earliestPendingGpuValue)
    {
        if (earliestPendingGpuValue is ulong pendingValue && pendingValue < cpuSignalValue)
            throw new GraphicsValidationException(
                $"CPU fence signal value {cpuSignalValue} cannot overtake pending GPU signal value {pendingValue}.");
    }
}

/// <summary>
/// Completes submitted command-list lifecycle transitions only after failed GPU work has been
/// brought to an idle point. The caller owns the backend context serialization while this runs.
/// </summary>
internal static class GraphicsCommandListSubmissionRecovery
{
    internal static void RecoverFailedSubmission<TList>(
        IReadOnlyList<TList> submittedLists,
        int submittedCount,
        bool commandsStarted,
        Action finishGpuWork,
        Action<TList> markCompleted)
    {
        ArgumentNullException.ThrowIfNull(submittedLists);
        ArgumentNullException.ThrowIfNull(finishGpuWork);
        ArgumentNullException.ThrowIfNull(markCompleted);
        if ((uint)submittedCount > (uint)submittedLists.Count)
            throw new ArgumentOutOfRangeException(nameof(submittedCount));

        // A failed Finish must leave submitted lists Pending so disposal can retry the safe path.
        if (commandsStarted)
            finishGpuWork();

        for (int i = 0; i < submittedCount; i++)
            markCompleted(submittedLists[i]);
    }
}
