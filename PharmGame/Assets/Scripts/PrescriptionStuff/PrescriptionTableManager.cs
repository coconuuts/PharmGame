using UnityEngine;
using Systems.Inventory;
using System;
using System.Collections.Generic;

public class PrescriptionTableManager : MonoBehaviour
{
    [Header("Main Inventories")]
    [SerializeField] private Inventory mainPrescriptiontableInventory; // The inventory this manager primarily monitors
    [SerializeField] private Inventory toolbarInventory; // The character's main inventory (toolbar)

    [Header("UI Roots")]
    [SerializeField] private GameObject prescriptionTableInventoryUIRoot; // The overall UI root for this manager's interface
    [SerializeField] private GameObject pillInventoryUIRoot;
    [SerializeField] private GameObject liquidInventoryUIRoot;
    [SerializeField] private GameObject inhalerInventoryUIRoot;
    [SerializeField] private GameObject insulinInventoryUIRoot;

    [Header("Specific Prescription Inventories")]
    [SerializeField] private Inventory pillInventory; // The Inventory component for the pill prescription table
    [SerializeField] private Inventory liquidInventory; // The Inventory component for the liquid prescription table
    [SerializeField] private Inventory inhalerInventory; // The Inventory component for the inhaler prescription table
    [SerializeField] private Inventory insulinInventory; // The Inventory component for the insulin prescription table

    [Header("Process Button")] // New Header for the button UI
    [SerializeField] private GameObject processButtonUIRoot; // The button UI GameObject to activate/deactivate

    // Using Dictionaries to map ItemLabels to their corresponding specific UI roots and Inventory components
    private Dictionary<ItemLabel, GameObject> specificUIRoots;
    private Dictionary<ItemLabel, Inventory> specificInventories;

    // To track the previous active state of the main UI root
    private bool _wasPrescriptionTableUIRootActive;

    // To track which specific ItemLabel was relevant in the main inventory in the previous state
    private ItemLabel _previousRelevantLabel = ItemLabel.None;

    // To keep a reference to the specific inventory we are currently monitoring for button state
    private Inventory _currentlyMonitoredSpecificInventory; // New field

    public void Awake()
    {
        // Initialize the dictionaries mapping relevant ItemLabels to their specific UI GameObjects and Inventory components
        specificUIRoots = new Dictionary<ItemLabel, GameObject>
        {
            { ItemLabel.PillStock, pillInventoryUIRoot },
            { ItemLabel.LiquidStock, liquidInventoryUIRoot },
            { ItemLabel.InhalerStock, inhalerInventoryUIRoot },
            { ItemLabel.InsulinStock, insulinInventoryUIRoot }
        };

        specificInventories = new Dictionary<ItemLabel, Inventory>
        {
            { ItemLabel.PillStock, pillInventory },
            { ItemLabel.LiquidStock, liquidInventory },
            { ItemLabel.InhalerStock, inhalerInventory },
            { ItemLabel.InsulinStock, insulinInventory }
        };

        // Ensure all specific inventory UI roots are initially inactive
        SetAllSpecificInventoryUIRootsActive(false);

        // Ensure the process button is initially inactive
        if (processButtonUIRoot != null)
        {
             processButtonUIRoot.SetActive(false);
        }
         else
        {
             Debug.LogWarning("PrescriptionTableManager: Process Button UI Root is not assigned! Please assign it in the inspector.", this);
        }

        _currentlyMonitoredSpecificInventory = null; // Initialize
    }

    private void Start()
    {
        // Subscribe to the main inventory state changes once it's likely initialized
        if (mainPrescriptiontableInventory != null && mainPrescriptiontableInventory.InventoryState != null)
        {
            mainPrescriptiontableInventory.InventoryState.AnyValueChanged += OnMainInventoryStateChanged;
        }
        else
        {
            Debug.LogError("PrescriptionTableManager: mainPrescriptiontableInventory or its InventoryState is null. Cannot subscribe to events.", this);
        }

        // Initialize the previous state tracker for the overall UI root
        if (prescriptionTableInventoryUIRoot != null)
        {
             _wasPrescriptionTableUIRootActive = prescriptionTableInventoryUIRoot.activeInHierarchy;
        }
        else
        {
             Debug.LogError("PrescriptionTableManager: prescriptionTableInventoryUIRoot is not assigned! Functionality will be limited.", this);
             _wasPrescriptionTableUIRootActive = false; // Default to false if not assigned
        }

        // Perform initial UI update and handle item move/button state based on starting conditions
        UpdateUIRootsAndHandleItemMoveAndButtonState();
    }

    private void Update()
    {
        // Monitor the active state of the main Prescription Table UI root
        if (prescriptionTableInventoryUIRoot != null)
        {
            bool isCurrentlyActive = prescriptionTableInventoryUIRoot.activeInHierarchy; // Use activeInHierarchy

            // Detect if the active state of the overall UI root has changed
            if (_wasPrescriptionTableUIRootActive != isCurrentlyActive)
            {
                // The active state of the overall UI root has changed
                Debug.Log($"Prescription Table UI Root changed state: {_wasPrescriptionTableUIRootActive} -> {isCurrentlyActive}", this);

                // Call the central update method which handles UI state, item movement, and button state
                UpdateUIRootsAndHandleItemMoveAndButtonState();

                // Update the tracker for the next frame AFTER handling the change
                _wasPrescriptionTableUIRootActive = isCurrentlyActive;
            }
            // else: State hasn't changed this frame.
            // Inventory changes while the root is active are handled by OnMainInventoryStateChanged.
            // Specific inventory changes (for button state) are handled by OnSpecificInventoryStateChanged.
        }
    }


    private void OnDestroy()
    {
        // Unsubscribe from the main inventory event
        if (mainPrescriptiontableInventory != null && mainPrescriptiontableInventory.InventoryState != null)
        {
            mainPrescriptiontableInventory.InventoryState.AnyValueChanged -= OnMainInventoryStateChanged;
        }

        // Unsubscribe from the currently monitored specific inventory event if any
        if (_currentlyMonitoredSpecificInventory != null && _currentlyMonitoredSpecificInventory.InventoryState != null)
        {
            _currentlyMonitoredSpecificInventory.InventoryState.AnyValueChanged -= OnSpecificInventoryStateChanged;
            Debug.Log($"PrescriptionTableManager: Unsubscribed from specific inventory state changes on destroy.", this);
        }
    }

    // This method is called whenever the mainPrescriptiontableInventory's state changes
    private void OnMainInventoryStateChanged(ArrayChangeInfo<Item> changeInfo)
    {
        // When the main inventory changes, trigger the central update logic.
        // This logic will handle item movement, specific UI state updates, and button state updates.
        Debug.Log($"Main Prescription Table Inventory changed state. Triggering update. Change Type: {changeInfo.Type}", this);
        UpdateUIRootsAndHandleItemMoveAndButtonState();
    }

    /// <summary>
    /// Central method to update specific UI root active states, manage specific inventory subscriptions,
    /// handle item movement back to toolbar, and update the process button state.
    /// </summary>
    private void UpdateUIRootsAndHandleItemMoveAndButtonState()
    {
        // Rule 1: If the overall UI root is inactive, all specific UIs and the button must be inactive.
        if (prescriptionTableInventoryUIRoot != null && !prescriptionTableInventoryUIRoot.activeInHierarchy)
        {
            SetAllSpecificInventoryUIRootsActive(false);
            SetProcessButtonActive(false); // Also turn off the button
            // Reset states when the UI is closed
            _previousRelevantLabel = ItemLabel.None;
            // Unsubscribe from the specific inventory when the UI is closed
            ManageSpecificInventorySubscription(null);
            Debug.Log("Overall Prescription Table UI is inactive. Specific UIs and Button forced off. States reset.", this);
            return; // Stop here, no further logic needed if the main UI is off
        }

        // Rule 2: If the overall UI root IS active, determine specific UI state based on main inventory content,
        // handle potential item movement, manage specific inventory subscription, and update button state.

        if (mainPrescriptiontableInventory == null || mainPrescriptiontableInventory.InventoryState == null)
        {
            Debug.LogError("PrescriptionTableManager: Cannot update UI roots or handle item move. mainPrescriptiontableInventory or its InventoryState is null.", this);
            SetAllSpecificInventoryUIRootsActive(false); // Default to off if inventory is invalid
            SetProcessButtonActive(false); // Also turn off button if inventory is invalid
            _previousRelevantLabel = ItemLabel.None; // Reset state
            ManageSpecificInventorySubscription(null); // Ensure no subscription if main inventory is invalid
            return;
        }

        // --- Determine the CURRENT relevant label based on main inventory content ---
        ItemLabel currentRelevantLabel = FindRelevantLabelInMainInventory(); // Use helper

        // --- Handle Item Movement if the relevant item was removed from the main inventory ---
        // Condition: Overall UI is active (checked above), previously a specific UI was relevant,
        // and now a different specific UI is relevant or none is relevant.
        if (_previousRelevantLabel != ItemLabel.None && specificInventories.ContainsKey(_previousRelevantLabel) && currentRelevantLabel != _previousRelevantLabel)
        {
            Debug.Log($"Relevant item changed in main inventory ({_previousRelevantLabel} -> {currentRelevantLabel}). Checking specific inventory for item move back to toolbar.", this);

            Inventory specificInvToMoveFrom = specificInventories[_previousRelevantLabel];

            // Check if the specific inventory exists and contains any items
            if (specificInvToMoveFrom != null && specificInvToMoveFrom.InventoryState != null && specificInvToMoveFrom.InventoryState.Count > 0)
            {
                Debug.Log($"Specific inventory for {_previousRelevantLabel} is active and contains items. Attempting to move item to toolbar.", this);

                // Get the first item from the specific inventory (assuming it holds only one relevant item at a time)
                Item itemToMove = specificInvToMoveFrom.InventoryState[0];

                if (itemToMove != null && toolbarInventory != null && toolbarInventory.Combiner != null)
                {
                    // Attempt to add the item to the toolbar inventory
                    bool addedToToolbar = toolbarInventory.Combiner.AddItem(itemToMove);

                    if (addedToToolbar)
                    {
                        // If successfully added to the toolbar, remove it from the specific inventory
                        bool removedFromSpecific = specificInvToMoveFrom.Combiner.TryRemoveAt(0); // Remove from the first slot

                        if (removedFromSpecific)
                        {
                            Debug.Log($"Successfully moved item '{itemToMove.details?.Name ?? "Unknown"}' from specific inventory ({_previousRelevantLabel}) to toolbar.", this);
                            // Item moved, the specific inventory is now empty.
                            // The UI update below will turn off the specific UI, and the button update will turn off the button.
                        }
                        else
                        {
                             // This shouldn't happen if AddItem succeeded and TryRemoveAt(0) is valid for this setup
                             Debug.LogError($"Failed to remove item '{itemToMove.details?.Name ?? "Unknown"}' from specific inventory ({_previousRelevantLabel}) AFTER successfully adding to toolbar. Inventory state might be inconsistent!", this);
                             // Inventory state is messed up, but don't want to lose the item. Leave it in both? Or remove from toolbar? Let's leave in both and log error.
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to add item '{itemToMove.details?.Name ?? "Unknown"}' from specific inventory ({_previousRelevantLabel}) to toolbar. Leaving item in specific inventory.", this);
                        // If it can't go to the toolbar, we leave it in the specific inventory.
                        // The UI update below will turn off the specific UI, making it inaccessible until
                        // the correct item is back in the main inventory or the item is manually moved.
                        // The button state will update based on the item still being present here.
                    }
                }
                else
                {
                    if (itemToMove == null) Debug.LogWarning($"Item to move from specific inventory ({_previousRelevantLabel}) is null.", this);
                    if (toolbarInventory == null) Debug.LogError("Toolbar Inventory is not assigned!", this);
                    if (toolbarInventory != null && toolbarInventory.Combiner == null) Debug.LogError("Toolbar Inventory's Combiner is null!", this);
                     Debug.LogWarning($"Skipping item move attempt from specific inventory due to missing references.", this);
                }
            }
            else
            {
                 Debug.Log($"Specific inventory for {_previousRelevantLabel} is empty or not assigned. No item to move back to toolbar.", this);
            }
        }

        // --- Manage Subscription to the Specific Inventory ---
        // Determine which specific inventory we should NOW be monitoring for button state
        Inventory newInventoryToMonitor = null;
        if (specificInventories.TryGetValue(currentRelevantLabel, out Inventory foundInventory))
        {
            newInventoryToMonitor = foundInventory;
        }
        ManageSpecificInventorySubscription(newInventoryToMonitor);


        // --- Update Specific UI Root Active States ---
        // Activate the one matching currentRelevantLabel, deactivate all others.
        foreach (var pair in specificUIRoots)
        {
            bool shouldBeActive = (pair.Key == currentRelevantLabel);

            // Check if the specific UI root GameObject is assigned in the inspector
            if (pair.Value != null)
            {
                pair.Value.SetActive(shouldBeActive);
            }
            else
            {
                Debug.LogWarning($"PrescriptionTableManager: Specific UI Root GameObject for {pair.Key} is not assigned! Please assign it in the inspector.", this);
            }
        }

        // --- Update the Process Button State ---
        // This must happen *after* ManageSpecificInventorySubscription
        // because it relies on _currentlyMonitoredSpecificInventory
        UpdateProcessButtonState();


        // --- Update the previous relevant label for the next check ---
        _previousRelevantLabel = currentRelevantLabel;
    }

    /// <summary>
    /// Manages the subscription to the AnyValueChanged event of the specific inventory
    /// that is currently relevant based on the main inventory.
    /// </summary>
    private void ManageSpecificInventorySubscription(Inventory inventoryToSubscribe)
    {
        // If the inventory to subscribe to is the same as the one we're already monitoring, do nothing
        if (_currentlyMonitoredSpecificInventory == inventoryToSubscribe)
        {
            // Debug.Log("Subscription already correct.", this);
            return;
        }

        // If we were previously monitoring an inventory, unsubscribe from its event
        if (_currentlyMonitoredSpecificInventory != null && _currentlyMonitoredSpecificInventory.InventoryState != null)
        {
            _currentlyMonitoredSpecificInventory.InventoryState.AnyValueChanged -= OnSpecificInventoryStateChanged;
            Debug.Log($"PrescriptionTableManager: Unsubscribed from {_currentlyMonitoredSpecificInventory.gameObject.name}'s state changes.", this);
        }

        // Set the new inventory to monitor
        _currentlyMonitoredSpecificInventory = inventoryToSubscribe;

        // If the new inventory is valid, subscribe to its event
        if (_currentlyMonitoredSpecificInventory != null && _currentlyMonitoredSpecificInventory.InventoryState != null)
        {
            _currentlyMonitoredSpecificInventory.InventoryState.AnyValueChanged += OnSpecificInventoryStateChanged;
            Debug.Log($"PrescriptionTableManager: Subscribed to {_currentlyMonitoredSpecificInventory.gameObject.name}'s state changes.", this);
            // Immediately trigger the button state update based on the content of the newly monitored inventory.
            // This is important if the inventory already contains items when it becomes the relevant one.
             UpdateProcessButtonState();
        }
        else
        {
             // If newInventoryToMonitor is null, ensure button is off and state is correct
             Debug.Log("PrescriptionTableManager: No specific inventory to subscribe to.", this);
             SetProcessButtonActive(false);
        }
    }


    /// <summary>
    /// Handles state changes on the specific inventory we are currently monitoring.
    /// </summary>
    private void OnSpecificInventoryStateChanged(ArrayChangeInfo<Item> changeInfo)
    {
        // The content of the currently monitored specific inventory changed.
        // Update the state of the process button.
        Debug.Log($"Specific Inventory ({_currentlyMonitoredSpecificInventory?.gameObject.name}) state changed. Triggering Process Button update. Change Type: {changeInfo.Type}", this);
        UpdateProcessButtonState();
    }


    /// <summary>
    /// Sets the active state of the process button based on the content of the currently monitored specific inventory.
    /// </summary>
    private void UpdateProcessButtonState()
    {
        bool shouldButtonBeActive = false;

        // The button should be active only if the overall UI is open AND
        // the currently monitored specific inventory exists AND has items.
        if (prescriptionTableInventoryUIRoot != null && prescriptionTableInventoryUIRoot.activeInHierarchy &&
            _currentlyMonitoredSpecificInventory != null && _currentlyMonitoredSpecificInventory.InventoryState != null)
        {
             shouldButtonBeActive = _currentlyMonitoredSpecificInventory.InventoryState.Count > 0;
        }

        SetProcessButtonActive(shouldButtonBeActive);
    }

    /// <summary>
    /// Helper method to set the active state of the process button UI root.
    /// </summary>
    private void SetProcessButtonActive(bool active)
    {
        // Only change state if necessary
        if (processButtonUIRoot != null && processButtonUIRoot.activeSelf != active)
        {
             processButtonUIRoot.SetActive(active);
             Debug.Log($"Process Button UI Root set active: {active}", this);
        }
        else if (processButtonUIRoot == null)
        {
             Debug.LogWarning("PrescriptionTableManager: Process Button UI Root is null, cannot set active state.", this);
        }
    }


    /// <summary>
    /// Helper method to set the active state of all tracked specific UI roots.
    /// </summary>
    private void SetAllSpecificInventoryUIRootsActive(bool active)
    {
        if (specificUIRoots == null) return; // Should not happen after Awake, but good practice
        foreach (var pair in specificUIRoots)
        {
            if (pair.Value != null)
            {
                pair.Value.SetActive(active);
            }
            else
            {
                Debug.LogWarning($"PrescriptionTableManager: Specific UI Root GameObject for {pair.Key} is not assigned!", this);
            }
        }
    }

    /// <summary>
    /// Helper method to find the first relevant item label in the main inventory.
    /// </summary>
    private ItemLabel FindRelevantLabelInMainInventory()
    {
         if (mainPrescriptiontableInventory == null || mainPrescriptiontableInventory.InventoryState == null)
         {
              return ItemLabel.None;
         }

         Item[] currentItems = mainPrescriptiontableInventory.InventoryState.GetCurrentArrayState();
         // Iterate through physical slots only (excluding the ghost slot if GetCurrentArrayState provides it)
         // Assuming the relevant item will always be in a physical slot (0 to PhysicalSlotCount-1)
         int physicalSlotCount = mainPrescriptiontableInventory.Combiner != null ? mainPrescriptiontableInventory.Combiner.PhysicalSlotCount : currentItems.Length; // Fallback if Combiner is null

         for (int i = 0; i < physicalSlotCount; i++)
         {
              Item item = currentItems[i]; // Access item via index

              if (item != null && item.details != null)
              {
                   ItemLabel currentItemLabel = item.details.itemLabel;
                   if (specificUIRoots.ContainsKey(currentItemLabel)) // Check if it's one of our tracked labels
                   {
                        return currentItemLabel; // Return the first relevant label found
                   }
              }
         }
         return ItemLabel.None; // No relevant label found in physical slots
    }
}