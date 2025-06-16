// --- START OF FILE ComputerInteractable.cs ---

using UnityEngine;
using UnityEngine.UI;
using System.Collections; // Keep if you have other coroutines, otherwise remove
using System.Collections.Generic;
using System.Text;
using TMPro;
using Systems.Inventory; // Needed for ItemDetails and Inventory
using Systems.Interaction; // ADD THIS USING
using Systems.UI; // Needed for PlayerUIPopups

public class ComputerInteractable : MonoBehaviour, IInteractable
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

    [Header("Prompt Settings")]
    public Vector3 computerTextPromptOffset = Vector3.zero;
    public Vector3 computerTextPromptRotationOffset = Vector3.zero;

    public string InteractionPrompt => interactionPrompt;

    // The purpose of isInteracting needs re-evaluation now that state/UI is external.
    // If it's just to prevent re-interacting while already using *this* computer, keep it and reset elsewhere.
    // If MenuManager handles entering/exiting computer state, its state check might be sufficient.
    // For now, let's keep it but its state management connection is removed.
    private bool isInteracting = false; // Tracks if *this* computer is conceptually being used

    // --- Shopping Cart and UI Logic ---
    [Header("Computer UI References")]
    [Tooltip("The root GameObject containing all the computer screen UI elements.")]
    [SerializeField] private GameObject computerUIContainer;

    [Tooltip("Button for the first medicine item.")]
    [SerializeField] private Button item1Button;

    [Tooltip("Button for the second medicine item.")]
    [SerializeField] private Button item2Button;

    [Tooltip("Text component to display the shopping cart contents.")]
    [SerializeField] private TextMeshProUGUI shoppingCartText;

    [Tooltip("Button to process the purchase.")]
    [SerializeField] private Button buyButton;

    // --- Item Details and Delivery Inventory ---
    [Header("Inventory Integration")]
    [Tooltip("The ItemDetails ScriptableObject for the first purchasable item.")]
    [SerializeField] private ItemDetails item1Details;

    [Tooltip("The ItemDetails ScriptableObject for the second purchasable item.")]
    [SerializeField] private ItemDetails item2Details;

    [Tooltip("The Inventory component where purchased items will be added (e.g., a delivery truck inventory).")]
    [SerializeField] private Inventory targetDeliveryInventory;


    private Dictionary<ItemDetails, int> shoppingCart = new Dictionary<ItemDetails, int>();

    // --- NEW: Awake Method for Registration ---
    private void Awake()
    {
         // --- NEW: Register with the singleton InteractionManager ---
         if (Systems.Interaction.InteractionManager.Instance != null) // Use full namespace if needed
         {
             Systems.Interaction.InteractionManager.Instance.RegisterInteractable(this);
         }
         else
         {
             // This error is critical as the component won't be managed
             Debug.LogError($"ComputerInteractable on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
             // Optionally disable here if registration is absolutely required for function
             // enabled = false;
         }
         // --- END NEW ---

         // REMOVED: Any manual enabled = false calls from Awake if they existed
         // The InteractionManager handles the initial enabled state.
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

        // Subscribe to button click events
        if (item1Button != null && item1Details != null)
        {
            item1Button.onClick.AddListener(() => AddItemToCart(item1Details));
        }
        else
        {
            if(item1Button == null) Debug.LogWarning("ComputerInteractable: Item 1 Button is not assigned.");
            if(item1Details == null) Debug.LogWarning("ComputerInteractable: Item 1 Details are not assigned. Item 1 button will not work.");
        }

        if (item2Button != null && item2Details != null)
        {
            item2Button.onClick.AddListener(() => AddItemToCart(item2Details));
        }
        else
        {
             if(item2Button == null) Debug.LogWarning("ComputerInteractable: Item 2 Button is not assigned.");
             if(item2Details == null) Debug.LogWarning("ComputerInteractable: Item 2 Details are not assigned. Item 2 button will not work.");
        }

        if(buyButton != null)
        {
            buyButton.onClick.AddListener(ProcessPurchase);
        }
        else
        {
             Debug.LogWarning("ComputerInteractable: Buy Button is not assigned. Purchase functionality will not work.");
        }

        UpdateShoppingCartUI();
    }

    // --- Existing OnDestroy Method ---
    private void OnDestroy()
    {
        // Unsubscribe button events
        if(item1Button != null) item1Button.onClick.RemoveAllListeners();
        if(item2Button != null) item2Button.onClick.RemoveAllListeners();
        if(buyButton != null) buyButton.onClick.RemoveAllListeners();

        // --- NEW: Unregister from the singleton InteractionManager ---
        if (Systems.Interaction.InteractionManager.Instance != null)
        {
             Systems.Interaction.InteractionManager.Instance.UnregisterInteractable(this);
        }
        // --- END NEW ---
    }

    /// <summary>
    /// Runs the object's specific interaction logic and returns a response describing the outcome.
    /// Changed to return InteractionResponse.
    /// </summary>
    /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
    public InteractionResponse Interact() // CHANGED RETURN TYPE
    {
        // Only interact if the game is in the Playing state (MenuManager checks this)
        // We can remove the state check here if PlayerInteractionManager only calls Interact in Playing state.
        // Let's keep the isInteracting check if it's used elsewhere to prevent re-triggering.
        if (isInteracting) // Check if *this* computer is already in use
        {
            Debug.Log("ComputerInteractable: Already interacting with this computer.");
            return null; // Return null response if already interacting
        }

        if (cameraViewPoint == null || computerUIContainer == null) // Check essential data for response
        {
             Debug.LogError("ComputerInteractable: Cannot create EnterComputerResponse - Camera View Point or Computer UI Container not assigned.", this);
             return null;
        }

        Debug.Log("ComputerInteractable: Interact called. Returning EnterComputerResponse.");

        // --- Create and return the response ---
        // The PlayerInteractionManager will receive this and pass it to the MenuManager
        EnterComputerResponse response = new EnterComputerResponse(
            cameraViewPoint,
            cameraMoveDuration,
            computerUIContainer,
            this
        );

        // Mark as interacting *immediately* before returning the response
        // The state exit action in MenuManager will be responsible for clearing this flag.
        isInteracting = true;

        return response;
    }

    // --- Shopping Cart Methods ---

    /// <summary>
    /// Adds one quantity of the specified item details to the shopping cart.
    /// </summary>
    /// <param name="itemDetails">The ItemDetails of the item to add.</param>
    private void AddItemToCart(ItemDetails itemDetails) // CHANGE PARAMETER TYPE
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
        if (shoppingCartText == null)
        {
            Debug.LogError("ComputerInteractable: ShoppingCartText is not assigned!");
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
            foreach (KeyValuePair<ItemDetails, int> itemEntry in new Dictionary<ItemDetails, int>(shoppingCart)) // Iterate using KeyValuePair<ItemDetails, int>
            {
                // Get the item name from the ItemDetails key
                cartDisplay.AppendLine($"{itemEntry.Value}x {itemEntry.Key.Name}"); // Use itemEntry.Key.Name
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

        // We now just need the target Inventory, not its Combiner directly
        if (targetDeliveryInventory == null) // *** MODIFIED CHECK ***
        {
            Debug.LogError("ComputerInteractable: Target Delivery Inventory is not assigned! Cannot deliver items.", this);
            PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Delivery inventory not set up!"); // Added popup
            return;
        }
        // Optional: Check if the Inventory *does* have a Combiner, as AddItem relies on it
        if (targetDeliveryInventory.Combiner == null)
         {
             Debug.LogError("ComputerInteractable: Target Delivery Inventory is missing its Combiner component! Cannot deliver items.", this);
             PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Delivery inventory misconfigured!"); // Added popup
             return;
         }


        if (shoppingCart.Count == 0)
        {
             Debug.Log("ComputerInteractable: Shopping cart is empty. Nothing to purchase.");
             PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Your cart is empty!"); // Added popup
             return;
        }

        // Create a list to hold all the individual Item instances to be added
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

            // --- Create Item instances respecting maxStack ---
            int quantityRemaining = totalQuantityToCreate;

            // If maxStack is 1, create individual instances
            if (details.maxStack == 1) // Check specifically for maxStack == 1 for single instances
            {
                for (int i = 0; i < totalQuantityToCreate; i++)
                {
                    itemsToDeliver.Add(details.Create(1)); // Create item with quantity 1
                     Debug.Log($"ComputerInteractable: Created 1x {details.Name} instance (maxStack 1).");
                }
            }
            else // If maxStack is greater than 1, create full stacks and remaining items
            {
                int maxStack = details.maxStack;

                // Create full stacks
                while (quantityRemaining >= maxStack)
                {
                    itemsToDeliver.Add(details.Create(maxStack)); // Create item with a full stack quantity
                    quantityRemaining -= maxStack;
                     Debug.Log($"ComputerInteractable: Created {maxStack}x {details.Name} instance (full stack). Remaining: {quantityRemaining}.");
                }

                // Create remaining items if any
                if (quantityRemaining > 0)
                {
                    itemsToDeliver.Add(details.Create(quantityRemaining)); // Create item with the remaining quantity
                     Debug.Log($"ComputerInteractable: Created {quantityRemaining}x {details.Name} instance (remaining).");
                }
            }
        }

        // --- Add the created Item instances to the delivery inventory ---
        Debug.Log($"ComputerInteractable: Delivering {itemsToDeliver.Count} item instances to inventory.");

        bool anyFailedToAdd = false; // Flag to track if ANY item failed to add

        foreach (Item itemInstance in itemsToDeliver)
        {
            // Use the public AddItem method on the target Inventory
            // Inventory.AddItem will handle the logic based on itemInstance's maxStack
            bool added = targetDeliveryInventory.AddItem(itemInstance); // *** MODIFIED ***

            if (!added)
            {
                 // If AddItem fails for any instance (inventory full or filtering disallowed), log a warning
                 // The itemInstance.quantity will reflect what couldn't be added (0 if non-stackable, >0 if stackable partial fail)
                 Debug.LogWarning($"ComputerInteractable: Failed to add item instance '{itemInstance.details?.Name ?? "Unknown"}' (Initial Qty: {itemInstance.quantity}) to delivery inventory. Remaining on instance: {itemInstance.quantity}. It might be full or filtering disallowed.", this);
                 anyFailedToAdd = true;
                 // Decide if you want to stop adding if the inventory becomes full mid-delivery
                 // For now, it will continue trying to add remaining items.
            }
             else
             {
                 // Log success message including remaining quantity (should be 0 if fully added)
                  Debug.Log($"ComputerInteractable: Successfully added item instance '{itemInstance.details?.Name ?? "Unknown"}' to delivery inventory. Remaining on instance: {itemInstance.quantity}.");
             }
        }

         // Provide feedback based on whether any items failed to add
         if (!anyFailedToAdd) // If no items failed to add
         {
             Debug.Log("ComputerInteractable: All purchased item instances successfully delivered.");
             PlayerUIPopups.Instance?.ShowPopup("Purchase Complete", "Your order has been placed!"); // Added popup
         }
         else
         {
             Debug.LogWarning("ComputerInteractable: Some purchased item instances could not be delivered.", this);
             PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "Some items could not be delivered! Inventory might be full."); // Added popup
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