using System;
using UnityEngine; // UnityEngine is often needed for interfaces if they use Unity types like GameObject, Transform, MonoBehaviour

namespace Systems.Minigame // Or the namespace where your other minigame scripts reside
{
    /// <summary>
    /// Interface defining the contract for all minigame components.
    /// Any script implementing this interface can be treated as a minigame by the system.
    /// </summary>
    public interface IMinigame
    {
        /// <summary>
        /// Sets up and starts the minigame with specific data.
        /// </summary>
        /// <param name="data">An object containing setup data for the minigame (e.g., items to scan, difficulty level).
        /// The specific minigame implementation will need to cast this to the expected type.</param>
        void SetupAndStart(object data);

        /// <summary>
        /// Resets the minigame's state to its initial condition without deactivating the GameObject or UI.
        /// Useful for restarting or cleaning up before ending.
        /// </summary>
        void Reset();

        /// <summary>
        /// Performs final cleanup for the minigame, often involving hiding UI or preparing for deactivation.
        /// This is called when the minigame session ends (either by completion or early exit).
        /// </summary>
        void End();

        /// <summary>
        /// Event triggered when the minigame is successfully completed.
        /// </summary>
        /// <remarks>
        /// The object parameter can carry completion data, such as score, currency earned, or items won.
        /// The central MinigameManager will subscribe to this event.
        /// </remarks>
        event Action<object> OnMinigameCompleted;

        // You might add other members here later if needed by all minigames, e.g.:
        // GameObject GetUIRoot();
        // MinigameType Type { get; } // If you use an enum to identify types
    }
}