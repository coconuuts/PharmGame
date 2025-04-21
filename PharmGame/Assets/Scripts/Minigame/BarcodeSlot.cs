using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Needed for IPointerClickHandler
using Systems.Minigame; // Assuming your MinigameManager is in this namespace

namespace Systems.Minigame // Use the same namespace as MinigameManager
{
    /// <summary>
    /// Represents a single clickable slot in the barcode minigame grid.
    /// Displays a barcode sprite and notifies the MinigameManager when clicked.
    /// </summary>
    public class BarcodeSlot : MonoBehaviour, IPointerClickHandler // Implement click handler
    {
        [Tooltip("The Image component that displays the barcode sprite.")]
        [SerializeField] private Image barcodeImage;

        // A reference back to the MinigameManager instance
        private MinigameManager minigameManager;

        /// <summary>
        /// Checks if this slot currently has a visible barcode.
        /// </summary>
        public bool IsEmpty => barcodeImage.sprite == null || !barcodeImage.enabled;

        private void Awake()
        {
            if (barcodeImage == null)
            {
                Debug.LogError($"BarcodeSlot ({gameObject.name}): Barcode Image component is not assigned!", this);
                enabled = false; // Disable if essential reference is missing
            }
             else
             {
                 // Ensure it's empty and hidden initially
                 ClearSprite();
             }
        }

        // Set the reference to the MinigameManager (called by MinigameManager)
        public void SetMinigameManager(MinigameManager manager)
        {
            minigameManager = manager;
        }

        /// <summary>
        /// Sets the sprite for this slot and makes it visible.
        /// </summary>
        /// <param name="sprite">The sprite to display.</param>
        public void SetSprite(Sprite sprite)
        {
            if (barcodeImage != null)
            {
                barcodeImage.sprite = sprite;
                barcodeImage.enabled = sprite != null; // Enable image only if sprite is not null
                 Debug.Log($"BarcodeSlot ({gameObject.name}): Sprite set.");
            }
        }

        /// <summary>
        /// Clears the sprite from this slot and hides the image.
        /// </summary>
        public void ClearSprite()
        {
            if (barcodeImage != null)
            {
                barcodeImage.sprite = null;
                barcodeImage.enabled = false;
                 Debug.Log($"BarcodeSlot ({gameObject.name}): Sprite cleared.");
            }
        }

        // --- IPointerClickHandler Implementation ---
        public void OnPointerClick(PointerEventData eventData)
        {
            // Only handle clicks if the left mouse button was used
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Only process click if there is a barcode visible in this slot
                if (!IsEmpty)
                {
                    Debug.Log($"BarcodeSlot ({gameObject.name}): Clicked!");
                    // Notify the MinigameManager that this slot was clicked
                    minigameManager?.BarcodeClicked(this); // Use ?. for safety
                }
                else
                {
                    Debug.Log($"BarcodeSlot ({gameObject.name}): Clicked, but slot is empty.");
                }
            }
        }

        // You might add other event handlers here if needed (e.g., OnPointerEnter, OnPointerExit for hover effects)
    }
}