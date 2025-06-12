// --- START OF FILE ICraftingMinigame.cs ---

using System;
using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic; // Needed for Dictionary // <-- Added using directive

namespace Systems.CraftingMinigames
{
    /// <summary>
    /// Interface defining the contract for all crafting minigame components.
    /// Any script implementing this interface can be treated as a crafting minigame.
    /// </summary>
    public interface ICraftingMinigame
    {
        /// <summary>
        /// Gets the Transform the camera should move to when the minigame *initially* starts (entering Minigame state).
        /// </summary>
        Transform InitialCameraTarget { get; }

        /// <summary>
        /// Gets the duration for the initial camera movement when the minigame starts.
        /// </summary>
        float InitialCameraDuration { get; }


        /// <summary>
        /// Sets up and starts the crafting minigame with recipe details, batches, and additional parameters.
        /// --- MODIFIED: Added parameters dictionary ---
        /// </summary>
        /// <param name="recipe">The CraftingRecipe being crafted.</param>
        /// <param name="batches">The number of batches being crafted.</param>
        /// <param name="parameters">A dictionary of additional parameters for minigame setup (e.g., target count from prescription).</param>
        void SetupAndStart(CraftingRecipe recipe, int batches, Dictionary<string, object> parameters);
        // --- END MODIFIED ---

        /// <summary>
        /// Performs final cleanup for the crafting minigame, often involving hiding UI or preparing for deactivation.
        /// This is called when the minigame session ends (either by completion or early exit).
        /// --- MODIFIED: Added parameter to indicate if the ending was an abort ---
        /// </summary>
        /// <param name="wasAborted">True if the minigame was aborted (e.g., by player pressing Escape), false if it reached a natural end state.</param>
        void EndMinigame(bool wasAborted);


        /// <summary>
        /// Event triggered when the crafting minigame session is completed or aborted.
        /// --- MODIFIED: The object parameter indicates success (true) or failure/abort (false). ---
        /// </summary>
        /// <remarks>
        /// The object parameter should be a boolean: true for success, false for failure/abort.
        /// The central CraftingMinigameManager will subscribe to this event.
        /// </remarks>
        event Action<object> OnCraftingMinigameCompleted;
    }
}
// --- END OF FILE ICraftingMinigame.cs ---