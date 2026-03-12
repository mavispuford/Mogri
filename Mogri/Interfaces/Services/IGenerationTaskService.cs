using System;
using System.Threading.Tasks;
using Mogri.Models;

namespace Mogri.Interfaces.Services
{
    public interface IGenerationTaskService
    {
        /// <summary>
        /// Whether generation is currently in progress.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Start generation. Receives cloned settings + the pre-resized init/mask
        /// images so the service has everything it needs without referencing the VM.
        /// </summary>
        Task StartAsync(GenerationTaskRequest request);

        /// <summary>
        /// Cancel the current generation.
        /// </summary>
        Task CancelAsync();

        /// <summary>
        /// Fired on every progress update (0.0 → 1.0).
        /// NOTE: Use WeakEventManager or WeakReferenceMessenger to prevent memory leaks.
        /// </summary>
        event EventHandler<float>? ProgressChanged;

        /// <summary>
        /// Fired when generation completes (success or failure).
        /// NOTE: Use WeakEventManager or WeakReferenceMessenger to prevent memory leaks.
        /// </summary>
        event EventHandler<GenerationTaskResult>? Completed;

        /// <summary>
        /// Holds the most recent completed result until consumed.
        /// </summary>
        GenerationTaskResult? LastResult { get; }

        /// <summary>
        /// Clears the last result after it has been consumed.
        /// </summary>
        void ClearLastResult();
    }
}
