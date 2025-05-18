using UnityEngine;
using System; // Needed for Action event
using System.Collections.Generic;
using Systems.Interaction; // Needed for CashRegisterInteractable and InteractionResponse types
using System.Linq; // Needed for Linq methods
using Systems.Inventory; // Needed for ItemDetails
using Systems.Economy; // Needed for EconomyManager
// Removed using Systems.GameStates; - Specific minigame components shouldn't directly reference MenuManager or states

namespace Systems.Minigame // Your Minigame namespace - ensure it matches BarcodeMinigameStartData
{
    /// <summary>
    /// Implements the specific logic for the Barcode Scanning Minigame.
    /// This script is a component attached to a minigame GameObject.
    /// </summary>
    public class BarcodeMinigame : MonoBehaviour, IMinigame
    {
        [Header("Grid References")]
        [Tooltip("Drag all the BarcodeSlot GameObjects from the UI grid here.")]
        [SerializeField] private List<BarcodeSlot> gridSlots;

        [Header("Barcode Settings")]
        [Tooltip("A list of possible sprites to use for the barcodes.")]
        [SerializeField] private List<Sprite> possibleBarcodeSprites;

        [Tooltip("The maximum number of barcodes visible on the grid at any given time.")]
        [SerializeField] private int maxVisibleBarcodes = 3;

        // Fields to store customer's purchase and the initiating register
        private List<(ItemDetails details, int quantity)> currentItemsToScan;
        private CashRegisterInteractable initiatingRegister;

        private int targetClickCount;
        private int clicksMade;
        private int barcodesCurrentlyVisible;

        private List<int> availableSlotIndices;

        private int lastClickedSlotIndex = -1;

        // --- ADDED: IMinigame Completion Event ---
        /// <summary>
        /// Event triggered when the Barcode Minigame is successfully completed.
        /// Passes the calculated total payment amount as an object.
        /// </summary>
        public event Action<object> OnMinigameCompleted;
        // ----------------------------------------


        private void Awake()
        {
            // --- REMOVED: Singleton Awake logic ---
            // if (Instance == null) Instance = this; else { ... }

            Debug.Log("BarcodeMinigame: Awake completed.");

            // --- REMOVED: Initial UI deactivation logic ---
            // The central manager handles the root GameObject activation.
            // if (minigameUIRoot != null) { minigameUIRoot.SetActive(false); } else { ... }

            // Initialize available slots and assign this instance to slots
            availableSlotIndices = new List<int>();
            if (gridSlots != null)
            {
                for (int i = 0; i < gridSlots.Count; i++)
                {
                    if (gridSlots[i] != null)
                    {
                        // --- MODIFIED: Assign this BarcodeMinigame instance to the slot ---
                        gridSlots[i].SetBarcodeMinigame(this); // Assuming BarcodeSlot has SetBarcodeMinigame()
                        availableSlotIndices.Add(i); // All slots are available initially
                    }
                    else Debug.LogWarning($"BarcodeMinigame: Grid slot at index {i} is null in the assigned list!", this);
                }
                 if(gridSlots.Count > 0 && availableSlotIndices.Count != gridSlots.Count)
                 {
                     Debug.LogWarning("BarcodeMinigame: Some grid slots are null in the list, availableSlotIndices size doesn't match gridSlots count.", this);
                 }
            }
            else { Debug.LogError("BarcodeMinigame: Grid Slots list is not assigned in the Inspector!", this); enabled = false; }

            // Initialize the item list
            currentItemsToScan = new List<(ItemDetails details, int quantity)>();
        }

        // --- REMOVED: OnEnable and OnDisable (no longer subscribing to MenuManager) ---
        // private void OnEnable() { MenuManager.OnStateChanged += HandleGameStateChanged; }
        // private void OnDisable() { MenuManager.OnStateChanged -= HandleGameStateChanged; }

        private void OnDestroy()
        {
            // --- REMOVED: Singleton OnDestroy logic ---
            // if (Instance == this) Instance = null;
        }

        // --- REMOVED: HandleGameStateChanged (UI handled by UIManager/Central Manager) ---
        // private void HandleGameStateChanged(...) { ... }


        // --- MODIFIED: Implement IMinigame.SetupAndStart ---
        /// <summary>
        /// Implements IMinigame.SetupAndStart. Sets up and starts the barcode minigame.
        /// Expects data to be a BarcodeMinigameStartData struct.
        /// </summary>
        /// <param name="data">Should be a BarcodeMinigameStartData struct.</param>
        public void SetupAndStart(object data) // Implements IMinigame
        {
             Debug.Log("BarcodeMinigame: SetupAndStart called.");

             // --- Cast the incoming data ---
             if (data is BarcodeMinigameStartData startData)
             {
                 // Now you have access to the itemsToScan and initiatingRegister from the data struct
                 List<(ItemDetails details, int quantity)> itemsToScan = startData.ItemsToScan;
                 CashRegisterInteractable register = startData.InitiatingRegister;

                 if (gridSlots == null || gridSlots.Count == 0) { Debug.LogError("BarcodeMinigame: Cannot start - gridSlots list is empty or null."); return; }
                 if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0) { Debug.LogError("BarcodeMinigame: Cannot start - possibleBarcodeSprites list is empty or null."); return; }
                  if (itemsToScan == null || itemsToScan.Count == 0) { Debug.LogWarning("BarcodeMinigame: Cannot start - itemsToScan list is null or empty."); return; }
                 if (register == null) { Debug.LogError("BarcodeMinigame: Cannot start - initiating register is null."); return; }


                // Store the customer's purchase list and the initiating register
                currentItemsToScan = new List<(ItemDetails details, int quantity)>(itemsToScan); // Store a copy
                initiatingRegister = register;

                // Calculate target clicks from the item list
                targetClickCount = currentItemsToScan.Sum(item => item.quantity);

                Debug.Log($"BarcodeMinigame: Starting minigame with target clicks (total quantity): {targetClickCount}. Items to scan: {currentItemsToScan.Count} distinct types.");

                clicksMade = 0;
                barcodesCurrentlyVisible = 0;
                lastClickedSlotIndex = -1; // Reset last clicked slot index

                ClearGrid(); // Clear any existing barcodes and prepare slots

                // Spawn the initial set of barcodes (up to maxVisibleBarcodes or targetCount if less)
                int initialBarcodesToSpawn = Mathf.Min(maxVisibleBarcodes, targetClickCount);
                Debug.Log($"BarcodeMinigame: Spawning initial {initialBarcodesToSpawn} barcodes.");
                for (int i = 0; i < initialBarcodesToSpawn; i++)
                {
                    SpawnBarcode(); // Spawn a barcode in a random empty slot
                }
             }
             else
             {
                 Debug.LogError($"BarcodeMinigame: Received incorrect data type for SetupAndStart. Expected BarcodeMinigameStartData.", this);
             }
        }

        // --- MODIFIED: Implement IMinigame.Reset ---
        /// <summary>
        /// Implements IMinigame.Reset. Resets the minigame state and clears the grid.
        /// </summary>
        public void Reset() // Implements IMinigame
        {
            Debug.Log("BarcodeMinigame: Reset called.");
            targetClickCount = 0;
            clicksMade = 0;
            barcodesCurrentlyVisible = 0;
            lastClickedSlotIndex = -1; // Reset last clicked slot index

            // Clear stored purchase list and register reference
            if (currentItemsToScan != null) currentItemsToScan.Clear();
            initiatingRegister = null;

            ClearGrid(); // Clears sprites and resets available slots
        }

         // --- ADDED: Implement IMinigame.End ---
         /// <summary>
         /// Implements IMinigame.End. Performs final cleanup for the barcode minigame.
         /// In this case, it just calls Reset.
         /// </summary>
        public void End() // Implements IMinigame
        {
             Debug.Log("BarcodeMinigame: End called.");
             // For the barcode game, ending is similar to resetting.
             Reset();
             // If you had specific visuals or states to clean up *before* deactivating the GameObject, do it here.
        }
        // -------------------------------------


        private void ClearGrid()
        {
            if (gridSlots == null) return;

            availableSlotIndices.Clear(); // Clear and repopulate available slots
            for (int i = 0; i < gridSlots.Count; i++)
            {
                if (gridSlots[i] != null)
                {
                    gridSlots[i].ClearSprite(); // Assuming BarcodeSlot has ClearSprite()
                    availableSlotIndices.Add(i); // Add the index back to available
                }
                 else Debug.LogWarning($"BarcodeMinigame: Grid slot at index {i} is null during ClearGrid!", this);
            }
            Debug.Log("BarcodeMinigame: Grid cleared. All slot indices are available.");
        }

        /// <summary>
        /// Spawns a single barcode in a randomly selected empty slot, avoiding the last clicked slot.
        /// Does nothing if no suitable slots are available or no sprites are possible.
        /// </summary>
        private void SpawnBarcode()
        {
            // Only spawn if there are still clicks needed
            if (clicksMade >= targetClickCount)
            {
                Debug.LogWarning("BarcodeMinigame: Not spawning barcode - target clicks already met or exceeded.");
                return;
            }

             // Create a temporary list of slots we can actually spawn in (available AND not last clicked)
            List<int> spawnableSlotIndices = new List<int>(availableSlotIndices);

            // Remove the last clicked slot from potential spawn locations if it's available
            if (lastClickedSlotIndex != -1 && spawnableSlotIndices.Contains(lastClickedSlotIndex))
            {
                 spawnableSlotIndices.Remove(lastClickedSlotIndex);
            }

            if (spawnableSlotIndices.Count == 0)
            {
                Debug.LogWarning("BarcodeMinigame: Cannot spawn barcode - no suitable empty slots available (excluding last clicked).");
                return;
            }
            if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0)
            {
                Debug.LogError("BarcodeMinigame: Cannot spawn barcode - no possible sprites assigned!");
                return;
            }

            int randomIndexInSpawnableList = UnityEngine.Random.Range(0, spawnableSlotIndices.Count);
            int slotIndexToUse = spawnableSlotIndices[randomIndexInSpawnableList]; // Get the actual index from the gridSlots list


            availableSlotIndices.Remove(slotIndexToUse); // Remove from the MAIN available list

            BarcodeSlot slotToSpawnIn = gridSlots[slotIndexToUse];
            Sprite randomSprite = possibleBarcodeSprites[UnityEngine.Random.Range(0, possibleBarcodeSprites.Count)];

            if (slotToSpawnIn != null)
            {
                slotToSpawnIn.SetSprite(randomSprite); // Assuming BarcodeSlot has SetSprite()
                barcodesCurrentlyVisible++;
                Debug.Log($"BarcodeMinigame: Spawned barcode in slot {slotIndexToUse}. Visible: {barcodesCurrentlyVisible}. Available slots: {availableSlotIndices.Count}. Clicks made: {clicksMade}/{targetClickCount}");
            }
            else
            {
                Debug.LogError($"BarcodeMinigame: BarcodeSlot at index {slotIndexToUse} is null!", this);
            }
        }

        /// <summary>
        /// Called by a BarcodeSlot when it is clicked.
        /// Processes the click, checks win condition, and spawns new barcodes if needed.
        /// </summary>
        /// <param name="clickedSlot">The BarcodeSlot that was clicked.</param>
        // This method does NOT come from IMinigame, it's specific barcode logic called by the slot.
        public void BarcodeClicked(BarcodeSlot clickedSlot)
        {
             if (clickedSlot == null || clickedSlot.IsEmpty)
             {
                 Debug.LogWarning("BarcodeMinigame: BarcodeClicked called with null or empty slot.");
                 return;
             }

             int clickedSlotIndex = gridSlots.IndexOf(clickedSlot);
             if (clickedSlotIndex == -1)
             {
                  Debug.LogError("BarcodeMinigame: Could not find clicked slot in gridSlots list!", this);
                  return;
             }

             // Only process click if game is not already won
             if (clicksMade < targetClickCount)
             {
                Debug.Log($"BarcodeMinigame: Barcode clicked in slot: {clickedSlot.gameObject.name} (Index: {clickedSlotIndex})");

                clickedSlot.ClearSprite();
                barcodesCurrentlyVisible--;
                clicksMade++;

                lastClickedSlotIndex = clickedSlotIndex;
                Debug.Log($"BarcodeMinigame: Last clicked slot index set to: {lastClickedSlotIndex}");

                 if (!availableSlotIndices.Contains(clickedSlotIndex))
                 {
                      availableSlotIndices.Add(clickedSlotIndex);
                      Debug.Log($"BarcodeMinigame: Slot {clickedSlotIndex} made available.");
                 }
                 else
                 {
                      Debug.LogWarning($"BarcodeMinigame: Slot {clickedSlotIndex} was already in the available list after clicking?", this);
                 }

                Debug.Log($"BarcodeMinigame: Clicks Made: {clicksMade} / {targetClickCount}. Visible barcodes: {barcodesCurrentlyVisible}. Available slots: {availableSlotIndices.Count}");

                // --- Check Win Condition ---
                // Check if this click resulted in reaching the target count
                if (clicksMade >= targetClickCount) // Use >= just in case
                {
                    Debug.Log("BarcodeMinigame: Target clicks reached or exceeded.");
                    CheckWinCondition(); // Handle win logic
                }
                // --- Check if we need to spawn more barcodes ---
                // Only spawn if:
                // 1. There are clicks remaining (clicksMade < targetClickCount) - Handled by the outer if
                // 2. We haven't reached the max visible barcodes yet (barcodesCurrentlyVisible < maxVisibleBarcodes)
                // 3. There are enough *unclicked* items remaining that we *should* show another barcode
                //    (i.e., the number of unclicked items is > the number of currently visible barcodes)
                //    (targetClickCount - clicksMade) > barcodesCurrentlyVisible
                else if (barcodesCurrentlyVisible < maxVisibleBarcodes && (targetClickCount - clicksMade) > barcodesCurrentlyVisible)
                {
                     // Before spawning, check if there's actually an available slot excluding the last clicked one
                     List<int> spawnableSlotIndices = new List<int>(availableSlotIndices);
                      if (lastClickedSlotIndex != -1 && spawnableSlotIndices.Contains(lastClickedSlotIndex))
                      {
                          spawnableSlotIndices.Remove(lastClickedSlotIndex);
                      }

                      if (spawnableSlotIndices.Count > 0)
                      {
                           Debug.Log("BarcodeMinigame: Spawning another barcode...");
                           SpawnBarcode(); // Spawn another barcode
                      }
                      else
                      {
                           Debug.Log("BarcodeMinigame: Cannot spawn barcode - not enough suitable empty slots available to maintain max visible count.");
                      }
                }
                 else
                 {
                      Debug.Log($"BarcodeMinigame: Not spawning barcode. Current visible: {barcodesCurrentlyVisible}, Max visible: {maxVisibleBarcodes}, Items left: {targetClickCount - clicksMade}.");
                 }
             }
             else
             {
                  Debug.LogWarning($"BarcodeMinigame: Barcode clicked in slot {clickedSlotIndex}, but clicksMade ({clicksMade}) is already >= targetClickCount ({targetClickCount}). Click ignored.");
             }
        }


        /// <summary>
        /// Handles the actions when the minigame is won (target clicks are made).
        /// </summary>
        private void CheckWinCondition()
        {
            // This method should only be called when clicksMade >= targetClickCount
            if (clicksMade >= targetClickCount)
            {
                Debug.Log($"BarcodeMinigame: Minigame WON! Clicks Made: {clicksMade}. Target: {targetClickCount}.");

                // Ensure all barcodes are cleared visually if any are left
                ClearGrid(); // Clear any remaining visible barcodes and reset slots

                // Calculate Total Payment
                float totalPayment = 0f;
                if (currentItemsToScan != null)
                {
                    foreach (var item in currentItemsToScan)
                    {
                        if (item.details != null)
                        {
                            totalPayment += item.details.price * item.quantity;
                        }
                        else
                        {
                            Debug.LogWarning($"BarcodeMinigame: ItemDetails is null for an item in the purchase list. Cannot calculate price for this item.", this);
                        }
                    }
                }
                Debug.Log($"BarcodeMinigame: Calculated total payment: {totalPayment}");

                // --- MODIFIED: Trigger the completion event with a Tuple containing payment and register ---
                Debug.Log("BarcodeMinigame: Invoking OnMinigameCompleted event with payment and register.");
                // Create a tuple to pass both pieces of data
                OnMinigameCompleted?.Invoke(Tuple.Create(totalPayment, initiatingRegister)); // Requires 'using System;'
                // --------------------------------------------------------------------------------------

                // The central MinigameManager listening to this event will handle:
                // 1. Processing the payment (EconomyManager.AddCurrency)
                // 2. Notifying the initiating register (initiatingRegister.OnMinigameCompleted)
                // 3. Telling the MenuManager to exit the state (MenuManager.SetState(Playing))
            }
        }
    }
}