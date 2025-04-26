using UnityEngine;
using Systems.Inventory;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

namespace Systems.Inventory
{
    /// <summary>
    /// Manages the drag and drop interactions for all inventories.
    /// </summary>
    public class DragAndDropManager : MonoBehaviour
    {
        // Singleton instance
        public static DragAndDropManager Instance { get; private set; }

        [Tooltip("The UI Image element used to visualize the item being dragged.")]
        [SerializeField] private Image ghostItemImage; // Assign your ghost visual UI Image here

        [Tooltip("The Text or TextMeshPro component for the quantity on the ghost visual.")]
        [SerializeField] private TextMeshProUGUI ghostQuantityText; // ADD THIS FIELD (Change to Text if not using TextMeshPro)

        // --- NEW: Flag to indicate if a drag operation is currently in progress ---
        public bool IsDragging { get; private set; } = false;

        // --- NEW: Event broadcast when the drag state changes ---
        public event Action<bool> OnDragStateChanged;


        private Item itemBeingDragged;
        private Inventory sourceInventory; // The inventory the item came from
        private int sourceSlotIndex; // The original slot index in the source inventory

        public event Action OnDragDropCompleted;

        // List of all active inventories in the scene (inventories register themselves)
        private static List<Inventory> allInventories = new List<Inventory>();

        public static void RegisterInventory(Inventory inventory)
        {
             if (!allInventories.Contains(inventory))
             {
                 allInventories.Add(inventory);
             }
        }

        public static void UnregisterInventory(Inventory inventory)
        {
            if (allInventories.Contains(inventory))
            {
                allInventories.Remove(inventory);
            }
        }


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // If the manager should persist between scenes
            }
            else
            {
                Debug.LogWarning("DragAndDropManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Ensure the ghost item visual is initially hidden
            if (ghostItemImage != null)
            {
                ghostItemImage.gameObject.SetActive(false);
                ghostItemImage.raycastTarget = false; // Disable raycasting so it doesn't block clicks
            }
            else
            {
                 Debug.LogError("DragAndDropManager: Ghost Item Image is not assigned!", this);
                 enabled = false;
                 return;
            }

             // Ensure the ghost quantity text is initially hidden
             if (ghostQuantityText != null)
             {
                 ghostQuantityText.gameObject.SetActive(false);
             }
             else
             {
                  Debug.LogWarning("DragAndDropManager: Ghost Quantity Text is not assigned. Quantity will not be displayed during drag.", this);
             }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // --- NEW: Ensure state is reset and event is potentially invoked on destruction ---
            if (IsDragging)
            {
                IsDragging = false;
                OnDragStateChanged?.Invoke(false);
            }
             // -----------------------------------------------------------------------------
        }


        /// <summary>
        /// Called by an InventorySlotUI when a pointer is pressed down.
        /// Initiates a drag if an item is present.
        /// </summary>
        public void StartDrag(InventorySlotUI slotUI, PointerEventData eventData)
        {
            if (itemBeingDragged != null) return; // Prevent starting a new drag if one is already active

            if (slotUI.ParentInventory == null || slotUI.SlotIndex < 0 || slotUI.SlotIndex >= slotUI.ParentInventory.Combiner.PhysicalSlotCount)
            {
                 Debug.LogError($"DragAndDropManager: StartDrag called with invalid slotUI parent inventory or index ({slotUI.SlotIndex}).", slotUI.gameObject);
                 return;
            }

            ObservableArray<Item> sourceObservableArray = slotUI.ParentInventory.InventoryState;

            if (sourceObservableArray == null)
            {
                 Debug.LogError($"DragAndDropManager: Source inventory {slotUI.ParentInventory.Id} has a null ObservableArray.", slotUI.ParentInventory.gameObject);
                 return;
            }

            Item itemInSlot = sourceObservableArray[slotUI.SlotIndex];

            if (itemInSlot != null)
            {
                // --- Start the drag ---
                itemBeingDragged = itemInSlot;
                sourceInventory = slotUI.ParentInventory;
                sourceSlotIndex = slotUI.SlotIndex;

                // --- NEW: Set dragging flag and broadcast event ---
                IsDragging = true;
                OnDragStateChanged?.Invoke(true);
                Debug.Log("DragAndDropManager: Drag started. Broadcasted OnDragStateChanged(true).");
                // -------------------------------------------------

                // Visually represent the item being dragged
                if (ghostItemImage != null && itemBeingDragged.details != null && itemBeingDragged.details.Icon != null)
                {
                    ghostItemImage.sprite = itemBeingDragged.details.Icon;
                    ghostItemImage.transform.position = eventData.position;
                    ghostItemImage.gameObject.SetActive(true);

                     // --- Update Ghost Quantity Text ---
                     if (ghostQuantityText != null)
                     {
                          // Only show quantity if maxStack > 1 and quantity > 1
                          if (itemBeingDragged.details.maxStack > 1 && itemBeingDragged.quantity > 1)
                          {
                              ghostQuantityText.text = itemBeingDragged.quantity.ToString();
                              ghostQuantityText.gameObject.SetActive(true);
                          }
                          else
                          {
                              // Hide quantity text if not applicable
                              ghostQuantityText.text = ""; // Clear text
                              ghostQuantityText.gameObject.SetActive(false);
                          }
                     }

                     // --- CONCEPTUAL MOVE TO SOURCE GHOST SLOT & CLEAR ORIGINAL SLOT ---
                     // Clear the original slot in the source inventory's data
                     sourceObservableArray.SetItemAtIndex(null, sourceSlotIndex);
                     // Move the item instance to the source inventory's ghost data slot
                      sourceObservableArray.SetItemAtIndex(itemBeingDragged, sourceObservableArray.Length - 1); // Length-1 is the ghost slot index

                }
                else
                {
                    Debug.LogError("DragAndDropManager: Ghost item visual setup failed (Image or ItemDetails/Icon missing).", this);
                    // --- NEW: Reset drag state if visual setup fails ---
                    itemBeingDragged = null;
                    sourceInventory = null;
                    sourceSlotIndex = -1;
                    IsDragging = false; // Reset flag immediately
                    OnDragStateChanged?.Invoke(false); // Broadcast state change
                    // -----------------------------------------------------
                }
            }
            else
            {
                Debug.Log($"DragAndDropManager: Clicked on empty slot {slotUI.SlotIndex}. No drag started.");
            }
        }

        /// <summary>
        /// Called by an InventorySlotUI while the pointer is being dragged.
        /// Updates the position of the ghost visual.
        /// </summary>
        public void Drag(PointerEventData eventData)
        {
             if (itemBeingDragged != null && ghostItemImage != null)
            {
                 ghostItemImage.transform.position = eventData.position;
                  // Keep the quantity text in sync with the image's position
                  if(ghostQuantityText != null)
                  {
                       ghostQuantityText.transform.position = eventData.position; // Assumes they are siblings or parented correctly
                  }
            }
        }

        /// <summary>
        /// Called by an InventorySlotUI when the pointer is released.
        /// Handles the drop logic.
        /// </summary>
        public void EndDrag(PointerEventData eventData)
        {
            if (itemBeingDragged == null) return;

            // Hide the ghost visual immediately
            if (ghostItemImage != null) ghostItemImage.gameObject.SetActive(false);
             if (ghostQuantityText != null) ghostQuantityText.gameObject.SetActive(false); // Hide quantity too

            // --- Determine the Drop Target ---
            InventorySlotUI targetSlotUI = FindTargetSlot(eventData.position);

            if (targetSlotUI != null && targetSlotUI.ParentInventory != null)
            {
                Inventory targetInventory = targetSlotUI.ParentInventory;
                int targetSlotIndex = targetSlotUI.SlotIndex;

                // Ensure target index is within the physical slot range of the target inventory
                // Combiner's PhysicalSlotCount is needed here. We have access via targetInventory.Combiner.PhysicalSlotCount
                if (targetSlotIndex >= 0 && targetSlotIndex < targetInventory.Combiner.PhysicalSlotCount)
                {
                    ObservableArray<Item> targetObservableArray = targetInventory.InventoryState;

                     if (targetObservableArray != null)
                     {
                         // --- Call the ObservableArray's HandleDrop method ---
                         // Pass the source array and original index
                         targetObservableArray.HandleDrop(itemBeingDragged, targetSlotIndex, sourceInventory.InventoryState, sourceSlotIndex);
                     }
                     else
                     {
                         Debug.LogError($"DragAndDropManager: Target inventory {targetInventory.Id} has a null ObservableArray. Cannot handle drop.", targetInventory.gameObject);
                         // Item remains in source ghost slot, will be returned to source original slot below
                         ReturnItemToSource();
                     }
                }
                else
                {
                     // Dropped over a valid inventory's UI element, but not a physical slot index (e.g., border, ghost slot, filler)
                     Debug.Log($"DragAndDropManager: Drop detected over target inventory '{targetInventory.Id}' but outside valid physical slots ({targetInventory.Combiner.PhysicalSlotCount}). Target Index: {targetSlotIndex}. Returning item to source.");
                     ReturnItemToSource();
                }
            }
            else
            {
                // --- No Valid Drop Target Found (Dropped outside any inventory slots) ---
                ReturnItemToSource();
            }

            OnDragDropCompleted?.Invoke();
            Debug.Log($"DragAndDropManager: Drag/Drop transaction completed. Triggering OnDragDropCompleted event.");

            // --- NEW: Reset dragging flag and broadcast event AFTER drop logic ---
            IsDragging = false;
            OnDragStateChanged?.Invoke(false);
            Debug.Log("DragAndDropManager: Drag ended. Broadcasted OnDragStateChanged(false).");
            // -------------------------------------------------------------------

            // --- Clean up drag state ---
            itemBeingDragged = null;
            sourceInventory = null;
            sourceSlotIndex = -1;
        }


        // Helper method to find the InventorySlotUI the pointer is currently over.
        private InventorySlotUI FindTargetSlot(Vector3 screenPosition)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = screenPosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                InventorySlotUI slotUI = result.gameObject.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    // Ensure the hit slot belongs to a valid inventory
                     if (slotUI.ParentInventory != null)
                     {
                          // --- IMPORTANT: Ignore raycasts hitting the ghost item itself ---
                         // The ghost image has raycastTarget = false, but defensive check is good.
                         if (result.gameObject == ghostItemImage.gameObject) continue;

                          return slotUI; // Found the target slot
                     }
                     else
                     {
                          Debug.LogWarning($"DragAndDropManager: Raycast hit InventorySlotUI on {result.gameObject.name} but it has no ParentInventory assigned.", result.gameObject);
                     }
                }
                 else
                 {
                      // Optional: Log other hits for debugging raycast setup
                      // Debug.Log($"DragAndDropManager: Raycast hit {result.gameObject.name} (not a slot).", result.gameObject);
                 }
            }

             // Debug.Log("DragAndDropManager: Raycast did not hit any valid InventorySlotUI with a parent inventory."); // Commented out to reduce spam
            return null; // No valid InventorySlotUI found at the drop position
        }


        /// <summary>
        /// Returns the item being dragged back to its original source slot.
        /// Called when the drop is invalid or the HandleDrop logic dictates.
        /// </summary>
        private void ReturnItemToSource()
        {
            if (itemBeingDragged != null && sourceInventory != null && sourceSlotIndex != -1)
            {
                ObservableArray<Item> sourceObservableArray = sourceInventory.InventoryState;
                if (sourceObservableArray != null)
                {

                     // Call HandleDrop on the *source* array, targeting the original slot.
                     // The HandleDrop method handles the internal logic (putting it back, potentially clearing ghost).
                     sourceObservableArray.HandleDrop(itemBeingDragged, sourceSlotIndex, sourceObservableArray, sourceSlotIndex); // source and target arrays are the same
                }
                else
                {
                    Debug.LogError($"DragAndDropManager: Cannot return item to source. Source inventory {sourceInventory.Id} has null ObservableArray.", sourceInventory.gameObject);
                    // Item is effectively lost unless handled differently (e.g., dropped in world)
                }
            }
             else
             {
                 Debug.LogError("DragAndDropManager: Attempted to return item to source, but drag state was invalid.", this);
             }
             // Note: Drag state (itemBeingDragged, etc.) is cleared in EndDrag after this method returns.
        }
    }
}