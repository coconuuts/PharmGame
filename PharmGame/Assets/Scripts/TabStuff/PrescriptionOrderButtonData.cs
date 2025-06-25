// --- START OF FILE PrescriptionOrderButtonData.cs ---

using UnityEngine;
using UnityEngine.UI; // Needed for Image component
using Game.Prescriptions; // Needed for PrescriptionOrder

// Make sure this script is in the same namespace as your other UI scripts if using one
// namespace Systems.UI // Example namespace
// {

    /// <summary>
    /// Simple data holder script attached to a Prescription Order Button prefab instance
    /// to store the PrescriptionOrder struct it represents and manage its visual state.
    /// MODIFIED: Added fields and method for highlighting, using the button's own Image component. // <-- Added note
    /// </summary>
    [RequireComponent(typeof(Button))] // Ensure there's a Button component (which implies an Image)
    public class PrescriptionOrderButtonData : MonoBehaviour
    {
        [Tooltip("The PrescriptionOrder struct this button represents.")]
        public PrescriptionOrder order; // Public field to hold the order data for this button

        // --- NEW FIELDS FOR HIGHLIGHTING ---
        [Header("Highlighting")]
        // Removed the [SerializeField] Image backgroundImage; field

        [Tooltip("The default color of the button background when not highlighted.")]
        [SerializeField] private Color normalColor = Color.white; // Default to white

        [Tooltip("The color of the button background when highlighted.")]
        [SerializeField] private Color highlightColor = Color.yellow; // Default to yellow
        // --- END NEW FIELDS ---

        // Private reference to the button's Image component
        private Image buttonImage; // <-- NEW private field


        // --- NEW METHOD FOR HIGHLIGHTING ---
        /// <summary>
        /// Sets the visual highlight state of the button.
        /// </summary>
        /// <param name="isHighlighted">True to highlight, false to return to normal color.</param>
        public void SetHighlight(bool isHighlighted)
        {
            if (buttonImage == null) // Use the new private field
            {
                Debug.LogWarning($"PrescriptionOrderButtonData on {gameObject.name}: Button Image component not found! Cannot set highlight.", this);
                return;
            }

            buttonImage.color = isHighlighted ? highlightColor : normalColor;
            // Debug.Log($"PrescriptionOrderButtonData on {gameObject.name}: Set highlight to {isHighlighted}.", this); // Too noisy
        }
        // --- END NEW METHOD ---

        // --- NEW: Get the Image component in Awake ---
        private void Awake()
        {
            // Get the Image component that is part of the Button
            buttonImage = GetComponent<Image>();
            if (buttonImage == null)
            {
                // This warning should ideally not happen if RequireComponent(typeof(Button)) is used
                // and the prefab is correctly set up as a UI Button.
                Debug.LogError($"PrescriptionOrderButtonData on {gameObject.name}: No Image component found on this GameObject! Highlighting will not work.", this);
            }
        }
        // --- END NEW ---
    }

// } // End namespace (if using one)