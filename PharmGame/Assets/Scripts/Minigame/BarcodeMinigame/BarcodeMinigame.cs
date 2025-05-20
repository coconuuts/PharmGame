// Systems/Minigame/BarcodeMinigame.cs
using UnityEngine;
using System; // Needed for Action event and Tuple
using System.Collections.Generic;
using Systems.Interaction; // Needed for CashRegisterInteractable and InteractionResponse types
using System.Linq; // Needed for Linq methods
using Systems.Inventory; // Needed for ItemDetails
using Systems.Economy; // Needed for EconomyManager
// MinigameManager is in the same namespace, no using needed for it.
// MinigameCompletionProcessor is nested in MinigameManager for now, access via MinigameManager.MinigameCompletionProcessor

namespace Systems.Minigame
{
    /// <summary>
    /// Implements the specific logic for the Barcode Scanning Minigame.
    /// This script is a component attached to a minigame GameObject.
    /// Implements the IMinigame interface.
    /// </summary>
    public class BarcodeMinigame : MonoBehaviour, IMinigame
    {
        [Header("Camera Settings for Minigame Manager")] // ADDED
        [Tooltip("The Transform the camera should move to when this minigame starts.")] // ADDED
        [SerializeField] private Transform initialCameraTarget; // ADDED
        [Tooltip("The duration for the initial camera movement.")] // ADDED
        [SerializeField] private float initialCameraDuration = 1.0f; // ADDED


        [Header("Grid References")]
        [Tooltip("Drag all the BarcodeSlot GameObjects from the UI grid here.")]
        [SerializeField] private List<BarcodeSlot> gridSlots; // Assumes BarcodeSlot script exists with SetBarcodeMinigame, ClearSprite, SetSprite, IsEmpty

        [Header("Barcode Settings")]
        [Tooltip("A list of possible sprites to use for the barcodes.")]
        [SerializeField] private List<Sprite> possibleBarcodeSprites;

        [Tooltip("The maximum number of barcodes visible on the grid at any given time.")]
        [SerializeField] private int maxVisibleBarcodes = 3;


        // IMinigame Properties Implementation
        public Transform InitialCameraTarget => initialCameraTarget; // ADDED
        public float InitialCameraDuration => initialCameraDuration; // ADDED


        // Fields to store customer's purchase and the initiating register
        private List<(ItemDetails details, int quantity)> currentItemsToScan;
        private CashRegisterInteractable initiatingRegister;

        private int targetClickCount;
        private int clicksMade;
        private int barcodesCurrentlyVisible;

        private List<int> availableSlotIndices;

        private int lastClickedSlotIndex = -1;

        // --- ADDED: Fields to store final data for the event ---
        private bool minigameCompletedSuccessfully = false; // Tracks if the win condition was met
        private object finalCompletionData = null; // Stores data like calculated payment
        // ------------------------------------------------------

        // IMinigame Completion Event
        /// <summary>
        /// Event triggered when the Barcode Minigame session ends (completed or aborted).
        /// --- MODIFIED: Passes a boolean indicating success (true) or failure/abort (false). ---
        /// --- MODIFIED: Passes final data (e.g., payment) if successful ---
        /// </summary>
        /// <remarks>
        /// The object parameter should be a Tuple: (bool success, object data).
        /// The 'data' part is only relevant if success is true (e.g., payment amount).
        /// The MinigameManager subscribes to this event.
        /// </remarks>
        public event Action<object> OnMinigameCompleted;


        private void Awake()
        {
            Debug.Log("BarcodeMinigame: Awake completed.");

            availableSlotIndices = new List<int>();
            if (gridSlots != null)
            {
                for (int i = 0; i < gridSlots.Count; i++)
                {
                    if (gridSlots[i] != null)
                    {
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

            currentItemsToScan = new List<(ItemDetails details, int quantity)>();
             // Deactivate UI root initially (handled by MinigameManager config)
        }

        private void OnDestroy()
        {
            // Clean up event listeners potentially held by slots?
            // BarcodeSlot.SetBarcodeMinigame(null) could be called during cleanup
            // to remove the reference from slots if needed.
        }


        /// <summary>
        /// Implements IMinigame.SetupAndStart. Sets up and starts the barcode minigame.
        /// Expects data to be a StartMinigameResponse.
        /// --- MODIFIED: Expects StartMinigameResponse directly now ---
        /// </summary>
        /// <param name="data">Should be a StartMinigameResponse.</param>
        public void SetupAndStart(object data) // Implements IMinigame
        {
             Debug.Log("BarcodeMinigame: SetupAndStart called.");

             // --- Cast the incoming data (now expecting StartMinigameResponse) ---
             if (data is StartMinigameResponse startData)
             {
                 // Get data needed for minigame logic from the response
                 List<(ItemDetails details, int quantity)> itemsToScan = startData.ItemsToScan;
                 CashRegisterInteractable register = startData.CashRegisterInteractable; // Use the property from the response

                 // Validation checks
                 if (gridSlots == null || gridSlots.Count == 0) { Debug.LogError("BarcodeMinigame: Cannot start - gridSlots list is empty or null."); SignalMinigameFinished(false); return; }
                 if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0) { Debug.LogError("BarcodeMinigame: Cannot start - possibleBarcodeSprites list is empty or null."); SignalMinigameFinished(false); return; }
                 if (itemsToScan == null || itemsToScan.Count == 0) { Debug.LogWarning("BarcodeMinigame: Cannot start - itemsToScan list is null or empty."); SignalMinigameFinished(false); return; } // Consider this a failure if no items
                 if (register == null) { Debug.LogError("BarcodeMinigame: Cannot start - initiating register is null."); SignalMinigameFinished(false); return; }

                 // Initialize game state
                currentItemsToScan = new List<(ItemDetails details, int quantity)>(itemsToScan); // Store a copy
                initiatingRegister = register;
                targetClickCount = currentItemsToScan.Sum(item => item.quantity);
                clicksMade = 0;
                barcodesCurrentlyVisible = 0;
                lastClickedSlotIndex = -1; // Reset last clicked slot index

                minigameCompletedSuccessfully = false; // Initialize completion status
                finalCompletionData = null; // Clear any previous data

                Debug.Log($"BarcodeMinigame: Starting minigame with target clicks (total quantity): {targetClickCount}. Items to scan: {currentItemsToScan.Count} distinct types.");

                ClearGrid(); // Clear any existing barcodes and prepare slots

                // Spawn the initial set of barcodes (up to maxVisibleBarcodes or targetCount if less)
                int initialBarcodesToSpawn = Mathf.Min(maxVisibleBarcodes, targetClickCount);
                Debug.Log($"BarcodeMinigame: Spawning initial {initialBarcodesToSpawn} barcodes.");
                for (int i = 0; i < initialBarcodesToSpawn; i++)
                {
                    SpawnBarcode(); // Spawn a barcode in a random empty slot
                }
                 // If for some reason 0 barcodes are spawned but target is > 0 (e.g., no sprites),
                 // this could lead to being stuck. Add a check?
                 if (targetClickCount > 0 && barcodesCurrentlyVisible == 0)
                 {
                      Debug.LogError("BarcodeMinigame: Target clicks > 0 but no barcodes spawned. Aborting minigame.", this);
                      SignalMinigameFinished(false); // Signal failure if we can't even start properly
                 }
             }
             else
             {
                 Debug.LogError($"BarcodeMinigame: Received incorrect data type for SetupAndStart. Expected StartMinigameResponse.", this);
                 SignalMinigameFinished(false); // Signal failure due to incorrect data
             }
        }

        /// <summary>
        /// Implements IMinigame.End. Performs final cleanup for the barcode minigame.
        /// Called by MinigameManager when the session ends (naturally or aborted).
        /// --- MODIFIED: Accepts wasAborted parameter and invokes event ---
        /// </summary>
        public void End(bool wasAborted) // Implements IMinigame
        {
             Debug.Log($"BarcodeMinigame: End called. Aborted: {wasAborted}.");

             // --- Invoke the completion event, passing the final status and data ---
             // If aborted, the status is always false. Otherwise, use the internally tracked status.
             bool finalStatus = wasAborted ? false : minigameCompletedSuccessfully;
             // Pass the final completion data if successful, otherwise null.
             object dataToPass = finalStatus ? finalCompletionData : null;

             Debug.Log($"BarcodeMinigame: Invoking OnMinigameCompleted event. Status: {finalStatus}, Data: {dataToPass}.");
             // The event needs to pass a single object. Let's pass a Tuple.
             // Tuple<(bool success, object data)>
             OnMinigameCompleted?.Invoke(Tuple.Create(finalStatus, dataToPass)); // Requires 'using System;'
             // -----------------------------------------------------------------

             // Perform cleanup (resetting the minigame state and grid)
             Reset(); // Reset the internal state and visuals
             // If you had specific visuals or states to clean up *before* deactivating the GameObject, do it here.
        }

         /// <summary>
         /// Resets the internal state of the minigame and clears the grid visuals.
         /// Called by End().
         /// </summary>
        private void Reset()
        {
            Debug.Log("BarcodeMinigame: Resetting internal state.");
            targetClickCount = 0;
            clicksMade = 0;
            barcodesCurrentlyVisible = 0;
            lastClickedSlotIndex = -1;

            if (currentItemsToScan != null) currentItemsToScan.Clear();
            initiatingRegister = null;

             minigameCompletedSuccessfully = false; // Reset completion status
             finalCompletionData = null; // Clear final data

            ClearGrid(); // Clears sprites and resets available slots
        }

        /// <summary>
        /// Utility method to signal that the minigame logic has finished, either successfully or due to an internal error.
        /// Calls End() to handle final cleanup and event invocation.
        /// --- ADDED method ---
        /// </summary>
        /// <param name="success">True if the minigame was completed successfully, false if it failed internally.</param>
        /// <param name="finalData">Optional data to pass along with a successful completion (e.g., payment).</param>
        private void SignalMinigameFinished(bool success, object finalData = null)
        {
             Debug.Log($"BarcodeMinigame: SignalMinigameFinished called. Success: {success}.");
             minigameCompletedSuccessfully = success; // Store the outcome
             finalCompletionData = finalData; // Store optional data

             // Call End. Pass 'false' for wasAborted because this is an internal signal, not an external abort.
             End(false);
        }


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
                 SignalMinigameFinished(false); // Signal failure if we can't spawn due to missing sprites
                return;
            }

            int randomIndexInSpawnableList = UnityEngine.Random.Range(0, spawnableSlotIndices.Count);
            int slotIndexToUse = spawnableSlotIndices[randomIndexInSpawnableList];


            availableSlotIndices.Remove(slotIndexToUse);

            BarcodeSlot slotToSpawnIn = gridSlots[slotIndexToUse];
            Sprite randomSprite = possibleBarcodeSprites[UnityEngine.Random.Range(0, possibleBarcodeSprites.Count)];

            if (slotToSpawnIn != null)
            {
                slotToSpawnIn.SetSprite(randomSprite);
                barcodesCurrentlyVisible++;
                Debug.Log($"BarcodeMinigame: Spawned barcode in slot {slotIndexToUse}. Visible: {barcodesCurrentlyVisible}. Available slots: {availableSlotIndices.Count}. Clicks made: {clicksMade}/{targetClickCount}");
            }
            else
            {
                Debug.LogError($"BarcodeMinigame: BarcodeSlot at index {slotIndexToUse} is null! Cannot set sprite.", this);
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
                 Debug.LogWarning("BarcodeMinigame: BarcodeClicked called with null or empty slot.");
                 return;
             }

             // Ensure we don't process clicks after the game is won
             if (clicksMade >= targetClickCount)
             {
                  Debug.LogWarning($"BarcodeMinigame: Barcode clicked after game completion. Clicks made ({clicksMade}) >= target ({targetClickCount}). Click ignored.");
                  return;
             }


             int clickedSlotIndex = gridSlots.IndexOf(clickedSlot);
             if (clickedSlotIndex == -1)
             {
                  Debug.LogError("BarcodeMinigame: Could not find clicked slot in gridSlots list!", this);
                  return;
             }

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

            // Check Win Condition
            if (clicksMade >= targetClickCount)
            {
                CheckWinCondition(); // Handle win logic
            }
            // Check if we need to spawn more barcodes (only if not won yet)
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


        /// <summary>
        /// Handles the actions when the minigame is won (target clicks are made).
        /// --- MODIFIED: Calls SignalMinigameFinished instead of invoking event directly ---
        /// </summary>
        private void CheckWinCondition()
        {
            // This method should only be called when clicksMade >= targetClickCount
            if (clicksMade >= targetClickCount)
            {
                Debug.Log($"BarcodeMinigame: Minigame logic WIN completed! Clicks Made: {clicksMade}. Target: {targetClickCount}.");

                // Ensure all barcodes are cleared visually if any are left
                ClearGrid();

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

                // --- MODIFIED: Store success status and data, then call SignalMinigameFinished ---
                // We need to pass the payment amount AND the initiating register back to the processor.
                // The processor is assumed to live elsewhere (e.g., a static method in MinigameManager or a dedicated class).
                // Let's pass a Tuple<float, CashRegisterInteractable> as the data payload.
                SignalMinigameFinished(true, Tuple.Create(totalPayment, initiatingRegister));
                // ------------------------------------------------------------------------------
            }
        }
    }
}