using System;
using UnityEngine; // Added for Transform

namespace Systems.Minigame
{
    /// <summary>
    /// Interface defining the contract for all general minigame components.
    /// Any script implementing this interface can be treated as a minigame.
    /// </summary>
    public interface IMinigame
    {
        /// <summary>
        /// Gets the Transform the camera should move to when the minigame *initially* starts (entering Minigame state).
        /// </summary>
        Transform InitialCameraTarget { get; } // ADDED

        /// <summary>
        /// Gets the duration for the initial camera movement when the minigame starts.
        /// </summary>
        float InitialCameraDuration { get; } // ADDED


        /// <summary>
        /// Sets up and starts the minigame with initial data.
        /// The type of data passed depends on the specific minigame.
        /// </summary>
        /// <param name="data">Initial setup data for the minigame.</param>
        void SetupAndStart(object data);

        /// <summary>
        /// Performs final cleanup for the minigame, often involving hiding UI or preparing for deactivation.
        /// This is called when the minigame session ends (either by completion or early exit).
        /// --- MODIFIED: Added parameter to indicate if the ending was an abort ---
        /// </summary>
        /// <param name="wasAborted">True if the minigame was aborted (e.g., by player pressing Escape), false if it reached a natural end state.</param>
        void End(bool wasAborted);

        /// <summary>
        /// Event triggered when the minigame session is completed or aborted.
        /// --- MODIFIED: The object parameter indicates success (true) or failure/abort (false). ---
        /// </summary>
        /// <remarks>
        /// The object parameter should be a boolean: true for success, false for failure/abort.
        /// The central MinigameManager will subscribe to this event.
        /// </remarks>
        event Action<object> OnMinigameCompleted;
    }
}