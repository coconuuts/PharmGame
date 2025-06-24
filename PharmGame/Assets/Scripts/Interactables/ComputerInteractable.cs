// --- START OF FILE ComputerInteractable.cs ---

using UnityEngine;
using UnityEngine.UI;
using System.Collections; // Keep if you have other coroutines, otherwise remove
using System.Collections.Generic;
using System.Text;
using TMPro;
using Systems.Inventory; // Needed for ItemDetails and Inventory
using Systems.Interaction; // Needed for IInteractable, InteractionResponse
using Systems.UI; // Needed for PlayerUIPopups, IPanelActivatable

// Implement the new IPanelActivatable interface
public class ComputerInteractable : MonoBehaviour, IInteractable, IPanelActivatable // ADD IPanelActivatable
{
    [Header("Camera View Point")]
    [Tooltip("The transform the camera should move to when interacting with the computer.")]
    [SerializeField] private Transform cameraViewPoint;

    [Tooltip("The duration of the camera movement animation.")]
    [SerializeField] private float cameraMoveDuration = 0.5f;

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access Computer (E)";

    [Tooltip("Should this interactable be enabled by default when registered?")]
    [SerializeField] private bool enableOnStart = true; // Keep this field for InteractionManager

    [Header("Prompt Settings")]
    public Vector3 computerTextPromptOffset = Vector3.zero;
    public Vector3 computerTextPromptRotationOffset = Vector3.zero;

    public string InteractionPrompt => interactionPrompt;

    // isInteracting is still used to prevent re-triggering Interact() while in the computer state
    private bool isInteracting = false;

    // --- Shopping Cart and UI Logic ---
    [Header("Computer UI References")]
    [Tooltip("The root GameObject containing all the computer screen UI elements (the one with TabManager).")]
    [SerializeField] private GameObject computerUIContainer;

    // NEW: Reference to the specific content panel this script manages (the Shop panel)
    [Tooltip("The GameObject panel within the computer UI that contains the shop elements.")]
    [SerializeField] private GameObject shopContentPanel;

    // REMOVED: Direct [SerializeField] references to UI elements.
    // [Tooltip("Button for the first medicine item.")]
    // [SerializeField] private Button item1Button;
    // [Tooltip("Button for the second medicine item.")]
    // [SerializeField] private Button item2Button;
    // [Tooltip("Text component to display the shopping cart contents.")]
    // [SerializeField] private TextMeshProUGUI shoppingCartText;
    // [Tooltip("Button to process the purchase.")]
    // [SerializeField] private Button buyButton;

    // NEW: Private fields to hold dynamically found UI element references
    private Button item1Button;
    private Button item2Button;
    private TextMeshProUGUI shoppingCartText;
    private Button buyButton;


    // --- Item Details and Delivery Inventory ---
    [Header("Inventory Integration")]
    [Tooltip("The ItemDetails ScriptableObject for the first purchasable item.")]
    [SerializeField] private ItemDetails item1Details;

    [Tooltip("The ItemDetails ScriptableObject for the second purchasable item.")]
    [SerializeField] private ItemDetails item2Details;

    [Tooltip("The Inventory component where purchased items will be added (e.g., a delivery truck inventory).")]
    [SerializeField] private Inventory targetDeliveryInventory;


    private Dictionary<ItemDetails, int> shoppingCart = new Dictionary<ItemDetails, int>();

    // --- Awake Method for Registration ---
    private void Awake()
    {
         // Register with the singleton InteractionManager
         if (Systems.Interaction.InteractionManager.Instance != null)
         {
             Systems.Interaction.InteractionManager.Instance.RegisterInteractable(this);
         }
         else
         {
             Debug.LogError($"ComputerInteractable on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
             // Consider disabling the component if registration is essential
             // enabled = false;
         }

         // REMOVED: Any manual enabled = false calls from Awake if they existed
         // The InteractionManager handles the initial enabled state based on enableOnStart.
    }
    // --- END NEW ---


    // --- Existing IInteractable methods ---
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

    // --- Start Method ---
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
        if (item1Details == null) Debug.LogWarning("ComputerInteractable: Item 1 Details are not assigned. Item 1 button functionality may be limited.");
        if (item2Details == null) Debug.LogWarning("ComputerInteractable: Item 2 Details are not assigned. Item 2 button functionality may be limited.");
        if (targetDeliveryInventory == null) Debug.LogWarning("ComputerInteractable: Target Delivery Inventory is not assigned. Purchase functionality will not work.");


        // REMOVED: Button subscription logic from Start.
        // This will now happen in OnPanelActivated.

        // REMOVED: Initial UpdateShoppingCartUI() call from Start.
        // This will now happen in OnPanelActivated.
    }

    // --- NEW: Implement IPanelActivatable methods ---

    /// <summary>
    /// Called by the TabManager when the shopContentPanel becomes active.
    /// Finds UI elements and subscribes button listeners.
    /// </summary>
    public void OnPanelActivated()
    {
        Debug.Log("ComputerInteractable: Shop Panel Activated. Finding UI elements and subscribing listeners.");

        if (shopContentPanel == null)
        {
            Debug.LogError("ComputerInteractable: shopContentPanel is null in OnPanelActivated! Cannot find UI elements.", this);
            return;
        }

        // --- Find UI elements dynamically within the shopContentPanel ---
        // Using GetComponentInChildren is generally safe if the elements are children.
        // You might need to adjust if they are deeply nested or named differently.
        item1Button = shopContentPanel.transform.Find("ShopItemsScrollArea/Viewport/ShopButtons/Med1")?.GetComponent<Button>();
        item2Button = shopContentPanel.transform.Find("ShopItemsScrollArea/Viewport/ShopButtons/Med2")?.GetComponent<Button>();
        shoppingCartText = shopContentPanel.transform.Find("ShoppingCart/Text")?.GetComponent<TextMeshProUGUI>(); 
        buyButton = shopContentPanel.transform.Find("ShoppingCart/BuyButton")?.GetComponent<Button>(); 

        // --- Subscribe to button click events ---
        if (item1Button != null && item1Details != null)
        {
            item1Button.onClick.AddListener(() => AddItemToCart(item1Details));
            Debug.Log("ComputerInteractable: Subscribed Item 1 Button listener.");
        }
        else
        {
            if(item1Button == null) Debug.LogWarning("ComputerInteractable: Item 1 Button not found under shopContentPanel.");
            // item1Details warning is already in Start
        }

        if (item2Button != null && item2Details != null)
        {
            item2Button.onClick.AddListener(() => AddItemToCart(item2Details));
            Debug.Log("ComputerInteractable: Subscribed Item 2 Button listener.");
        }
        else
        {
             if(item2Button == null) Debug.LogWarning("ComputerInteractable: Item 2 Button not found under shopContentPanel.");
             // item2Details warning is already in Start
        }

        if(buyButton != null)
        {
            buyButton.onClick.AddListener(ProcessPurchase);
            Debug.Log("ComputerInteractable: Subscribed Buy Button listener.");
        }
        else
        {
             Debug.LogWarning("ComputerInteractable: Buy Button not found under shopContentPanel.");
        }

        // Update the UI display when the panel becomes active
        UpdateShoppingCartUI();
    }

    /// <summary>
    /// Called by the TabManager when the shopContentPanel becomes inactive.
    /// Unsubscribes button listeners and clears references.
    /// </summary>
    public void OnPanelDeactivated()
    {
        Debug.Log("ComputerInteractable: Shop Panel Deactivated. Unsubscribing listeners and clearing references.");

        // --- Unsubscribe button events ---
        // Check if references are valid before removing listeners
        if(item1Button != null)
        {
            item1Button.onClick.RemoveAllListeners(); // Remove all listeners added to this button
            Debug.Log("ComputerInteractable: Unsubscribed Item 1 Button listeners.");
        }
        if(item2Button != null)
        {
            item2Button.onClick.RemoveAllListeners(); // Remove all listeners added to this button
            Debug.Log("ComputerInteractable: Unsubscribed Item 2 Button listeners.");
        }
        if(buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners(); // Remove all listeners added to this button
            Debug.Log("ComputerInteractable: Unsubscribed Buy Button listeners.");
        }

        // --- Clear dynamic references ---
        item1Button = null;
        item2Button = null;
        shoppingCartText = null;
        buyButton = null;
    }

    // --- END NEW ---


    // --- Existing OnDestroy Method ---
    private void OnDestroy()
    {
        // Ensure listeners are removed if the object is destroyed while the panel is active
        // OnPanelDeactivated might not have been called if the whole UI root is destroyed.
        // This provides a final cleanup.
        OnPanelDeactivated(); // Call the deactivation logic for cleanup

        // Unregister from the singleton InteractionManager
        if (Systems.Interaction.InteractionManager.Instance != null)
        {
             Systems.Interaction.InteractionManager.Instance.UnregisterInteractable(this);
        }
    }

    /// <summary>
    /// Runs the object's specific interaction logic and returns a response describing the outcome.
    /// Changed to return InteractionResponse.
    /// </summary>
    /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
    public InteractionResponse Interact() // CHANGED RETURN TYPE
    {
        // Only interact if *this* computer is not already in use
        if (isInteracting)
        {
            Debug.Log("ComputerInteractable: Already interacting with this computer.");
            return null; // Return null response if already interacting
        }

        // Check essential data for response
        if (cameraViewPoint == null || computerUIContainer == null)
        {
             Debug.LogError("ComputerInteractable: Cannot create EnterComputerResponse - Camera View Point or Computer UI Container not assigned.", this);
             return null;
        }

        Debug.Log("ComputerInteractable: Interact called. Returning EnterComputerResponse.");

        // Create and return the response
        // The PlayerInteractionManager will receive this and pass it to the MenuManager
        EnterComputerResponse response = new EnterComputerResponse(
            cameraViewPoint,
            cameraMoveDuration,
            computerUIContainer, // Pass the root UI container GameObject
            this // Pass a reference to this ComputerInteractable instance
        );

        // Mark as interacting immediately before returning the response
        // The state exit action in MenuManager will be responsible for clearing this flag.
        isInteracting = true;

        return response;
    }

    // --- Shopping Cart Methods ---

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
        // Use ItemDetails as the key
        if (shoppingCart.ContainsKey(itemDetails))
        {
            shoppingCart[itemDetails]++;
        }
        else
        {
            shoppingCart[itemDetails] = 1;
        }

        UpdateShoppingCartUI(); // Update the UI display after adding an item
    }

    /// <summary>
    /// Updates the shopping cart text display based on the current contents.
    /// </summary>
    private void UpdateShoppingCartUI()
    {
        // Use the dynamically found reference
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
            // Iterate over a copy to avoid issues if the collection is modified during iteration (though unlikely here)
            foreach (KeyValuePair<ItemDetails, int> itemEntry in new Dictionary<ItemDetails, int>(shoppingCart))
            {
                // Get the item name from the ItemDetails key
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
    public void ProcessPurchase() // Linked to the Buy button
    {
        Debug.Log("ComputerInteractable: Processing purchase!");

        if (targetDeliveryInventory == null)
        {
            Debug.LogError("ComputerInteractable: Target Delivery Inventory is not assigned! Cannot deliver items.", this);
            PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Delivery inventory not set up!");
            return;
        }
        if (targetDeliveryInventory.Combiner == null)
         {
             Debug.LogError("ComputerInteractable: Target Delivery Inventory is missing its Combiner component! Cannot deliver items.", this);
             PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Delivery inventory misconfigured!");
             return;
         }


        if (shoppingCart.Count == 0)
        {
             Debug.Log("ComputerInteractable: Shopping cart is empty. Nothing to purchase.");
             PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Your cart is empty!");
             return;
        }

        List<Item> itemsToDeliver = new List<Item>();

        // Iterate through the shopping cart contents
        foreach (KeyValuePair<ItemDetails, int> itemEntry in new Dictionary<ItemDetails, int>(shoppingCart)) // Iterate over a copy
        {
            ItemDetails details = itemEntry.Key;
            int totalQuantityToCreate = itemEntry.Value;

            if (details == null)
            {
                 Debug.LogWarning("ComputerInteractable: Skipping purchase of item with null ItemDetails in cart.");
                 continue;
            }

            Debug.Log($"ComputerInteractable: Preparing to purchase {totalQuantityToCreate}x {details.Name} (Max Stack: {details.maxStack}).");

            // Create Item instances respecting maxStack
            int quantityRemaining = totalQuantityToCreate;

            // If maxStack is 1, create individual instances
            if (details.maxStack == 1)
            {
                for (int i = 0; i < totalQuantityToCreate; i++)
                {
                    itemsToDeliver.Add(details.Create(1));
                     Debug.Log($"ComputerInteractable: Created 1x {details.Name} instance (maxStack 1).");
                }
            }
            else // If maxStack is greater than 1, create full stacks and remaining items
            {
                int maxStack = details.maxStack;

                // Create full stacks
                while (quantityRemaining >= maxStack)
                {
                    itemsToDeliver.Add(details.Create(maxStack));
                    quantityRemaining -= maxStack;
                     Debug.Log($"ComputerInteractable: Created {maxStack}x {details.Name} instance (full stack). Remaining: {quantityRemaining}.");
                }

                // Create remaining items if any
                if (quantityRemaining > 0)
                {
                    itemsToDeliver.Add(details.Create(quantityRemaining));
                     Debug.Log($"ComputerInteractable: Created {quantityRemaining}x {details.Name} instance (remaining).");
                }
            }
        }

        // Add the created Item instances to the delivery inventory
        Debug.Log($"ComputerInteractable: Delivering {itemsToDeliver.Count} item instances to inventory.");

        bool anyFailedToAdd = false;

        foreach (Item itemInstance in itemsToDeliver)
        {
            // Use the public AddItem method on the target Inventory
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
             PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer    ", "Your order has been placed!");
         }
         else
         {
             Debug.LogWarning("ComputerInteractable: Some purchased item instances could not be delivered.", this);
             PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Some items could not be delivered! Inventory might be full.");
         }

        // Clear the shopping cart after attempting to process all items
        shoppingCart.Clear();
        UpdateShoppingCartUI(); // Refresh display

        Debug.Log("ComputerInteractable: Purchase process completed.");
    }

    // --- Public method to reset the interacting state (Called by MenuManager state exit action) ---
    public void ResetInteraction()
    {
        isInteracting = false;
        Debug.Log($"ComputerInteractable ({gameObject.name}): ResetInteraction called. isInteracting is now false.", this);
    }
}
// --- END OF FILE ComputerInteractable.cs ---