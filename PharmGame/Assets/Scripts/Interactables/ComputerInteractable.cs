// --- START OF FILE ComputerInteractable.cs ---

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Systems.Inventory; // Needed for ItemDetails and Inventory
using Systems.Interaction; // Needed for IInteractable, InteractionResponse
using Systems.UI; // Needed for PlayerUIPopups, IPanelActivatable
using System.Linq;

// Implement the new IPanelActivatable interface
public class ComputerInteractable : MonoBehaviour, IInteractable, IPanelActivatable
{
    [Header("Camera View Point")]
    [Tooltip("The transform the camera should move to when interacting with the computer.")]
    [SerializeField] private Transform cameraViewPoint;

    [Tooltip("The duration of the camera movement animation.")]
    [SerializeField] private float cameraMoveDuration = 0.5f;

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access Computer (E)";

    [Tooltip("Should this interactable be enabled by default when registered?")]
    [SerializeField] private bool enableOnStart = true;
    public bool EnableOnStart => enableOnStart;

    [Header("Prompt Settings")]
    public Vector3 computerTextPromptOffset = Vector3.zero;
    public Vector3 computerTextPromptRotationOffset = Vector3.zero;

    public string InteractionPrompt => interactionPrompt;

    private bool isInteracting = false;

    // --- Shopping Cart and UI Logic ---
    [Header("Computer UI References")]
    [Tooltip("The root GameObject containing all the computer screen UI elements (the one with TabManager).")]
    [SerializeField] private GameObject computerUIContainer;

    [Tooltip("The GameObject panel within the computer UI that contains the shop elements.")]
    [SerializeField] private GameObject shopContentPanel;

    // NEW: Reference to the prefab for shop buttons
    [Tooltip("Prefab for a single shop item button. It should contain a Button and an Image component (for the icon).")]
    [SerializeField] private GameObject shopButtonPrefab;

    [Tooltip("Prefab for a shop category (Accordion style).")]
    [SerializeField] private GameObject shopCategoryPrefab;

    private TextMeshProUGUI shoppingCartText;
    private Button buyButton;

    // --- Item Details and Delivery Inventory ---
    [Header("Inventory Integration")]
    // NEW: List of all purchasable ItemDetails
    [Tooltip("List of all ItemDetails ScriptableObjects available for purchase in this shop.")]
    [SerializeField] private List<ItemDetails> purchasableItems = new List<ItemDetails>();

    [Tooltip("The Inventory component where purchased items will be added (e.g., a delivery truck inventory).")]
    [SerializeField] private Inventory targetDeliveryInventory;


    private Dictionary<ItemDetails, int> shoppingCart = new Dictionary<ItemDetails, int>();

    // NEW: List to keep track of dynamically created buttons for cleanup
    private List<Button> createdShopButtons = new List<Button>();
    private List<GameObject> createdCategories = new List<GameObject>();

    // NEW: Cached UpgradeDetailsSO references for tier unlocks
    // These will now be populated in OnEnable
    private UpgradeDetailsSO _unlockTier1UpgradeSO;
    private UpgradeDetailsSO _unlockTier2UpgradeSO;
    private UpgradeDetailsSO _unlockTier3UpgradeSO;

    // NEW: Cached UpgradeManager instance reference
    private UpgradeManager _upgradeManager; 

    private void Awake()
    {
         if (Systems.Interaction.InteractionManager.Instance != null)
         {
             Systems.Interaction.InteractionManager.Instance.RegisterInteractable(this);
         }
         else
         {
             Debug.LogError($"ComputerInteractable on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
         }
    }

    private void OnEnable() // MODIFIED: Moved upgrade manager fetching and event subscription here
    {
        _upgradeManager = UpgradeManager.Instance;
        if (_upgradeManager == null)
        {
            Debug.LogError($"ComputerInteractable on {gameObject.name}: UpgradeManager.Instance is null in OnEnable! Cannot subscribe to upgrade events or manage shop tiers.", this);
            // Consider disabling component if this is a critical dependency
            // enabled = false; 
            return;
        }

        // --- MODIFIED: Fetch tier upgrade SOs here, now that _upgradeManager is guaranteed to be available ---
        // IMPORTANT: Ensure these names EXACTLY match the 'upgradeName' field in your UpgradeDetailsSO assets.
        _unlockTier1UpgradeSO = _upgradeManager.GetUpgradeDetailsByName("OTC License 1"); 
        if (_unlockTier1UpgradeSO == null)
        {
            Debug.LogWarning($"ComputerInteractable on {gameObject.name}: 'OTC License 1' UpgradeDetailsSO not found in UpgradeManager. Check asset name and UpgradeManager's AllAvailableUpgrades list.", this);
        }

        _unlockTier2UpgradeSO = _upgradeManager.GetUpgradeDetailsByName("OTC License 2");
        if (_unlockTier2UpgradeSO == null)
        {
            Debug.LogWarning($"ComputerInteractable on {gameObject.name}: 'OTC License 2' UpgradeDetailsSO not found in UpgradeManager. Check asset name and UpgradeManager's AllAvailableUpgrades list.", this);
        }

        _unlockTier3UpgradeSO = _upgradeManager.GetUpgradeDetailsByName("OTC License 3");
        if (_unlockTier3UpgradeSO == null)
        {
            Debug.LogWarning($"ComputerInteractable on {gameObject.name}: 'OTC License 3' UpgradeDetailsSO not found in UpgradeManager. Check asset name and UpgradeManager's AllAvailableUpgrades list.", this);
        }
        // --- END MODIFIED ---

        // NEW: Subscribe to UpgradeManager's purchased event globally, as long as this component is enabled
        _upgradeManager.OnUpgradePurchasedSuccessfully += HandleUpgradeUnlocked;
        Debug.Log($"ComputerInteractable: Subscribed to UpgradeManager.OnUpgradePurchasedSuccessfully in OnEnable.");
    }

    private void OnDisable() // MODIFIED: Moved event unsubscription here
    {
        if (_upgradeManager != null)
        {
            _upgradeManager.OnUpgradePurchasedSuccessfully -= HandleUpgradeUnlocked;
            Debug.Log($"ComputerInteractable: Unsubscribed from UpgradeManager.OnUpgradePurchasedSuccessfully in OnDisable.");
        }
        // Clear cached references upon disable for clean state management
        _unlockTier1UpgradeSO = null;
        _unlockTier2UpgradeSO = null;
        _unlockTier3UpgradeSO = null;
        _upgradeManager = null;
    }

    public void ActivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, computerTextPromptOffset, computerTextPromptRotationOffset);
         }
         else
         {
              Debug.LogWarning("ComputerInteractable: PromptEditor.Instance is null. Cannot display prompt.", this);
         }
    }

    public void DeactivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.HidePrompt();
         }
    }

    private void Start()
    {
        if (cameraViewPoint == null)
        {
            Debug.LogError("ComputerInteractable: 'Camera View Point' Transform is not assigned!", this);
        }
        if (computerUIContainer == null)
        {
             Debug.LogError("ComputerInteractable: 'Computer UI Container' GameObject is not assigned!", this);
        }
        if (shopContentPanel == null)
        {
             Debug.LogError("ComputerInteractable: 'Shop Content Panel' GameObject is not assigned! Shop UI functionality will not work.", this);
        }
        if (shopCategoryPrefab == null) Debug.LogError("ComputerInteractable: Shop Category Prefab is not assigned!");
        if (shopButtonPrefab == null)
        {
            Debug.LogError("ComputerInteractable: 'Shop Button Prefab' is not assigned! Dynamic shop buttons cannot be created.", this);
        }
        if (purchasableItems == null || purchasableItems.Count == 0)
        {
            Debug.LogWarning("ComputerInteractable: No purchasable items defined in the 'Purchasable Items' list. The shop will appear empty.", this);
        }
        if (targetDeliveryInventory == null) Debug.LogWarning("ComputerInteractable: Target Delivery Inventory is not assigned. Purchase functionality will not work.");
    }

    /// <summary>
    /// Called by the TabManager when the shopContentPanel becomes active.
    /// Finds UI elements, dynamically creates buttons, and subscribes button listeners.
    /// </summary>
    public void OnPanelActivated()
    {
        if (shopContentPanel == null || shopButtonPrefab == null || shopCategoryPrefab == null) return;

        // 1. Find the Main Content Parent (The Scroll View Content)
        // NOTE: In your hierarchy, this is "ShopButtons". We need to treat this object 
        // as the holder of Categories now, NOT the holder of buttons.
        Transform mainContentParent = shopContentPanel.transform.Find("ShopItemsScrollArea/Viewport/ShopTabs");
        
        if (mainContentParent == null) return;

        shoppingCartText = shopContentPanel.transform.Find("ShoppingCart/Text")?.GetComponent<TextMeshProUGUI>();
        buyButton = shopContentPanel.transform.Find("ShoppingCart/BuyButton")?.GetComponent<Button>();

        CleanupUI(); // Remove old buttons and categories

        // 2. Group items by our custom Category Names
        var groupedItems = purchasableItems
            .Where(item => item != null) // Filter nulls
            .GroupBy(item => GetShopCategoryName(item.itemLabel)) // <-- Changed to use our new function
            .OrderBy(group => group.Key); // Optional sorting

        // 3. Iterate Groups and Create Categories
        foreach (var group in groupedItems)
        {
            // First, check if ANY item in this group is unlocked. 
            // If the whole category is locked, we might want to hide the category header entirely.
            var unlockedItemsInGroup = group.Where(IsItemTierUnlocked).ToList();

            if (unlockedItemsInGroup.Count == 0) continue; // Skip empty categories

            // Instantiate Category Header
            GameObject categoryGO = Instantiate(shopCategoryPrefab, mainContentParent);
            createdCategories.Add(categoryGO);

            ShopCategoryHandler categoryHandler = categoryGO.GetComponent<ShopCategoryHandler>();
            
            // The group.Key is now directly the string returned by GetShopCategoryName
            string categoryName = group.Key; 
            categoryHandler.Setup(categoryName);

            Transform itemContainer = categoryHandler.GetItemContainer();

            // 4. Instantiate Items inside the Category Container
            foreach (ItemDetails details in unlockedItemsInGroup)
            {
                GameObject buttonGO = Instantiate(shopButtonPrefab, itemContainer);
                Button button = buttonGO.GetComponent<Button>();
                Image buttonImage = buttonGO.GetComponent<Image>();

                if (buttonImage != null && details.Icon != null)
                {
                    buttonImage.sprite = details.Icon;
                    buttonImage.color = Color.white;
                    buttonImage.preserveAspect = true;
                }

                // Setup Button Click
                ItemDetails currentItemDetails = details;
                button.onClick.AddListener(() => AddItemToCart(currentItemDetails));
                createdShopButtons.Add(button);
            }
        }

        if(buyButton != null) buyButton.onClick.AddListener(ProcessPurchase);
        
        UpdateShoppingCartUI();
    }

    /// <summary>
    /// Called by the TabManager when the shopContentPanel becomes inactive.
    /// Unsubscribes button listeners and clears references.
    /// </summary>
    public void OnPanelDeactivated()
    {
        CleanupUI(); // Consolidate cleanup logic
        if(buyButton != null) buyButton.onClick.RemoveAllListeners();
        shoppingCartText = null;
        buyButton = null;
    }

    private void CleanupUI()
    {
        // 1. Destroy Buttons
        foreach (Button button in createdShopButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                Destroy(button.gameObject);
            }
        }
        createdShopButtons.Clear();

        // 2. Destroy Categories
        foreach (GameObject category in createdCategories)
        {
            if (category != null) Destroy(category);
        }
        createdCategories.Clear();
    }

    private void OnDestroy()
    {
        // Ensure listeners are removed if the object is destroyed while the panel is destroyed
        OnPanelDeactivated(); // Call the deactivation logic for final cleanup

        if (Systems.Interaction.InteractionManager.Instance != null)
        {
             Systems.Interaction.InteractionManager.Instance.UnregisterInteractable(this);
        }
    }

    public InteractionResponse Interact()
    {
        if (isInteracting)
        {
            Debug.Log("ComputerInteractable: Already interacting with this computer.");
            return null;
        }

        if (cameraViewPoint == null || computerUIContainer == null)
        {
             Debug.LogError("ComputerInteractable: Cannot create EnterComputerResponse - Camera View Point or Computer UI Container not assigned.", this);
             return null;
        }

        Debug.Log("ComputerInteractable: Interact called. Returning EnterComputerResponse.");

        EnterComputerResponse response = new EnterComputerResponse(
            cameraViewPoint,
            cameraMoveDuration,
            computerUIContainer,
            this
        );

        isInteracting = true;

        return response;
    }

    /// <summary>
    /// Checks if a given ItemDetails' tier is currently unlocked based on purchased upgrades.
    /// </summary>
    /// <param name="item">The ItemDetails to check.</param>
    /// <returns>True if the item's tier is unlocked, false otherwise.</returns>
    private bool IsItemTierUnlocked(ItemDetails item)
    {
        if (item == null) return false;

        // Ensure UpgradeManager is available before checking for purchased upgrades
        if (_upgradeManager == null) 
        {
            Debug.LogWarning($"ComputerInteractable: _upgradeManager reference is null when checking unlock status for {item.Name}. Assuming locked.", this);
            return false;
        }

        UpgradeDetailsSO upgradeToCheck = null;
        string upgradeNameDebug = "N/A"; // For logging purposes

        if (item.itemTier == ItemTier.Tier1)
        {
            upgradeToCheck = _unlockTier1UpgradeSO;
            upgradeNameDebug = "OTC License 1"; // Use the correct debug name
        }
        else if (item.itemTier == ItemTier.Tier2)
        {
            upgradeToCheck = _unlockTier2UpgradeSO;
            upgradeNameDebug = "OTC License 2"; // Use the correct debug name
        }
        else if (item.itemTier == ItemTier.Tier3)
        {
            upgradeToCheck = _unlockTier3UpgradeSO;
            upgradeNameDebug = "OTC License 3"; // Use the correct debug name
        }
        else
        {
            // For any other unexpected tiers or 'None' tier, assume locked
            Debug.Log($"ComputerInteractable: Item {item.Name} has unhandled tier {item.itemTier}. Assuming locked.");
            return false;
        }

        if (upgradeToCheck == null)
        {
            Debug.LogWarning($"ComputerInteractable: UpgradeDetailsSO for '{upgradeNameDebug}' (Tier {item.itemTier}) is null in ComputerInteractable cache. Item {item.Name} will be locked. This suggests a problem fetching the upgrade by name in OnEnable.", this);
            return false;
        }

        bool isPurchased = _upgradeManager.IsUpgradePurchased(upgradeToCheck);
        
        return isPurchased;
    }

    /// <summary>
    /// Adds one quantity of the specified item details to the shopping cart.
    /// </summary>
    /// <param name="itemDetails">The ItemDetails of the item to add.</param>
    private void AddItemToCart(ItemDetails itemDetails)
    {
         if(itemDetails == null)
         {
             Debug.LogWarning("ComputerInteractable: Attempted to add null ItemDetails to cart.");
             return;
         }

        Debug.Log($"Attempting to add {itemDetails.Name} to cart.");
        if (shoppingCart.ContainsKey(itemDetails))
        {
            shoppingCart[itemDetails]++;
        }
        else
        {
            shoppingCart[itemDetails] = 1;
        }

        UpdateShoppingCartUI();
    }

    /// <summary>
    /// Updates the shopping cart text display based on the current contents.
    /// </summary>
    private void UpdateShoppingCartUI()
    {
        if (shoppingCartText == null)
        {
            // This warning might happen if the panel is deactivated, which is fine.
            // Debug.LogWarning("ComputerInteractable: ShoppingCartText reference is null. Cannot update UI.");
            return;
        }

        StringBuilder cartDisplay = new StringBuilder();
        cartDisplay.AppendLine("Shopping Cart:");

        if (shoppingCart.Count == 0)
        {
            cartDisplay.Append(" (Empty)");
        }
        else
        {
            foreach (KeyValuePair<ItemDetails, int> itemEntry in new Dictionary<ItemDetails, int>(shoppingCart))
            {
                cartDisplay.AppendLine($"{itemEntry.Value}x {itemEntry.Key.Name}");
            }
        }

        shoppingCartText.text = cartDisplay.ToString();
        Debug.Log("ComputerInteractable: Shopping cart UI updated.");
    }

    /// <summary>
    /// Processes the items currently in the shopping cart, creates Item instances respecting maxStack,
    /// and attempts to add them to the target delivery inventory.
    /// </summary>
    public void ProcessPurchase()
    {
        Debug.Log("ComputerInteractable: Processing purchase!");

        if (targetDeliveryInventory == null)
        {
            Debug.LogError("ComputerInteractable: Target Delivery Inventory is not assigned! Cannot deliver items.", this);
            return;
        }
        if (targetDeliveryInventory.Combiner == null)
         {
             Debug.LogError("ComputerInteractable: Target Delivery Inventory is missing its Combiner component! Cannot deliver items.", this);
             return;
         }

        List<Item> itemsToDeliver = new List<Item>();

        // We iterate over a copy of the shoppingCart to safely modify the original if needed
        foreach (KeyValuePair<ItemDetails, int> itemEntry in new Dictionary<ItemDetails, int>(shoppingCart))
        {
            ItemDetails details = itemEntry.Key;
            int totalQuantityToCreate = itemEntry.Value;

            if (details == null)
            {
                 Debug.LogWarning("ComputerInteractable: Skipping purchase of item with null ItemDetails in cart.");
                 continue;
            }

            Debug.Log($"ComputerInteractable: Preparing to purchase {totalQuantityToCreate}x {details.Name} (Max Stack: {details.maxStack}).");

            int quantityRemaining = totalQuantityToCreate;

            if (details.maxStack == 1)
            {
                for (int i = 0; i < totalQuantityToCreate; i++)
                {
                    itemsToDeliver.Add(details.Create(1));
                     Debug.Log($"ComputerInteractable: Created 1x {details.Name} instance (maxStack 1).");
                }
            }
            else
            {
                int maxStack = details.maxStack;

                while (quantityRemaining >= maxStack)
                {
                    itemsToDeliver.Add(details.Create(maxStack));
                    quantityRemaining -= maxStack;
                     Debug.Log($"ComputerInteractable: Created {maxStack}x {details.Name} instance (full stack). Remaining: {quantityRemaining}.");
                }

                if (quantityRemaining > 0)
                {
                    itemsToDeliver.Add(details.Create(quantityRemaining));
                     Debug.Log($"ComputerInteractable: Created {quantityRemaining}x {details.Name} instance (remaining).");
                }
            }
        }

        Debug.Log($"ComputerInteractable: Delivering {itemsToDeliver.Count} item instances to inventory.");

        bool anyFailedToAdd = false;

        foreach (Item itemInstance in itemsToDeliver)
        {
            bool added = targetDeliveryInventory.AddItem(itemInstance);

            if (!added)
            {
                 Debug.LogWarning($"ComputerInteractable: Failed to add item instance '{itemInstance.details?.Name ?? "Unknown"}' (Initial Qty: {itemInstance.quantity}) to delivery inventory. Remaining on instance: {itemInstance.quantity}. It might be full or filtering disallowed.", this);
                 anyFailedToAdd = true;
            }
             else
             {
                  Debug.Log($"ComputerInteractable: Successfully added item instance '{itemInstance.details?.Name ?? "Unknown"}' to delivery inventory. Remaining on instance: {itemInstance.quantity}.");
             }
        }

         if (!anyFailedToAdd)
         {
             Debug.Log("ComputerInteractable: All purchased item instances successfully delivered.");
         }
         else
         {
             Debug.LogWarning("ComputerInteractable: Some purchased item instances could not be delivered.", this);
             PlayerUIPopups.Instance?.ShowPopup("ToolbarPopup", "Some items could not be delivered! Inventory might be full."); // Changed text for partial success
         }

        shoppingCart.Clear();
        UpdateShoppingCartUI();

        Debug.Log("ComputerInteractable: Purchase process completed.");
    }

    /// <summary>
    /// Handles the event when an upgrade is successfully purchased.
    /// If it's a tier-unlocking upgrade, it refreshes the shop UI.
    /// </summary>
    /// <param name="unlockedUpgrade">The UpgradeDetailsSO that was just purchased.</param>
    private void HandleUpgradeUnlocked(UpgradeDetailsSO unlockedUpgrade)
    {
        if (unlockedUpgrade == null)
        {
            Debug.LogWarning("ComputerInteractable: Received null unlockedUpgrade in HandleUpgradeUnlocked.", this);
            return;
        }

        // Check if the purchased upgrade is one of our tier-unlocking upgrades
        if (unlockedUpgrade == _unlockTier1UpgradeSO || unlockedUpgrade == _unlockTier2UpgradeSO || unlockedUpgrade == _unlockTier3UpgradeSO)
        {
            Debug.Log($"ComputerInteractable: Tier unlock upgrade '{unlockedUpgrade.upgradeName}' purchased. Re-populating shop list to reflect new availability.");

            // Clear existing buttons (returns them to pool)
            // Note: This relies on the current implementation of OnPanelActivated to re-create buttons.
            // If using a more persistent UI, a granular update would be needed.
            // For now, this full refresh is simpler and effective.
            
            // First, clear the shopping cart to prevent trying to purchase items from a now-unlocked tier that might have been added while locked
            shoppingCart.Clear();
            UpdateShoppingCartUI(); // Update UI to show empty cart

            // Re-populate the list, which will now enable the newly unlocked tier's buttons
            // This is equivalent to calling OnPanelDeactivated() then OnPanelActivated()
            OnPanelActivated(); 
            
            PlayerUIPopups.Instance?.ShowPopup("ToolbarPopup", $"New {unlockedUpgrade.upgradeName} items are now available!");
        }
    }

    /// <summary>
    /// Maps an ItemLabel to a specific shop category string.
    /// </summary>
    private string GetShopCategoryName(ItemLabel label)
    {
        switch (label)
        {
            case ItemLabel.OverTheCounter:
                return "Over-the-Counter";

            case ItemLabel.PillStock:
            case ItemLabel.LiquidStock:
            case ItemLabel.InhalerStock:
            case ItemLabel.InsulinStock:
                return "Stock";

            case ItemLabel.PillMedContainer:
            case ItemLabel.LiquidMedContainer:
            case ItemLabel.InhalerMedContainer:
            case ItemLabel.InsulinMedContainer:
                return "Packaging";

            default:
                // Fallback for any items not explicitly listed above 
                // Uses the Regex you originally had to format the Enum name nicely
                return System.Text.RegularExpressions.Regex.Replace(label.ToString(), "(\\B[A-Z])", " $1");
        }
    }

    public void ResetInteraction()
    {
        isInteracting = false;
        Debug.Log($"ComputerInteractable ({gameObject.name}): ResetInteraction called. isInteracting is now false.", this);
    }
}
// --- END OF FILE ComputerInteractable.cs ---