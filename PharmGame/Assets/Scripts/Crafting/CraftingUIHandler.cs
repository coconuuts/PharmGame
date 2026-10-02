using UnityEngine;
using UnityEngine.UI; // Needed for Button

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// Manages the visual state and interaction elements of the crafting UI.
    /// Communicates with the CraftingStation for logic.
    /// </summary>
    public class CraftingUIHandler : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("The GameObject containing the UI for the primary input inventory.")]
        [SerializeField] private GameObject primaryInputUIPanel;

        [Tooltip("The GameObject containing the UI for the secondary input inventory (optional).")]
        [SerializeField] private GameObject secondaryInputUIPanel;

        [Tooltip("The GameObject containing the UI for the output inventory.")]
        [SerializeField] private GameObject outputUIPanel;

        [Tooltip("The Craft Button UI element.")]
        [SerializeField] private Button craftButton;

        // Reference to the CraftingStation component this UI handler is for
        private CraftingStation linkedCraftingStation;

        private void Awake()
        {
            Debug.Log($"CraftingUIHandler ({gameObject.name}): Awake. Initializing UI states.", this);
            // Initially ensure all panels and the button are hidden/disabled
            if (primaryInputUIPanel != null) primaryInputUIPanel.SetActive(false);
            if (secondaryInputUIPanel != null) secondaryInputUIPanel.SetActive(false);
            if (outputUIPanel != null) outputUIPanel.SetActive(false);
            if (craftButton != null)
            {
                craftButton.gameObject.SetActive(false);
                // Button interactability is handled by UpdateUIState and SetCraftButtonInteractable
            }

            // Add listener to the craft button click
            if (craftButton != null)
            {
                craftButton.onClick.AddListener(OnCraftButtonClicked);
            }
             else
             {
                  Debug.LogError($"CraftingUIHandler ({gameObject.name}): Craft Button reference is not assigned!", this);
             }
        }

        private void OnDestroy()
        {
            if (craftButton != null)
            {
                craftButton.onClick.RemoveListener(OnCraftButtonClicked);
            }
        }

        /// <summary>
        /// Sets the link to the managing CraftingStation and performs initial setup.
        /// Called by the CraftingStation when the UI is opened.
        /// </summary>
        /// <param name="station">The CraftingStation component.</param>
        public void LinkCraftingStation(CraftingStation station)
        {
            linkedCraftingStation = station;
            Debug.Log($"CraftingUIHandler ({gameObject.name}): Linked to Crafting Station '{station.gameObject.name}'.", this);
            // The initial state update will be called by the CraftingStation immediately after linking
        }

        /// <summary>
        /// Updates the visibility of UI panels and button based on the current crafting state.
        /// Called by the CraftingStation when the state changes.
        /// </summary>
        /// <param name="state">The current state of the crafting station.</param>
        public void UpdateUIState(CraftingStation.CraftingState state)
        {
            Debug.Log($"CraftingUIHandler ({gameObject.name}): Updating UI for state: {state}.", this);
            switch (state)
            {
                case CraftingStation.CraftingState.Inputting:
                    if (primaryInputUIPanel != null) primaryInputUIPanel.SetActive(true);
                    // Only activate secondary if it's assigned in the inspector
                    if (secondaryInputUIPanel != null) secondaryInputUIPanel.SetActive(true);
                    if (outputUIPanel != null) outputUIPanel.SetActive(false);
                    if (craftButton != null) craftButton.gameObject.SetActive(true); 
                    // Interactability handled by SetCraftButtonInteractable
                    break;
                case CraftingStation.CraftingState.Crafting:
                    // Typically hide/disable interaction with slots during crafting animation/time
                    if (primaryInputUIPanel != null) primaryInputUIPanel.SetActive(false);
                    if (secondaryInputUIPanel != null) secondaryInputUIPanel.SetActive(false);
                    if (outputUIPanel != null) outputUIPanel.SetActive(false); 
                    if (craftButton != null) craftButton.gameObject.SetActive(false); 
                    break;
                case CraftingStation.CraftingState.Outputting:
                    if (primaryInputUIPanel != null) primaryInputUIPanel.SetActive(false);
                    if (secondaryInputUIPanel != null) secondaryInputUIPanel.SetActive(false);
                    if (outputUIPanel != null) outputUIPanel.SetActive(true);
                    if (craftButton != null) craftButton.gameObject.SetActive(false); 
                    break;
            }
            Debug.Log($"CraftingUIHandler ({gameObject.name}): UI updated for state: {state}.", this);
        }

        /// <summary>
        /// Sets whether the Craft button is interactable.
        /// Called by the CraftingStation based on recipe match.
        /// </summary>
        /// <param name="isInteractable">True to enable the button, false to disable.</param>
        public void SetCraftButtonInteractable(bool isInteractable)
        {
            if (craftButton != null)
            {
                craftButton.interactable = isInteractable;
            }
        }

        /// <summary>
        /// Handles the Craft button click event.
        /// </summary>
        private void OnCraftButtonClicked()
        {
            if (linkedCraftingStation != null)
            {
                Debug.Log($"CraftingUIHandler ({gameObject.name}): Craft button clicked. Notifying CraftingStation.", this);
                // Delegate the craft logic back to the CraftingStation
                linkedCraftingStation.NotifyCraftButtonClicked(); 
            }
            else
            {
                Debug.LogWarning($"CraftingUIHandler ({gameObject.name}): Craft button clicked but no CraftingStation link!", this);
            }
        }

        // Optional: Add methods to update UI based on recipe match visual feedback if needed later
        // public void ShowRecipeMatchFeedback(CraftingRecipe recipe) { ... }
        // public void HideRecipeMatchFeedback() { ... }
    }
}