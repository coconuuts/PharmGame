using UnityEngine;
using UnityEngine.UI; // Required for the Button component

// Assumes a script named 'TabManager' exists and has a public method:
// public void SwitchToPanel(GameObject panelToShow);
// The TabManager script will be created in the next step.

public class TabButtonHandler : MonoBehaviour
{
    [Header("Tab References")]
    [Tooltip("The TabManager component that controls this tab set.")]
    [SerializeField] private TabManager tabManager; // Reference to the parent TabManager

    [Tooltip("The GameObject panel that should be activated when this button is clicked.")]
    [SerializeField] private GameObject contentPanel; // The GameObject panel this button activates

    private Button tabButton; // Reference to the Button component on this GameObject

    private void Awake()
    {
        // Get the Button component attached to this GameObject
        tabButton = GetComponent<Button>();

        // --- Validation Checks ---
        if (tabManager == null)
        {
            Debug.LogError($"TabButtonHandler on {gameObject.name}: Tab Manager reference is not assigned! This tab button will not function.", this);
            enabled = false; // Disable the script if the critical manager reference is missing
            return; // Stop further execution in Awake
        }
        if (contentPanel == null)
        {
            Debug.LogError($"TabButtonHandler on {gameObject.name}: Content Panel reference is not assigned! This tab button will not function.", this);
            enabled = false; // Disable the script if the critical content reference is missing
            return; // Stop further execution in Awake
        }
        if (tabButton == null)
        {
             Debug.LogError($"TabButtonHandler on {gameObject.name}: No Button component found on this GameObject! This tab button will not function.", this);
             enabled = false; // Disable the script if the required Button component is missing
             return; // Stop further execution in Awake
        }
        // --- End Validation Checks ---


        // Add a listener to the button's onClick event
        tabButton.onClick.AddListener(OnTabButtonClick);

        Debug.Log($"TabButtonHandler on {gameObject.name}: Initialized and added click listener for panel {contentPanel.name}.", this);
    }

    private void OnDestroy()
    {
        // Remove the listener when the GameObject is destroyed to prevent potential memory leaks
        if (tabButton != null) // Check if tabButton was successfully found in Awake
        {
            tabButton.onClick.RemoveListener(OnTabButtonClick);
             Debug.Log($"TabButtonHandler on {gameObject.name}: Removed click listener.", this);
        }
    }

    /// <summary>
    /// Called when the tab button is clicked. Notifies the TabManager.
    /// </summary>
    private void OnTabButtonClick()
    {
        // Check if the TabManager reference is still valid before calling
        if (tabManager != null)
        {
             Debug.Log($"TabButtonHandler on {gameObject.name}: Button clicked. Requesting TabManager to switch to panel '{contentPanel.name}'.", this);
            // Call the method on the TabManager to perform the actual panel switching
            tabManager.SwitchToPanel(contentPanel);
        }
         else
         {
             // This case should ideally not happen if Awake checks work, but is a safe fallback.
             Debug.LogWarning($"TabButtonHandler on {gameObject.name}: Button clicked, but TabManager reference is missing or destroyed.", this);
         }
    }

    // Optional: Public method to get the content panel, might be useful for TabManager setup
    // public GameObject GetContentPanel()
    // {
    //     return contentPanel;
    // }
}