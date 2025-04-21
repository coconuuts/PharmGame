using UnityEngine;
using System.Collections.Generic;
using Systems.Interaction;
using System.Linq; // Needed for Linq methods like .ToList() or .Contains()

namespace Systems.Minigame
{
    public class MinigameManager : MonoBehaviour
    {
        // ... (Existing fields remain)
        public static MinigameManager Instance { get; private set; }

        [Header("Grid References")]
        [Tooltip("Drag all the BarcodeSlot GameObjects from the UI grid here.")]
        [SerializeField] private List<BarcodeSlot> gridSlots;

        [Header("Barcode Settings")]
        [Tooltip("A list of possible sprites to use for the barcodes.")]
        [SerializeField] private List<Sprite> possibleBarcodeSprites;

        [Tooltip("The maximum number of barcodes visible on the grid at any given time.")]
        [SerializeField] private int maxVisibleBarcodes = 3;

        private int targetClickCount;
        private int clicksMade;
        private int barcodesCurrentlyVisible;

        private List<int> availableSlotIndices;

        // --- ADDED FIELD TO TRACK LAST CLICKED SLOT ---
        private int lastClickedSlotIndex = -1; // Stores the index of the most recently clicked slot
        // ---------------------------------------------


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

        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartMinigame(int targetClicks)
        {
             if (gridSlots == null || gridSlots.Count == 0) { Debug.LogError("MinigameManager: Cannot start minigame - gridSlots list is empty or null."); return; }
             if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0) { Debug.LogError("MinigameManager: Cannot start minigame - possibleBarcodeSprites list is empty or null."); return; }

            Debug.Log($"MinigameManager: Starting minigame with target clicks: {targetClicks}.");
            targetClickCount = targetClicks;
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

        public void ResetMinigame()
        {
            Debug.Log("MinigameManager: Resetting minigame state.");
            targetClickCount = 0;
            clicksMade = 0;
            barcodesCurrentlyVisible = 0;
            lastClickedSlotIndex = -1; // Reset last clicked slot index
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
             // --- Create a temporary list of available indices, excluding the last clicked slot ---
             List<int> spawnableSlotIndices = new List<int>(availableSlotIndices); // Copy the main list

             if (lastClickedSlotIndex != -1 && spawnableSlotIndices.Contains(lastClickedSlotIndex))
             {
                  // If the last clicked slot index is valid and in the temporary list, remove it
                  spawnableSlotIndices.Remove(lastClickedSlotIndex);
                  Debug.Log($"MinigameManager: Excluding last clicked slot {lastClickedSlotIndex} from spawn options.");
             }
             // -----------------------------------------------------------------------------------


             if (spawnableSlotIndices.Count == 0)
             {
                 Debug.LogWarning("MinigameManager: Cannot spawn barcode - no suitable empty slots available.");
                 return; // No suitable empty slots
             }
            if (possibleBarcodeSprites == null || possibleBarcodeSprites.Count == 0)
            {
                 Debug.LogError("MinigameManager: Cannot spawn barcode - no possible sprites assigned!");
                 return;
            }

             // Select a random index from the TEMPORARY list
            int randomIndex = UnityEngine.Random.Range(0, spawnableSlotIndices.Count);
            int slotIndexToUse = spawnableSlotIndices[randomIndex]; // Get index from temp list


            // Remove the selected index from the MAIN available list
            // This is important so it's no longer considered available for future spawns until cleared
            availableSlotIndices.Remove(slotIndexToUse); // Remove from the main list


            // Get the corresponding BarcodeSlot using the actual index
            BarcodeSlot slotToSpawnIn = gridSlots[slotIndexToUse];

             // Select a random barcode sprite
             Sprite randomSprite = possibleBarcodeSprites[UnityEngine.Random.Range(0, possibleBarcodeSprites.Count)];

             // Set the sprite on the slot
            if (slotToSpawnIn != null)
            {
                slotToSpawnIn.SetSprite(randomSprite);
                barcodesCurrentlyVisible++; // Increment count of visible barcodes
                 Debug.Log($"MinigameManager: Spawned barcode in slot {slotIndexToUse}. Visible: {barcodesCurrentlyVisible}. Available slots: {availableSlotIndices.Count}");
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
             if (clickedSlot == null || clickedSlot.IsEmpty) // Also check if slot is empty (shouldn't happen due to click logic, but safe)
             {
                 Debug.LogWarning("MinigameManager: BarcodeClicked called with null or empty slot.");
                 return;
             }

             // Get the index of the clicked slot BEFORE clearing it
             int clickedSlotIndex = gridSlots.IndexOf(clickedSlot);
             if (clickedSlotIndex == -1)
             {
                  Debug.LogError("MinigameManager: Could not find clicked slot in gridSlots list!", this);
                  return; // Cannot process click if slot not found
             }

             Debug.Log($"MinigameManager: Barcode clicked in slot: {clickedSlot.gameObject.name} (Index: {clickedSlotIndex})");

            // Process the click
            clickedSlot.ClearSprite(); // Make the clicked sprite disappear
            barcodesCurrentlyVisible--; // Decrement the count of visible barcodes
            clicksMade++; // Increment the total clicks made

            // --- Update last clicked slot index ---
            lastClickedSlotIndex = clickedSlotIndex; // Store the index of the slot just clicked
            Debug.Log($"MinigameManager: Last clicked slot index set to: {lastClickedSlotIndex}");
            // -------------------------------------


             // Add the clicked slot's index back to the available list (it's now empty)
             // Ensure it's not already in the list before adding (shouldn't be, but safe)
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
            if (clicksMade >= targetClickCount) // Use >= in case of logic errors, but ideally should be ==
            {
                Debug.Log("MinigameManager: Win condition met or exceeded!");
                // Trigger win logic only if exactly target clicks are made? Or just >= ?
                // Let's use == for a precise win. If clicksMade > targetClickCount, it's a potential failure/overclick.
                if (clicksMade == targetClickCount)
                {
                     CheckWinCondition(); // Handle win logic
                }
                else
                {
                     Debug.LogWarning($"MinigameManager: Clicks Made ({clicksMade}) exceeded target ({targetClickCount}). Potential overclick/failure?", this);
                     // TODO: Handle overclicking/failure state if needed
                     // For now, it just won't trigger the win condition if it's >
                }
            }
            // --- Check if we need to spawn more barcodes (Issue 1 fix) ---
            // Only spawn if:
            // 1. Game is not yet won (clicksMade < targetClickCount)
            // 2. We need more barcodes than are currently visible to reach the target count
            // 3. The number of currently visible barcodes is less than the maximum allowed
            else if (clicksMade < targetClickCount && barcodesCurrentlyVisible < maxVisibleBarcodes && (targetClickCount - clicksMade) > barcodesCurrentlyVisible)
            {
                 Debug.Log("MinigameManager: Spawning another barcode...");
                 SpawnBarcode(); // Spawn another barcode
            }
             else
             {
                  Debug.Log("MinigameManager: Not spawning barcode (Conditions not met).");
             }
        }

        /// <summary>
        /// Handles the actions when the minigame is won.
        /// </summary>
        private void CheckWinCondition()
        {
            // Ensure we only trigger win logic if the exact target clicks are made
            if (clicksMade == targetClickCount)
            {
                Debug.Log("MinigameManager: Minigame WON!");

                // TODO: Trigger success feedback (sound, particles, etc.)

                // Tell the MenuManager to exit the minigame state and return to Playing
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