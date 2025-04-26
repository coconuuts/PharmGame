using UnityEngine;

public class NPCUIManager : MonoBehaviour
{
    public static NPCUIManager Instance; // Singleton

    [Header("Inventory UI")]
    public GameObject inventoryUIRoot; // Drag your NPCInventory Grid GameObject here in the Inspector
    // public GameObject playerInventoryUIRoot; // You could add player UI here too

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Duplicate UIManager instance found, destroying this one.", this);
            Destroy(gameObject);
        }
        // Don't destroy on load if this manager should persist across scenes
        // DontDestroyOnLoad(gameObject);
    }

    // You can add methods here to easily open/close UI panels etc.
    // public void OpenInventoryUI() { inventoryUIRoot?.SetActive(true); }
    // public void CloseInventoryUI() { inventoryUIRoot?.SetActive(false); }
}
