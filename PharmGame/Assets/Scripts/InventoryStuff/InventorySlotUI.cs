using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Systems.Inventory; // Needed for Inventory reference
using UnityEngine.EventSystems; // Needed for IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IDragHandler, IPointerUpHandler

namespace Systems.Inventory
{
    // Add IPointerEnterHandler and IPointerExitHandler interfaces
    public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("The index of this slot in the inventory's data array (0-based).")]
        [HideInInspector] public int SlotIndex;

        [Tooltip("The Image component for displaying the item icon.")]
        [SerializeField] private Image itemIcon;

        [Tooltip("The Text or TextMeshPro component for displaying the quantity.")]
        [SerializeField] private TextMeshProUGUI quantityText;

        [Tooltip("The UI element used for highlighting this slot.")]
        [SerializeField] private GameObject highlightElement;

        [HideInInspector] public Inventory ParentInventory;

        // --- ADD FIELDS TO TRACK HIGHLIGHT STATE ---
        private bool isSelected = false; // True if this slot is selected by the InventorySelector
        private bool isHovered = false; // True if the mouse pointer is currently over this slot
        // ------------------------------------------


        private void Awake()
        {
             if (highlightElement != null)
             {
                 highlightElement.SetActive(false); // Ensure highlight is off by default
             }
             else
             {
                  Debug.LogWarning($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Highlight Element is not assigned. Selection/Hover highlight will not be visible.", this);
             }

             // Ensure state flags are false initially
             isSelected = false;
             isHovered = false;
        }

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

        // --- METHODS CALLED BY InventorySelector (FOR SELECTION HIGHLIGHT) ---
        /// <summary>
        /// Applies the visual highlight for selection.
        /// </summary>
        public void ApplySelectionHighlight()
        {
             isSelected = true;
             UpdateHighlightVisual(); // Update the visual based on combined state
             // Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Selection Highlight Applied.");
        }

        /// <summary>
        /// Removes the visual highlight for selection.
        /// </summary>
        public void RemoveSelectionHighlight()
        {
             isSelected = false;
             UpdateHighlightVisual(); // Update the visual based on combined state
             // Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Selection Highlight Removed.");
        }
        // -------------------------------------------------------------------


        // --- METHODS CALLED BY POINTER EVENTS (FOR HOVER HIGHLIGHT) ---
        /// <summary>
        /// Applies the visual highlight for hover.
        /// </summary>
        public void ApplyHoverHighlight()
        {
             isHovered = true;
             UpdateHighlightVisual(); // Update the visual based on combined state
             // Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Hover Highlight Applied.");
        }

        /// <summary>
        /// Removes the visual highlight for hover.
        /// </summary>
        public void RemoveHoverHighlight()
        {
             isHovered = false;
             UpdateHighlightVisual(); // Update the visual based on combined state
             // Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Hover Highlight Removed.");
        }
        // ------------------------------------------------------------


        /// <summary>
        /// Updates the visual highlight element's active state based on isSelected and isHovered flags.
        /// </summary>
        private void UpdateHighlightVisual()
        {
             if (highlightElement != null)
             {
                 // The highlight is active if EITHER selected OR hovered
                 highlightElement.SetActive(isSelected || isHovered);
             }
        }


        // --- Pointer Event Handlers (remain for Drag & Drop) ---
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (DragAndDropManager.Instance != null)
                {
                    DragAndDropManager.Instance.StartDrag(this, eventData);
                }
                else Debug.LogError("InventorySlotUI: DragAndDropManager Instance is null!", this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
             if (eventData.button == PointerEventData.InputButton.Left)
            {
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
                 if (DragAndDropManager.Instance != null)
                 {
                     DragAndDropManager.Instance.EndDrag(eventData);
                 }
                 else Debug.LogError("InventorySlotUI: DragAndDropManager Instance is null!", this);
            }
        }


        // --- ADD POINTER ENTER AND EXIT HANDLERS FOR HOVER ---
        public void OnPointerEnter(PointerEventData eventData)
        {
             // Only apply hover highlight if the game state is InInventory
             if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.InInventory)
             {
                  ApplyHoverHighlight();
             }
             // Optional: Handle hover effects in other states if needed, but the requirement is for InInventory
        }

        public void OnPointerExit(PointerEventData eventData)
        {
             // Only remove hover highlight if the game state is InInventory
             if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.InInventory)
             {
                 RemoveHoverHighlight();
             }
             // Optional: Ensure hover is removed even if state changes while hovering (handled by state exit action)
        }
        // --------------------------------------------------

        // Removed obsolete Highlight() and Unhighlight() methods.
    }
}