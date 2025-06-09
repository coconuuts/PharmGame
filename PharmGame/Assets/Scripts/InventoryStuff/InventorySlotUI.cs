using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Systems.Inventory;
using UnityEngine.EventSystems;
using System;
using Systems.GameStates; // Ensure this is included

namespace Systems.Inventory
{
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

        private bool isSelected = false; // True if this slot is selected by the InventorySelector
        private bool isHovered = false; // True if the mouse pointer is currently over this slot


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

        // --- Subscribe to the drag state event ---
        private void OnEnable()
        {
            if (DragAndDropManager.Instance != null)
            {
                DragAndDropManager.Instance.OnDragStateChanged += HandleDragStateChanged;
            }
            // Ensure initial state is correct in case a drag is already happening
            UpdateHighlightVisual();
        }

        // --- Unsubscribe from the drag state event ---
        private void OnDisable()
        {
            if (DragAndDropManager.Instance != null)
            {
                DragAndDropManager.Instance.OnDragStateChanged -= HandleDragStateChanged;
            }
            // Ensure highlight is off when disabled
            if (highlightElement != null)
            {
                highlightElement.SetActive(false);
            }
        }

        /// <summary>
        /// Called when the drag state changes (start or end).
        /// </summary>
        // --- Handler for the drag state changed event ---
        private void HandleDragStateChanged(bool isDragging)
        {
            // When the drag state changes, re-evaluate the highlight visual.
            // The logic inside UpdateHighlightVisual() will handle whether to show it.
            UpdateHighlightVisual();
        }

        public void SetItem(Item item)
        {
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
        public void ApplySelectionHighlight()
        {
            isSelected = true;
            UpdateHighlightVisual(); // Update the visual based on combined state
        }

        public void RemoveSelectionHighlight()
        {
            isSelected = false;
            UpdateHighlightVisual(); // Update the visual based on combined state
        }

        // --- METHODS CALLED BY POINTER EVENTS (FOR HOVER HIGHLIGHT) ---
        public void ApplyHoverHighlight()
        {
            isHovered = true;
            UpdateHighlightVisual(); // Update the visual based on combined state
        }

        public void RemoveHoverHighlight()
        {
            isHovered = false;
            UpdateHighlightVisual(); // Update the visual based on combined state
        }


        /// <summary>
        /// Updates the visual highlight element's active state based on isSelected, isHovered, and drag state.
        /// </summary>
        private void UpdateHighlightVisual()
        {
            if (highlightElement != null)
            {
                // Determine if the highlight *should* be active under normal circumstances (selected or hovered)
                bool shouldBeActiveNormally = isSelected || isHovered;

                // --- Check if a drag operation is in progress ---
                bool dragInProgress = (DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging);

                // The highlight element is active ONLY IF it should be active normally AND NO drag is in progress.
                highlightElement.SetActive(shouldBeActiveNormally && !dragInProgress);
            }
        }


        // --- Pointer Event Handlers (remain for Drag & Drop and Hover) ---
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // DragAndDropManager handles its own state checks and should only start drag in appropriate game states
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
                 else Debug.LogError("InventorySlotUI: DragAndDropManager Instance is null!", this);
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

        // --- POINTER ENTER AND EXIT HANDLERS FOR HOVER ---
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (MenuManager.Instance != null &&
               (MenuManager.Instance.currentState == MenuManager.GameState.InInventory ||
                MenuManager.Instance.currentState == MenuManager.GameState.InCrafting))
            {
                 ApplyHoverHighlight(); // Sets isHovered = true and calls UpdateHighlightVisual
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (MenuManager.Instance != null &&
               (MenuManager.Instance.currentState == MenuManager.GameState.InInventory ||
                MenuManager.Instance.currentState == MenuManager.GameState.InCrafting))
            {
                 RemoveHoverHighlight(); // Sets isHovered = false and calls UpdateHighlightVisual
            }
        }
    }
}