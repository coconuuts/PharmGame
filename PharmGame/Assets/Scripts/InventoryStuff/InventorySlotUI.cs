using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Systems.Inventory;
using UnityEngine.EventSystems;

namespace Systems.Inventory
{
    /// <summary>
    /// Represents a single visual inventory slot in the UI and handles user input events.
    /// </summary>
    // KEEP these interfaces. We will use OnPointerDown, OnDrag, and OnPointerUp.
    // We REMOVE IEndDragHandler implementation from the class declaration line if it was there explicitly.
    public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler // Removed IEndDragHandler if it was listed
    {
        [Tooltip("The index of this slot in the inventory's data array (0-based).")]
        [HideInInspector] public int SlotIndex;

        [Tooltip("The Image component for displaying the item icon.")]
        [SerializeField] private Image itemIcon;

        [Tooltip("The Text or TextMeshPro component for displaying the quantity.")]
        [SerializeField] private TextMeshProUGUI quantityText; // Change to Text if not using TextMeshPro

        [Tooltip("The UI element used for highlighting this slot when selected.")]
        [SerializeField] private GameObject highlightElement;

        // Reference back to the main Inventory this slot belongs to
        [HideInInspector] public Inventory ParentInventory;

        private void Awake() // Added Awake to ensure highlight element is off initially
        {
             if (highlightElement != null)
             {
                 highlightElement.SetActive(false); // Ensure highlight is off by default
             }
             else
             {
                  Debug.LogWarning($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Highlight Element is not assigned. Selection highlight will not be visible.", this);
             }
        }
        
        // Method to update the visual representation of the slot
        public void SetItem(Item item)
        {
            // ... (SetItem method remains exactly as in your latest code)
            if (item == null)
            {
                itemIcon.sprite = null;
                itemIcon.enabled = false;
                quantityText.text = "";
                quantityText.enabled = false;
            }
            else
            {
                if (item.details != null && item.details.Icon != null)
                {
                    itemIcon.sprite = item.details.Icon;
                    itemIcon.enabled = true;
                }
                else
                {
                    itemIcon.sprite = null;
                    itemIcon.enabled = false;
                }

                if (item.details != null && item.details.maxStack > 1 && item.quantity > 1)
                {
                     quantityText.text = item.quantity.ToString();
                     quantityText.enabled = true;
                }
                else
                {
                     quantityText.text = "";
                     quantityText.enabled = false;
                }
            }
        }

        /// <summary>
        /// Activates the visual highlight for this slot.
        /// </summary>
        public void Highlight()
        {
             if (highlightElement != null)
             {
                 highlightElement.SetActive(true);
                 // Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Highlighted.");
             }
        }

        /// <summary>
        /// Deactivates the visual highlight for this slot.
        /// </summary>
        public void Unhighlight()
        {
             if (highlightElement != null)
             {
                 highlightElement.SetActive(false);
                 // Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Unhighlighted.");
             }
        }

        // --- Implement Event Handlers ---

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (DragAndDropManager.Instance != null)
                {
                    // Start the drag process when the button is pressed down
                    DragAndDropManager.Instance.StartDrag(this, eventData);
                }
                else
                {
                    Debug.LogError("InventorySlotUI: DragAndDropManager Instance is null!", this);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
             if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Continue the drag process (update ghost position)
                 if (DragAndDropManager.Instance != null)
                 {
                     DragAndDropManager.Instance.Drag(eventData);
                 }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
             if (eventData.button == PointerEventData.InputButton.Left)
            {
                // --- CALL EndDrag logic here on mouse button release ---
                // This ensures the drop logic/cleanup runs even on a fast click where OnEndDrag might not fire.
                 if (DragAndDropManager.Instance != null)
                 {
                     DragAndDropManager.Instance.EndDrag(eventData);
                 }
                 else
                 {
                     Debug.LogError("InventorySlotUI: DragAndDropManager Instance is null!", this);
                 }
            }
        }

        // Optional: Implement this if you want to control when a drag actually begins (e.g., after moving a certain distance)
        // public void OnInitializePotentialDrag(PointerEventData eventData)
        // {
        //      // Tell the EventSystem which button can start this drag
        //      eventData.button = PointerEventData.InputButton.Left;
        // }
    }
}