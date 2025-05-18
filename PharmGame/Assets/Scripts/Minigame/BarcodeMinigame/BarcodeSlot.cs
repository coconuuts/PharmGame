using UnityEngine;
using UnityEngine.UI; // Needed for Image component
using UnityEngine.EventSystems; // Needed for IPointerClickHandler
// Removed using Systems.GameStates; // Not needed here

namespace Systems.Minigame // Ensure this matches
{
    // --- MODIFIED: Implement IPointerClickHandler ---
    // Assuming your slots are clickable UI elements
    public class BarcodeSlot : MonoBehaviour, IPointerClickHandler
    {
        // --- MODIFIED: Reference to the parent BarcodeMinigame instance ---
        private BarcodeMinigame barcodeMinigame;
        // ----------------------------------------------------------------

        [Tooltip("The Image component displaying the barcode sprite.")]
        [SerializeField] private Image barcodeImage;

        // Property to check if the slot has a sprite
        public bool IsEmpty => barcodeImage.sprite == null;


        // --- MODIFIED: Method to set the parent BarcodeMinigame instance ---
        /// <summary>
        /// Called by the BarcodeMinigame instance to provide a reference back.
        /// </summary>
        /// <param name="manager">The BarcodeMinigame instance this slot belongs to.</param>
        public void SetBarcodeMinigame(BarcodeMinigame manager)
        {
            barcodeMinigame = manager;
            // Optional: Debug.Log($"BarcodeSlot {gameObject.name}: BarcodeMinigame reference set.");
        }
        // ------------------------------------------------------------------


        /// <summary>
        /// Sets the sprite for this slot.
        /// </summary>
        /// <param name="sprite">The sprite to display.</param>
        public void SetSprite(Sprite sprite)
        {
            if (barcodeImage != null)
            {
                barcodeImage.sprite = sprite;
                 // Ensure the Image is visible when a sprite is set
                 barcodeImage.enabled = (sprite != null); // Enable if sprite exists, disable if null
                // Optional: Debug.Log($"BarcodeSlot {gameObject.name}: Sprite set.");
            }
             else Debug.LogWarning($"BarcodeSlot {gameObject.name}: Barcode Image component is not assigned!", this);
        }

        /// <summary>
        /// Clears the sprite from this slot.
        /// </summary>
        public void ClearSprite()
        {
            SetSprite(null); // Set sprite to null
            // Optional: Debug.Log($"BarcodeSlot {gameObject.name}: Sprite cleared.");
        }


        // --- Implement IPointerClickHandler interface ---
        public void OnPointerClick(PointerEventData eventData)
        {
            // --- MODIFIED: Call BarcodeClicked on the assigned BarcodeMinigame instance ---
            if (barcodeMinigame != null)
            {
                // Only process clicks if the slot is NOT empty
                if (!IsEmpty)
                {
                    barcodeMinigame.BarcodeClicked(this); // Pass this slot instance
                    // Optional: Debug.Log($"BarcodeSlot {gameObject.name}: Click processed.");
                }
                 else Debug.Log("BarcodeSlot: Clicked on an empty slot.");
            }
            else
            {
                Debug.LogWarning($"BarcodeSlot {gameObject.name}: BarcodeMinigame reference is null. Cannot process click.", this);
            }
            // -------------------------------------------------------------------------
        }

         // Add Awake or Start if needed for initialization
         private void Awake()
         {
              // Ensure image is initially disabled if no sprite is set
              if (barcodeImage != null)
              {
                   barcodeImage.enabled = (barcodeImage.sprite != null);
              }
         }
    }
}