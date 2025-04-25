using UnityEngine;
using System.Collections.Generic;
using Systems.Interaction; // Needed for InteractionResponse types (although not used directly here, common pattern)
using System.Linq; // Needed for Linq methods like .ToList() or .Contains(), Sum()
using Systems.Inventory; // Needed for ItemDetails
using Systems.Economy; // Needed for EconomyManager

namespace Systems.Minigame // Your Minigame namespace
{
    public class MinigameManager : MonoBehaviour
    {
        // ... (Existing fields remain, check against your current script)
        public static MinigameManager Instance { get; private set; }

        [Header("Grid References")]
        [Tooltip("Drag all the BarcodeSlot GameObjects from the UI grid here.")]
        [SerializeField] private List<BarcodeSlot> gridSlots;

        [Header("Barcode Settings")]
        [Tooltip("A list of possible sprites to use for the barcodes.")]
        [SerializeField] private List<Sprite> possibleBarcodeSprites;

        [Tooltip("The maximum number of barcodes visible on the grid at any given time.")]
        [SerializeField] private int maxVisibleBarcodes = 3;

        // --- MODIFIED: Fields to store customer's purchase and the initiating register ---
        private List<(ItemDetails details, int quantity)> currentItemsToScan; // Store the list of items the customer bought
        private CashRegisterInteractable initiatingRegister; // Store the reference to the register that started this minigame
        // ------------------------------------------------------------------------------

        private int targetClickCount; // This will now be derived from currentItemsToScan
        private int clicksMade;
        private int barcodesCurrentlyVisible;

        private List<int> availableSlotIndices;

        private int lastClickedSlotIndex = -1;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("MinigameManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
            Debug.Log("MinigameManager: Awake completed.");

            availableSlotIndices = new List<int>();
            if (gridSlots != null)
            {
                for (int i = 0; i < gridSlots.Count; i++)
                {
                    if (gridSlots[i] != null)
                    {
                        gridSlots[i].SetMinigameManager(this);
                    }
                    else Debug.LogWarning($"MinigameManager: Grid slot at index {i} is null in the assigned list!", this);
                }
            }
            else { Debug.LogError("MinigameManager: Grid Slots list is not assigned in the Inspector!", this); enabled = false; }

             // Initialize the item list
             currentItemsToScan = new List<(ItemDetails details, int quantity)>();

        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Starts the minigame with the given list of items to be scanned.
        /// Called by the MenuManager based on a StartMinigameResponse.
        /// </summary>
        /// <param name="itemsToScan">The list of items the customer is buying.</param>
        /// <param name="register">The CashRegisterInteractable that initiated this minigame.</param>
        // --- MODIFIED: Accept list of items and initiating register ---
        public void StartMinigame(List<(ItemDetails details, int quantity)> itemsToScan, CashRegisterInteractable register)
        {
             if (gridSlots == null || gridSlots.Count == 0) { Debug.LogError("MinigameManager: Cannot start minigame - gridSlots list is empty or null."); return; }
             if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0) { Debug.LogError("MinigameManager: Cannot start minigame - possibleBarcodeSprites list is empty or null."); return; }
             if (itemsToScan == null || itemsToScan.Count == 0) { Debug.LogWarning("MinigameManager: Cannot start minigame - itemsToScan list is null or empty."); return; }
             if (register == null) { Debug.LogError("MinigameManager: Cannot start minigame - initiating register is null."); return; }


            // Store the customer's purchase list and the initiating register
            currentItemsToScan = new List<(ItemDetails details, int quantity)>(itemsToScan); // Store a copy
            initiatingRegister = register;

            // --- Calculate target clicks from the item list ---
            targetClickCount = currentItemsToScan.Sum(item => item.quantity);
            // --------------------------------------------------

            Debug.Log($"MinigameManager: Starting minigame with target clicks (total quantity): {targetClickCount}. Items to scan: {currentItemsToScan.Count} distinct types.");

            clicksMade = 0;
            barcodesCurrentlyVisible = 0;
            lastClickedSlotIndex = -1; // Reset last clicked slot index

            ClearGrid(); // Clear any existing barcodes and prepare slots

            // Spawn the initial set of barcodes (up to maxVisibleBarcodes or targetCount if less)
            int initialBarcodesToSpawn = Mathf.Min(maxVisibleBarcodes, targetClickCount);
            Debug.Log($"MinigameManager: Spawning initial {initialBarcodesToSpawn} barcodes.");
            for (int i = 0; i < initialBarcodesToSpawn; i++)
            {
                SpawnBarcode(); // Spawn a barcode in a random empty slot
            }
        }

        /// <summary>
        /// Resets the minigame state and clears the grid.
        /// Called by the MenuManager upon exiting the minigame state.
        /// </summary>
        public void ResetMinigame()
        {
            Debug.Log("MinigameManager: Resetting minigame state.");
            targetClickCount = 0;
            clicksMade = 0;
            barcodesCurrentlyVisible = 0;
            lastClickedSlotIndex = -1; // Reset last clicked slot index

            // --- Clear stored purchase list and register reference ---
            if (currentItemsToScan != null) currentItemsToScan.Clear();
            initiatingRegister = null;
            // ------------------------------------------------------

            ClearGrid();
        }

        private void ClearGrid()
        {
             if (gridSlots == null) return;

             availableSlotIndices.Clear();
             for (int i = 0; i < gridSlots.Count; i++)
             {
                 if (gridSlots[i] != null)
                 {
                     gridSlots[i].ClearSprite();
                     availableSlotIndices.Add(i);
                 }
             }
             Debug.Log("MinigameManager: Grid cleared. All slot indices are available.");
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
                 Debug.LogWarning("MinigameManager: Not spawning barcode - target clicks already met or exceeded.");
                 return;
             }

             List<int> spawnableSlotIndices = new List<int>(availableSlotIndices);

             if (lastClickedSlotIndex != -1 && spawnableSlotIndices.Contains(lastClickedSlotIndex))
             {
                  spawnableSlotIndices.Remove(lastClickedSlotIndex);
             }

             if (spawnableSlotIndices.Count == 0)
             {
                 Debug.LogWarning("MinigameManager: Cannot spawn barcode - no suitable empty slots available.");
                 return;
             }
            if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0)
            {
                 Debug.LogError("MinigameManager: Cannot spawn barcode - no possible sprites assigned!");
                 return;
            }

            int randomIndex = UnityEngine.Random.Range(0, spawnableSlotIndices.Count);
            int slotIndexToUse = spawnableSlotIndices[randomIndex];

            availableSlotIndices.Remove(slotIndexToUse); // Remove from the MAIN available list

            BarcodeSlot slotToSpawnIn = gridSlots[slotIndexToUse];
            Sprite randomSprite = possibleBarcodeSprites[UnityEngine.Random.Range(0, possibleBarcodeSprites.Count)];

            if (slotToSpawnIn != null)
            {
                slotToSpawnIn.SetSprite(randomSprite);
                barcodesCurrentlyVisible++;
                 Debug.Log($"MinigameManager: Spawned barcode in slot {slotIndexToUse}. Visible: {barcodesCurrentlyVisible}. Available slots: {availableSlotIndices.Count}. Clicks made: {clicksMade}/{targetClickCount}");
            }
            else
            {
                 Debug.LogError($"MinigameManager: BarcodeSlot at index {slotIndexToUse} is null!", this);
            }
        }

/// <summary>
        /// Called by a BarcodeSlot when it is clicked.
        /// Processes the click, checks win condition, and spawns new barcodes if needed.
        /// </summary>
        /// <param name="clickedSlot">The BarcodeSlot that was clicked.</param>
        public void BarcodeClicked(BarcodeSlot clickedSlot)
        {
             if (clickedSlot == null || clickedSlot.IsEmpty)
             {
                 Debug.LogWarning("MinigameManager: BarcodeClicked called with null or empty slot.");
                 return;
             }

             int clickedSlotIndex = gridSlots.IndexOf(clickedSlot);
             if (clickedSlotIndex == -1)
             {
                  Debug.LogError("MinigameManager: Could not find clicked slot in gridSlots list!", this);
                  return;
             }

             // Only process click if game is not already won
             if (clicksMade < targetClickCount)
             {
                Debug.Log($"MinigameManager: Barcode clicked in slot: {clickedSlot.gameObject.name} (Index: {clickedSlotIndex})");

                clickedSlot.ClearSprite();
                barcodesCurrentlyVisible--;
                clicksMade++;

                lastClickedSlotIndex = clickedSlotIndex;
                Debug.Log($"MinigameManager: Last clicked slot index set to: {lastClickedSlotIndex}");

                 if (!availableSlotIndices.Contains(clickedSlotIndex))
                 {
                      availableSlotIndices.Add(clickedSlotIndex);
                      Debug.Log($"MinigameManager: Slot {clickedSlotIndex} made available.");
                 }
                 else
                 {
                      Debug.LogWarning($"MinigameManager: Slot {clickedSlotIndex} was already in the available list after clicking?", this);
                 }

                Debug.Log($"MinigameManager: Clicks Made: {clicksMade} / {targetClickCount}. Visible barcodes: {barcodesCurrentlyVisible}. Available slots: {availableSlotIndices.Count}");

                // --- Check Win Condition ---
                // Check if this click resulted in reaching the target count
                if (clicksMade == targetClickCount) // Trigger win only at the exact count
                {
                    Debug.Log("MinigameManager: Target clicks reached exactly.");
                    CheckWinCondition(); // Handle win logic
                }
                // --- Check if we need to spawn more barcodes ---
                // Spawn if:
                // 1. There are still clicks remaining (clicksMade < targetClickCount)
                // 2. We haven't reached the max visible barcodes yet (barcodesCurrentlyVisible < maxVisibleBarcodes)
                // 3. There are enough *remaining items* to justify spawning another barcode
                //    (i.e., the number of *unclicked* items is > the number of currently visible barcodes)
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
                           Debug.Log("MinigameManager: Spawning another barcode...");
                           SpawnBarcode(); // Spawn another barcode
                      }
                      else
                      {
                           Debug.LogWarning("MinigameManager: Cannot spawn barcode - not enough suitable empty slots available to maintain max visible count.");
                      }
                }
                 else
                 {
                      Debug.Log("MinigameManager: Not spawning barcode (Conditions not met).");
                 }
             }
             else
             {
                  Debug.LogWarning($"MinigameManager: Barcode clicked in slot {clickedSlotIndex}, but clicksMade ({clicksMade}) is already >= targetClickCount ({targetClickCount}). Click ignored.");
             }
        }

        /// <summary>
        /// Handles the actions when the minigame is won (target clicks are made).
        /// </summary>
        private void CheckWinCondition()
        {
            // This method should only be called when clicksMade == targetClickCount (or >= depending on exact win logic)
             if (clicksMade >= targetClickCount)
            {
                Debug.Log($"MinigameManager: Minigame WON! Clicks Made: {clicksMade}. Target: {targetClickCount}.");

                // Ensure all barcodes are cleared visually if any are left (might happen with complex logic)
                ClearGrid(); // Clear any remaining visible barcodes and reset slots


                // --- Calculate Total Payment ---
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
                             Debug.LogWarning($"MinigameManager: ItemDetails is null for an item in the purchase list. Cannot calculate price for this item.", this);
                         }
                     }
                 }
                 Debug.Log($"MinigameManager: Calculated total payment: {totalPayment}");
                // -----------------------------

                // --- Process Payment (Add to Player's Currency) ---
                 if (EconomyManager.Instance != null)
                 {
                     EconomyManager.Instance.AddCurrency(totalPayment);
                 }
                 else
                 {
                      Debug.LogError("MinigameManager: EconomyManager Instance is null! Cannot process payment.", this);
                 }
                // --------------------------------------------------


                // --- Notify the Initiating Register ---
                 if (initiatingRegister != null)
                 {
                      initiatingRegister.OnMinigameCompleted(totalPayment); // Call the completion method on the register
                 }
                 else
                 {
                      Debug.LogError("MinigameManager: Initiating register reference is null! Cannot notify completion.", this);
                 }
                // --------------------------------------


                // Tell the MenuManager to exit the minigame state and return to Playing
                // This also calls ResetMinigame via the exit action
                if (MenuManager.Instance != null)
                {
                    MenuManager.Instance.SetState(MenuManager.GameState.Playing, null); // Exit minigame state
                }
                else Debug.LogError("MinigameManager: MenuManager Instance is null! Cannot exit minigame state.");
            }
        }


        // TODO: Implement failure conditions if necessary (e.g., clicking too many times, time runs out)
    }
}