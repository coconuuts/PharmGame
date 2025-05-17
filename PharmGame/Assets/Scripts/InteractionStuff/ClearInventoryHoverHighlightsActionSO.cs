using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse
using Systems.Inventory; // Needed for Systems.Inventory.Inventory, Visualizer, and InventorySelector

namespace Systems.GameStates // Place in the same namespace as your other StateActions
{
    /// <summary>
    /// State action to clear hover highlights from an inventory's UI.
    /// Targets the inventory stored in MenuManager's currentOpenInventoryComponent
    /// AND the player's toolbar inventory.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Clear Inventory Hover Highlights")]
    public class ClearInventoryHoverHighlightsActionSO : StateAction
    {
        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            if (manager == null)
            {
                Debug.LogWarning("ClearInventoryHoverHighlightsActionSO: MenuManager reference is null.", this);
                return;
            }

            // --- Clear highlights for the currently open dynamic inventory (if any) ---
            // Access the currently open inventory via the MenuManager reference's public getter.
            Systems.Inventory.Inventory dynamicInventoryToClear = manager.CurrentOpenInventoryComponent;

            if (dynamicInventoryToClear != null)
            {
                // Call the public helper method on MenuManager for the dynamic inventory
                manager.ClearHoverHighlights(dynamicInventoryToClear);
                // Debug.Log("ClearInventoryHoverHighlightsActionSO: Called ClearHoverHighlights for dynamic open inventory."); // Optional debug
            }
            // else
            // {
            //      Debug.Log("ClearInventoryHoverHighlightsActionSO: No dynamic open inventory found via MenuManager to clear highlights.");
            // }
            // ------------------------------------------------------------------------


            // --- Clear highlights for the player's toolbar inventory ---
            // Access the playerToolbarInventorySelector via the MenuManager reference's public getter.
            // We need to get the Inventory component from the same GameObject as the selector.
            InventorySelector toolbarSelector = manager.PlayerToolbarInventorySelector; // Assuming PlayerToolbarInventorySelector is a public getter on MenuManager

            if (toolbarSelector != null)
            {
                 // Get the Inventory component from the same GameObject
                 Systems.Inventory.Inventory toolbarInventoryToClear = toolbarSelector.GetComponent<Systems.Inventory.Inventory>();

                 if (toolbarInventoryToClear != null)
                 {
                      // Call the public helper method on MenuManager for the toolbar inventory
                      manager.ClearHoverHighlights(toolbarInventoryToClear);
                      // Debug.Log("ClearInventoryHoverHighlightsActionSO: Called ClearHoverHighlights for toolbar inventory."); // Optional debug
                 }
                 else
                 {
                      Debug.LogWarning("ClearInventoryHoverHighlightsActionSO: Could not find Inventory component on Player Toolbar Inventory Selector GameObject.", toolbarSelector.gameObject);
                 }
            }
            // else
            // {
            //      Debug.LogWarning("ClearInventoryHoverHighlightsActionSO: Player Toolbar Inventory Selector reference is null on MenuManager. Cannot clear toolbar highlights.");
            // }
            // -------------------------------------------------------

            // You might want a general debug log if *neither* was found, but separate logs are more informative.
            // if (dynamicInventoryToClear == null && toolbarSelector == null)
            // {
            //      Debug.LogWarning("ClearInventoryHoverHighlightsActionSO: No dynamic open inventory or toolbar selector found to clear highlights.");
            // }

        }
    }
}