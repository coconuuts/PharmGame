using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse
// Assuming CraftingStation is in Systems.Crafting namespace
using Systems.Inventory;

namespace Systems.GameStates
{
    /// <summary>
    /// State action to call specific methods on the currently active CraftingStation.
    /// Assumes CraftingStation has public methods like OpenCraftingUI() and CloseCraftingUI().
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Call Crafting Station Method")]
    public class CallCraftingStationMethodActionSO : StateAction
    {
        public enum CraftingStationMethod
        {
            OpenUI,
            CloseUI
        }

        [Tooltip("The method to call on the CraftingStation.")]
        public CraftingStationMethod methodToCall;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            // Access the currently active CraftingStation via the MenuManager reference
            CraftingStation craftingStation = manager?.CurrentCraftingStation; // Use the public getter

            if (craftingStation != null)
            {
                switch (methodToCall)
                {
                    case CraftingStationMethod.OpenUI:
                        craftingStation.OpenCraftingUI(); // Assumes this method exists
                        // Debug.Log("CallCraftingStationMethodAction: Called OpenCraftingUI."); // Optional debug
                        break;
                    case CraftingStationMethod.CloseUI:
                        craftingStation.CloseCraftingUI(); // Assumes this method exists
                        // Debug.Log("CallCraftingStationMethodAction: Called CloseCraftingUI."); // Optional debug
                        break;
                }
            }
            else
            {
                Debug.LogWarning($"CallCraftingStationMethodActionSO: No current CraftingStation instance found via MenuManager.");
            }
        }
    }
}