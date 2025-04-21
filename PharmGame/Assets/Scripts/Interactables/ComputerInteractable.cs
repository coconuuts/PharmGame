using UnityEngine;
using UnityEngine.UI; // Required for UI components like Text, Button, Image
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for Dictionary
using System.Text; // Required for StringBuilder
using TMPro;
using Systems.Inventory; // ADD THIS USING

public class ComputerInteractable : MonoBehaviour, IInteractable
{
    // --- Existing Camera and Interaction Logic ---
    [SerializeField] private Transform cameraViewPoint;
    [SerializeField] private float cameraMoveDuration = 0.5f;

    private Transform playerCameraTransform;
    private Quaternion originalCameraRotation;
    private Vector3 originalCameraPosition;
    private Coroutine currentCameraMoveCoroutine;

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access Computer (E)";

    [Header("Prompt Settings")]
    public Vector3 computerTextPromptOffset = Vector3.zero;
    public Vector3 computerTextPromptRotationOffset = Vector3.zero;

    public string InteractionPrompt => interactionPrompt;

    private bool isInteracting = false; // Tracks if *this* computer is being interacted with

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
    [SerializeField] private Button buyButton; // ADD THIS FIELD

    // --- Item Details and Delivery Inventory ---
    [Header("Inventory Integration")]
    [Tooltip("The ItemDetails ScriptableObject for the first purchasable item.")]
    [SerializeField] private ItemDetails item1Details; // ADD THIS FIELD

    [Tooltip("The ItemDetails ScriptableObject for the second purchasable item.")]
    [SerializeField] private ItemDetails item2Details; // ADD THIS FIELD

    [Tooltip("The Inventory component where purchased items will be added (e.g., a delivery truck inventory).")]
    [SerializeField] private Inventory targetDeliveryInventory; // ADD THIS FIELD


    // Dictionary to store item details and their quantities
    private Dictionary<ItemDetails, int> shoppingCart = new Dictionary<ItemDetails, int>(); // CHANGE DICTIONARY KEY TYPE

    // Item names are now obtained from ItemDetails

    // --- Existing IInteractable methods ---
    public void ActivatePrompt()
    {
        // Assuming PromptEditor.Instance is available and handles the UI display
         if (PromptEditor.Instance != null) // Added null check
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
         if (PromptEditor.Instance != null) // Added null check
         {
             PromptEditor.Instance.HidePrompt();
         }
    }

    // --- Start Method + New UI Setup ---
    private void Start()
    {
        // Find the player camera by tag
        GameObject playerCameraObject = GameObject.FindGameObjectWithTag("MainCamera");
        if (playerCameraObject != null)
        {
            playerCameraTransform = playerCameraObject.transform;
        }
        else
        {
            Debug.LogError("ComputerInteractable: Could not find GameObject with tag 'MainCamera'. Cannot move camera.", this);
        }

        if (cameraViewPoint == null)
        {
            Debug.LogError("ComputerInteractable: 'Camera View Point' Transform is not assigned!", this);
        }

        // Subscribe to MenuManager state changes
        if (MenuManager.Instance != null)
        {
            MenuManager.OnStateChanged += HandleGameStateChanged;
        }
        else
        {
            Debug.LogError("ComputerInteractable: MenuManager.Instance is null! State changes cannot be handled.", this);
        }

        // Subscribe to button click events
        if (item1Button != null && item1Details != null) // Added null check for itemDetails
        {
             // Use lambda to pass the ItemDetails directly
            item1Button.onClick.AddListener(() => AddItemToCart(item1Details));
        }
        else
        {
            if(item1Button == null) Debug.LogWarning("ComputerInteractable: Item 1 Button is not assigned.");
            if(item1Details == null) Debug.LogWarning("ComputerInteractable: Item 1 Details are not assigned. Item 1 button will not work.");
        }

        if (item2Button != null && item2Details != null) // Added null check for itemDetails
        {
             // Use lambda to pass the ItemDetails directly
            item2Button.onClick.AddListener(() => AddItemToCart(item2Details));
        }
        else
        {
             if(item2Button == null) Debug.LogWarning("ComputerInteractable: Item 2 Button is not assigned.");
             if(item2Details == null) Debug.LogWarning("ComputerInteractable: Item 2 Details are not assigned. Item 2 button will not work.");
        }

        // Link the Buy button click to the ProcessPurchase method
        if(buyButton != null) // Added null check
        {
            buyButton.onClick.AddListener(ProcessPurchase); // Link the method
        }
        else
        {
             Debug.LogWarning("ComputerInteractable: Buy Button is not assigned. Purchase functionality will not work.");
        }


        // Initialize the shopping cart display (should show empty initially)
        UpdateShoppingCartUI();
    }

    // --- Existing OnDestroy Method ---
    private void OnDestroy()
    {
        if (MenuManager.Instance != null)
        {
            MenuManager.OnStateChanged -= HandleGameStateChanged;
        }

        // Ensure we unsubscribe from button events to prevent memory leaks
        if(item1Button != null) item1Button.onClick.RemoveAllListeners();
        if(item2Button != null) item2Button.onClick.RemoveAllListeners();
        if(buyButton != null) buyButton.onClick.RemoveAllListeners(); // Unsubscribe buy button
    }

    // --- Existing Interact Method + State Transition ---
    public void Interact()
    {
        // Only interact if the game is in the Playing state and not already interacting with THIS computer
        if (MenuManager.Instance == null || MenuManager.Instance.currentState != MenuManager.GameState.Playing || isInteracting)
        {
            if(isInteracting) Debug.Log("ComputerInteractable: Already interacting with this computer.");
            else Debug.Log("ComputerInteractable: Cannot interact - not in Playing state.");
            return;
        }

        if (playerCameraTransform == null || cameraViewPoint == null)
        {
            Debug.LogError("ComputerInteractable: Cannot interact - Camera or View Point not assigned.", this);
            return;
        }

        Debug.Log("ComputerInteractable: Interacting with computer!");

        originalCameraPosition = playerCameraTransform.position;
        originalCameraRotation = playerCameraTransform.rotation;

        if (currentCameraMoveCoroutine != null) StopCoroutine(currentCameraMoveCoroutine);
        currentCameraMoveCoroutine = StartCoroutine(MoveCamera(cameraViewPoint.position, cameraViewPoint.rotation, cameraMoveDuration));

        // Tell the MenuManager to enter the *Computer* state
        MenuManager.Instance.SetState(MenuManager.GameState.InComputer); // Use the new state

        // Mark this computer as the one being interacted with
        isInteracting = true;

        // Cursor state is handled by MenuManager state entry
    }

    // --- Existing HandleGameStateChanged Method + New UI Deactivation ---
    private void HandleGameStateChanged(MenuManager.GameState newState)
    {
        // If we were interacting with THIS computer and the state is changing back to Playing
        if (isInteracting && newState == MenuManager.GameState.Playing)
        {
            Debug.Log("ComputerInteractable: State changed back to Playing. Moving camera back.");
            // Stop any existing camera movement coroutine (in case escape was pressed mid-move)
            if (currentCameraMoveCoroutine != null)
            {
                StopCoroutine(currentCameraMoveCoroutine);
            }

            // Start the coroutine to move the camera back to its original position
            currentCameraMoveCoroutine = StartCoroutine(MoveCamera(originalCameraPosition, originalCameraRotation, cameraMoveDuration));

            // We are no longer interacting with this computer
            isInteracting = false;

             // Restore cursor state (assuming you lock it during gameplay)
             Cursor.visible = false;
             Cursor.lockState = CursorLockMode.Locked;


        }
         else if (isInteracting && newState != MenuManager.GameState.InComputer)
        {
             // If we were interacting with this computer, but the state changed to something OTHER than Playing or InComputer
             // (e.g., Pause Menu, or perhaps another interactable forced a state change unexpectedly),
             // treat this as exiting the computer interaction.
             Debug.Log($"ComputerInteractable: State changed from InComputer to {newState}. Treating as exit, moving camera back.");

             if (currentCameraMoveCoroutine != null)
             {
                 StopCoroutine(currentCameraMoveCoroutine);
             }

             currentCameraMoveCoroutine = StartCoroutine(MoveCamera(originalCameraPosition, originalCameraRotation, cameraMoveDuration));

             isInteracting = false;

              // Restore cursor state
              Cursor.visible = false;
              Cursor.lockState = CursorLockMode.Locked;
         }
    }

    // --- Existing Coroutine ---
    private IEnumerator MoveCamera(Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
         // ... (Coroutine remains the same)
        float elapsed = 0f;
        Vector3 startPosition = playerCameraTransform.position;
        Quaternion startRotation = playerCameraTransform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            playerCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            playerCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        playerCameraTransform.position = targetPosition;
        playerCameraTransform.rotation = targetRotation;

        currentCameraMoveCoroutine = null;
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
}