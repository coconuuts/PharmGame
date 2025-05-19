using UnityEngine;
using Systems.Interaction; // Needed for CashRegisterInteractable
using Systems.Inventory; // Needed for ItemDetails (if relevant for start data)

namespace Systems.Minigame // Ensure this is in the correct namespace
{
    /// <summary>
    /// Contains the necessary information to start a specific minigame.
    /// </summary>
    public struct MinigameStartInfo
    {
        public IMinigame MinigameLogic;
        public GameObject UIRoot;
        public object StartData;
        public bool IsValid; // Indicate if valid info was found

        public MinigameStartInfo(IMinigame logic, GameObject uiRoot, object data)
        {
            MinigameLogic = logic;
            UIRoot = uiRoot;
            StartData = data;
            IsValid = true; // If we create it with values, it's valid
        }

        // Static property for an invalid/empty state
        public static MinigameStartInfo Invalid => new MinigameStartInfo { IsValid = false };
    }
}