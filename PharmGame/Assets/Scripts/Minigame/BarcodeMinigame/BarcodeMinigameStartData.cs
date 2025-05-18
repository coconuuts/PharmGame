using System.Collections.Generic;
using Systems.Inventory; // Needed for ItemDetails
using Systems.Interaction; // Needed for CashRegisterInteractable

namespace Systems.Minigame // Or a common Data namespace if preferred
{
    /// <summary>
    /// Data structure to pass required information when starting the Barcode Minigame.
    /// This is passed via the IMinigame.SetupAndStart(object data) method.
    /// </summary>
    public struct BarcodeMinigameStartData
    {
        public List<(ItemDetails details, int quantity)> ItemsToScan;
        public CashRegisterInteractable InitiatingRegister;

        public BarcodeMinigameStartData(List<(ItemDetails details, int quantity)> itemsToScan, CashRegisterInteractable initiatingRegister)
        {
            ItemsToScan = itemsToScan;
            InitiatingRegister = initiatingRegister;
        }
    }
}