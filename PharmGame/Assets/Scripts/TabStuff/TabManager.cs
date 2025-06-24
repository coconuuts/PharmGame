using UnityEngine;
using System.Collections.Generic; // Required for List and Dictionary
using Systems.UI; 

// Helper class to store the mapping between a tab button handler and its content panel
// This makes it easier to manage the relationships in the Inspector and code.
[System.Serializable] // Make this struct visible in the Inspector
public struct TabEntry
{
    [Tooltip("The TabButtonHandler script attached to the button for this tab.")]
    public TabButtonHandler tabButtonHandler;
    [Tooltip("The GameObject content panel associated with this tab.")]
    public GameObject contentPanel;
    // NEW: Optional reference to an IPanelActivatable component NOT on the content panel itself.
    // This is for scripts elsewhere that need to know when this panel is active (e.g., ComputerInteractable).
    [Tooltip("Optional: An IPanelActivatable component (likely on another GameObject) to notify when this panel is activated/deactivated.")]
    public MonoBehaviour externalActivatable; // Use MonoBehaviour because IPanelActivatable is not a UnityEngine.Object
}

public class TabManager : MonoBehaviour
{
    [Header("Tab Configuration")]
    [Tooltip("List of tab button handlers and their corresponding content panels.")]
    [SerializeField] private List<TabEntry> tabs = new List<TabEntry>();

    [Tooltip("The index of the tab that should be active by default when the UI is enabled.")]
    [SerializeField] private int defaultTabIndex = 0;

    // Dictionary for quick lookup of panel by button handler (optional, list is often sufficient for small numbers)
    // private Dictionary<TabButtonHandler, GameObject> tabMap = new Dictionary<TabButtonHandler, GameObject>();

    private GameObject currentActivePanel = null; // Tracks the currently active content panel

    private void Awake()
    {
        // --- Validation and Setup ---
        if (tabs == null || tabs.Count == 0)
        {
            Debug.LogWarning($"TabManager on {gameObject.name}: No tabs configured in the list! The tab system will not function.", this);
            enabled = false; // Disable if no tabs are set up
            return;
        }

        // Ensure all entries have valid references
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].tabButtonHandler == null)
            {
                Debug.LogError($"TabManager on {gameObject.name}: Tab Entry {i} has a null TabButtonHandler reference! Please fix in the Inspector.", this);
                // Decide how to handle: remove the entry, disable the manager, etc.
                // For now, we'll log and continue, but the system might not work correctly.
            }
            if (tabs[i].contentPanel == null)
            {
                Debug.LogError($"TabManager on {gameObject.name}: Tab Entry {i} has a null ContentPanel reference! Please fix in the Inspector.", this);
                // Decide how to handle
            }
            // Optional: Set the tabManager reference on the handler here if you don't do it manually in Inspector
            // if (tabs[i].tabButtonHandler != null)
            // {
            //      // Assuming TabButtonHandler has a public setter or property for tabManager
            //      // tabs[i].tabButtonHandler.SetTabManager(this); // You'd need to add this method
            // }
        }

        // Optional: Initialize the dictionary if using it
        // foreach(var entry in tabs)
        // {
        //     if(entry.tabButtonHandler != null && entry.contentPanel != null)
        //     {
        //          tabMap[entry.tabButtonHandler] = entry.contentPanel;
        //     }
        // }
        // --- End Validation and Setup ---

        Debug.Log($"TabManager on {gameObject.name}: Initialized with {tabs.Count} tabs.", this);
    }

    private void OnEnable()
    {
        // When the Tabbed UI is enabled, switch to the default tab
        // Ensure defaultTabIndex is within bounds
        int initialTabIndex = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1);

        // Trigger the switch to the initial tab.
        // Check if tabs list is valid before accessing index
        if (tabs != null && tabs.Count > 0)
        {
            // Use the public method to ensure proper state management
            SwitchToPanel(tabs[initialTabIndex].contentPanel);
        }
        else
        {
            // This case should be caught by Awake, but double check
            Debug.LogWarning($"TabManager on {gameObject.name}: Cannot switch to default tab, no tabs configured.", this);
        }
    }

    /// <summary>
    /// Called by a TabButtonHandler to request switching to a specific content panel.
    /// </summary>
    /// <param name="panelToShow">The GameObject of the content panel to activate.</param>
    public void SwitchToPanel(GameObject panelToShow)
    {
        if (panelToShow == null)
        {
            Debug.LogWarning("TabManager: Attempted to switch to a null panel.", this);
            return;
        }

        // If the requested panel is already active, do nothing
        if (currentActivePanel == panelToShow)
        {
            Debug.Log($"TabManager on {gameObject.name}: Panel '{panelToShow.name}' is already active.", this);
            return;
        }

        Debug.Log($"TabManager on {gameObject.name}: Switching to panel '{panelToShow.name}'.", this);

        // --- Find the TabEntry for the currently active panel (before it changes) ---
        TabEntry? oldEntry = null; // Use nullable struct
        if (currentActivePanel != null)
        {
            // Find the entry corresponding to the old active panel
            foreach (var entry in tabs)
            {
                if (entry.contentPanel == currentActivePanel)
                {
                    oldEntry = entry;
                    break;
                }
            }
        }

        // --- Deactivate the current active panel and notify its associated external component ---
        if (currentActivePanel != null && currentActivePanel.activeSelf)
        {
            // Notify the external IPanelActivatable component associated with the OLD panel
            if (oldEntry.HasValue && oldEntry.Value.externalActivatable != null)
            {
                IPanelActivatable activatable = oldEntry.Value.externalActivatable as IPanelActivatable;
                if (activatable != null)
                {
                    Debug.Log($"TabManager on {gameObject.name}: Notifying external activatable on {oldEntry.Value.externalActivatable.gameObject.name} (old panel '{currentActivePanel.name}') OnPanelDeactivated.", this);
                    activatable.OnPanelDeactivated(); // Notify BEFORE deactivating the GameObject
                }
                else
                {
                    Debug.LogWarning($"TabManager on {gameObject.name}: externalActivatable on {oldEntry.Value.externalActivatable.gameObject.name} is not IPanelActivatable for old panel '{currentActivePanel.name}'.", this);
                }
            }

            currentActivePanel.SetActive(false);

            // Optional: Update visual state of the corresponding button handler for the old panel
            // if (oldEntry.HasValue && oldEntry.Value.tabButtonHandler != null) oldEntry.Value.tabButtonHandler.SetVisualState(false);
        }

        // --- Find the TabEntry for the panel to show ---
        TabEntry? newEntry = null;
        // Find the entry corresponding to the panel to show
        foreach (var entry in tabs)
        {
            if (entry.contentPanel == panelToShow)
            {
                newEntry = entry;
                break;
            }
        }

        if (!newEntry.HasValue)
        {
            Debug.LogError($"TabManager: Could not find TabEntry for panel '{panelToShow.name}'. Cannot activate.", this);
            currentActivePanel = null; // Ensure tracker is null if we fail to activate
            return;
        }


        // --- Activate the requested panel and notify its associated external component ---
        panelToShow.SetActive(true);
        currentActivePanel = panelToShow; // Update the tracker

        // Notify the external IPanelActivatable component associated with the NEW panel
        if (newEntry.Value.externalActivatable != null)
        {
            IPanelActivatable activatable = newEntry.Value.externalActivatable as IPanelActivatable;
            if (activatable != null)
            {
                Debug.Log($"TabManager on {gameObject.name}: Notifying external activatable on {newEntry.Value.externalActivatable.gameObject.name} (new panel '{panelToShow.name}') OnPanelActivated.", this);
                activatable.OnPanelActivated(); // Notify AFTER activating the GameObject
            }
            else
            {
                Debug.LogWarning($"TabManager on {gameObject.name}: externalActivatable on {newEntry.Value.externalActivatable.gameObject.name} is not IPanelActivatable for new panel '{panelToShow.name}'.", this);
            }
        }

        // Optional: Update visual state of the button handler corresponding to the active panel
        // if (newEntry.Value.tabButtonHandler != null) newEntry.Value.tabButtonHandler.SetVisualState(true);
    }
    
    // Optional: Add a public method to get the current active panel
    // public GameObject GetCurrentActivePanel()
    // {
    //     return currentActivePanel;
    // }
}