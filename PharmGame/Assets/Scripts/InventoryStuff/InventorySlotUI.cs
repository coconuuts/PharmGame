using UnityEngine;
using UnityEngine.UI;
using TMPro; // If you are using TextMeshPro. Use UnityEngine.UI.Text if not.
using Systems.Inventory; // Needed for the Item class reference
using UnityEngine.EventSystems;

namespace Systems.Inventory
{
    /// <summary>
    /// Represents a single visual inventory slot in the UI and handles user input events.
    /// </summary>
    // ADD THESE INTERFACES
    public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler //, IInitializePotentialDragHandler // Optional if you need to check drag threshold
    {
        [Tooltip("The index of this slot in the inventory's data array (0-based).")]
        // We won't set this manually; the Visualizer will assign it.
        [HideInInspector] public int SlotIndex;

        [Tooltip("The Image component for displaying the item icon.")]
        [SerializeField] private Image itemIcon;

        [Tooltip("The Text or TextMeshPro component for displaying the quantity.")]
        [SerializeField] private TextMeshProUGUI quantityText; // Change to Text if not using TextMeshPro

        // Reference back to the main Inventory this slot belongs to
        // This is crucial for the DragAndDropManager to know the source/target inventory
        [HideInInspector] public Inventory ParentInventory; // ADD THIS FIELD


        // Method to update the visual representation of the slot
        public void SetItem(Item item)
        {
            // --- ADD THIS DEBUG LOG ---
            Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): SetItem called with item: {(item != null ? item.details?.Name ?? "Item (Details null)" : "null")}", this);

            if (item == null)
            {
                // Slot is empty
                itemIcon.sprite = null;
                itemIcon.enabled = false;
                quantityText.text = "";
                quantityText.enabled = false; // Hide quantity text when empty

                // --- ADD THIS DEBUG LOG ---
                Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Slot cleared.", this);
            }
            else
            {
                // Slot has an item
                if (item.details != null && item.details.Icon != null)
                {
                    itemIcon.sprite = item.details.Icon;
                    itemIcon.enabled = true;

                    // --- ADD THIS DEBUG LOG ---
                     Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Setting icon to {item.details.Icon.name}.", this);
                }
                else
                {
                    // Fallback if somehow item details or icon are missing
                    itemIcon.sprite = null;
                    itemIcon.enabled = false;
                     Debug.LogWarning($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Item details or icon missing for item {item.details?.Name ?? "Unknown"}.", this);
                }

                // Only show quantity if maxStack is greater than 1 and quantity is > 1
                if (item.details != null && item.details.maxStack > 1 && item.quantity > 1)
                {
                     quantityText.text = item.quantity.ToString();
                     quantityText.enabled = true;
                     // --- ADD THIS DEBUG LOG ---
                     Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Setting quantity text to {item.quantity}.", this);
                }
                else
                {
                     quantityText.text = ""; // Clear text if quantity is 1 or item not stackable
                     quantityText.enabled = false; // Hide text
                     // --- ADD THIS DEBUG LOG ---
                     Debug.Log($"InventorySlotUI ({gameObject.name}, Index: {SlotIndex}): Hiding quantity text (maxStack<=1 or quantity<=1).", this);
                }

                // Ensure the parent GameObject or Canvas is active if necessary, though usually handled externally
                // if (!gameObject.activeSelf) gameObject.SetActive(true); // Example if needed
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Only start drag with the left mouse button
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Delegate the drag start logic to the central manager
                // Pass this slotUI instance and the event data
                if (DragAndDropManager.Instance != null)
                {
                    DragAndDropManager.Instance.StartDrag(this, eventData);
                }
                else
                {
                    Debug.LogError("InventorySlotUI: DragAndDropManager Instance is null!");
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Only process drag for the left mouse button
             if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Delegate the drag movement logic to the central manager
                 if (DragAndDropManager.Instance != null)
                 {
                     DragAndDropManager.Instance.Drag(eventData);
                 }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Only process end drag for the left mouse button
             if (eventData.button == PointerEventData.InputButton.Left)
            {
                 // Delegate the drag end logic (drop) to the central manager
                 if (DragAndDropManager.Instance != null)
                 {
                     DragAndDropManager.Instance.EndDrag(eventData);
                 }
             }
        }

        // OnPointerUp is often used as the final event after EndDrag,
        // but sometimes EndDrag is sufficient. Depending on desired behavior
        // you might need logic here too, but often EndDrag is where the drop logic goes.
        public void OnPointerUp(PointerEventData eventData)
        {
             // For this implementation, EndDrag handles the drop logic.
             // This method could be used for other interactions like clicks without drags.
        }


        // Optional: Implement this if you want to control when a drag actually begins (e.g., after moving a certain distance)
        // public void OnInitializePotentialDrag(PointerEventData eventData)
        // {
        //      // Tell the EventSystem which button can start this drag
        //      eventData.button = PointerEventData.InputButton.Left;
        // }

        // Optional: You might add methods here later for visual feedback (e.g., highlighting)
    }
}