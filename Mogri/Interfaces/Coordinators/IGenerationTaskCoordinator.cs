using Mogri.Models;

namespace Mogri.Interfaces.Coordinators;

/// <summary>
/// Coordinates long-running generation execution, progress, cancellation, and completed-result handoff.
/// </summary>
public interface IGenerationTaskCoordinator
{
    bool IsRunning { get; }

    Task StartAsync(GenerationTaskRequest request);

    Task CancelAsync();

    event EventHandler<float>? ProgressChanged;

    event EventHandler<GenerationTaskResult>? Completed;

    GenerationTaskResult? LastResult { get; }

    void ClearLastResult();
}