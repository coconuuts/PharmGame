// PrescriptionEvents.cs
using Systems.Inventory; // Ensure ItemLabel, Inventory, Item, ItemDetails are accessible
using System;
using UnityEngine; // For Debug logging if needed in events

namespace Prescription // Use your game namespace
{
    /// <summary>
    /// Static class holding all events for the Prescription system.
    /// Subscribe to these events to react to system state changes and actions.
    /// Invoke these events to signal state changes and actions.
    /// </summary>
    public static class PrescriptionEvents
    {
        // --- Events related to Overall UI State ---

        // Fired by PrescriptionTableManager when the overall UI root becomes active.
        public static event Action OnPrescriptionUIOpened;
        public static void InvokePrescriptionUIOpened()
        {
            Debug.Log("EVENT: OnPrescriptionUIOpened");
            OnPrescriptionUIOpened?.Invoke();
        }

        // Fired by PrescriptionTableManager when the overall UI root becomes inactive.
        public static event Action OnPrescriptionUIClosed;
        public static void InvokePrescriptionUIClosed()
        {
            Debug.Log("EVENT: OnPrescriptionUIClosed");
            OnPrescriptionUIClosed?.Invoke();
        }

        // OnPrescriptionSetupReady and OnSpecificInventoryContentChanged are removed.


        // --- Events related to Processing ---

        // Fired by PrescriptionTableManager's button click handler after initial checks pass.
        // Signals the intent to start processing for a specific item type.
        public static event Action<ItemLabel, Inventory, Item, Inventory, Item> OnProcessButtonClicked;
        public static void InvokeProcessButtonClicked(ItemLabel label, Inventory mainInv, Item mainItem, Inventory specificInv, Item secondaryItem)
        {
            Debug.Log($"EVENT: OnProcessButtonClicked({label})");
            OnProcessButtonClicked?.Invoke(label, mainInv, mainItem, specificInv, secondaryItem);
        }

        // Fired by a PrescriptionProcessor after ingredients are consumed successfully.
        // Includes details about the result item.
        public static event Action<ItemLabel, ItemDetails, int> OnIngredientsConsumed;
        public static void InvokeIngredientsConsumed(ItemLabel label, ItemDetails resultItemDetails, int resultQuantity)
        {
            Debug.Log($"EVENT: OnIngredientsConsumed({label}, {resultItemDetails?.Name ?? "NULL"}, {resultQuantity})");
            OnIngredientsConsumed?.Invoke(label, resultItemDetails, resultQuantity);
        }

        // Fired by a PrescriptionProcessor after crafting is fully complete (item added to output).
        // Includes the crafted Item instance AND the label that started the process.
        public static event Action<ItemLabel, Item> OnCraftingComplete;
        public static void InvokeCraftingComplete(ItemLabel label, Item craftedItem)
        {
            Debug.Log($"EVENT: OnCraftingComplete({label}, {craftedItem?.details?.Name ?? "NULL"})");
            OnCraftingComplete?.Invoke(label, craftedItem);
        }

        // Fired by a PrescriptionProcessor if crafting fails after starting.
        // Includes the label that started the process and a reason.
        public static event Action<ItemLabel, string> OnCraftingFailed;
        public static void InvokeCraftingFailed(ItemLabel label, string reason)
        {
            Debug.LogWarning($"EVENT: OnCraftingFailed({label}): {reason}");
            OnCraftingFailed?.Invoke(label, reason);
        }

        // --- Add other events as needed (e.g., OnMinigameStarted, OnMinigameComplete) ---
    }
}