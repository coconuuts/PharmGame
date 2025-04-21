using UnityEngine;
using UnityEngine.UI;
using System.Collections; // Keep if you have other coroutines, otherwise remove
using System.Collections.Generic;
using System.Text;
using TMPro;
using Systems.Inventory; // Needed for ItemDetails and Inventory
using Systems.Interaction; // ADD THIS USING

public class ComputerInteractable : MonoBehaviour, IInteractable
{
    [Header("Camera View Point")]
    [Tooltip("The transform the camera should move to when interacting with the computer.")]
    [SerializeField] private Transform cameraViewPoint;

    [Tooltip("The duration of the camera movement animation.")]
    [SerializeField] private float cameraMoveDuration = 0.5f;

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access Computer (E)";

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
            foreach (KeyValuePair<ItemDetails, int> itemEntry in shoppingCart) // Iterate using KeyValuePair<ItemDetails, int>
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

        if (targetDeliveryInventory == null || targetDeliveryInventory.Combiner == null)
        {
            Debug.LogError("ComputerInteractable: Target Delivery Inventory or its Combiner is not assigned or found! Cannot deliver items.", this);
            return;
        }

        if (shoppingCart.Count == 0)
        {
             Debug.Log("ComputerInteractable: Shopping cart is empty. Nothing to purchase.");
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

            // If maxStack is 1 or less, create individual items for each quantity
            if (details.maxStack <= 1)
            {
                for (int i = 0; i < totalQuantityToCreate; i++)
                {
                    itemsToDeliver.Add(details.Create(1)); // Create item with quantity 1
                     Debug.Log($"ComputerInteractable: Created 1x {details.Name} instance (maxStack <= 1).");
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

        bool allAddedSuccessfully = true;
        foreach (Item itemInstance in itemsToDeliver)
        {
            // Use the Combiner's AddItem method for each individual instance
            bool added = targetDeliveryInventory.Combiner.AddItem(itemInstance);

            if (!added)
            {
                 // If AddItem fails for any instance (inventory full), log a warning
                 // You might want more sophisticated error handling here (e.g., refund, partial delivery)
                 Debug.LogWarning($"ComputerInteractable: Failed to add item instance ({itemInstance.details?.Name}, Qty: {itemInstance.quantity}) to delivery inventory. It might be full.", this);
                 allAddedSuccessfully = false;
                 // Decide if you want to stop adding if the inventory becomes full mid-delivery
                 // For now, it will continue trying to add remaining items.
            }
        }

         if (allAddedSuccessfully)
         {
             Debug.Log("ComputerInteractable: All item instances successfully delivered.");
         }
         else
         {
             Debug.LogWarning("ComputerInteractable: Some item instances could not be delivered.", this);
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