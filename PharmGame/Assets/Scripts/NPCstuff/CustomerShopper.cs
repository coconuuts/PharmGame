// --- CustomerShopper.cs ---
using UnityEngine;
using System.Collections.Generic;
using Systems.Inventory;
using System.Linq;
using Random = UnityEngine.Random;

public class CustomerShopper : MonoBehaviour
{
        // Remove or repurpose itemsToBuy list if inventoryNPC is the true cart
        // private List<(ItemDetails details, int quantity)> itemsToBuy = new List<(ItemDetails, int)>(); // Consider removing this list

        [SerializeField] private int minItemsToBuy = 1;
        [SerializeField] private int maxItemsToBuy = 3;
        [SerializeField] private int minQuantityPerItem = 1;
        [SerializeField] private int maxQuantityPerItem = 5;
        [Tooltip("Mean for the normal distribution of item quantities.")]
        [SerializeField] private float quantityMean = 5f;
        [Tooltip("Standard deviation for the normal distribution of item quantities.")]
        [SerializeField] private float quantityStandardDeviation = 1f;

        [Tooltip("Drag the GameObject with the Inventory component here. This is the customer's 'shopping cart'.")]
        [SerializeField] private Inventory inventoryNPC; // This IS the customer's inventory

        public int MinItemsToBuy => minItemsToBuy;
        public int MaxItemsToBuy => maxItemsToBuy;

        private float _gaussianStoredVariate;
        private bool _hasStoredVariate = false;

        // Now these should probably reflect the contents of inventoryNPC
        public int TotalQuantityToBuy
        {
            get
            {
                // Sum the quantities of items in the customer's inventory
                if (inventoryNPC != null && inventoryNPC.InventoryState != null)
                {
                    return inventoryNPC.InventoryState.GetCurrentArrayState().Sum(item => item != null ? item.quantity : 0);
                }
                return 0;
            }
        }

        public int DistinctItemCount
        {
            get
            {
                // Count distinct item types in the customer's inventory
                if (inventoryNPC != null && inventoryNPC.InventoryState != null)
                {
                     return inventoryNPC.InventoryState.GetCurrentArrayState()
                         .Where(item => item != null) // Filter out null items
                         .Select(item => item.details.Id) // Select the unique ID of item details
                         .Distinct() // Get distinct IDs
                         .Count(); // Count them
                }
                return 0;
            }
        }

        // Check if the shopper has items in their inventoryNPC
        public bool HasItems => TotalQuantityToBuy > 0;


        private int consecutiveShelvesWithNoItemsFound = 0;

        void Awake()
        {
            // --- Finding the Inventory Component for the NPC itself ---
            // Ensure inventoryNPC reference is set.
            if (inventoryNPC == null)
            {
                // Try to find the Inventory component on this same GameObject
                inventoryNPC = GetComponent<Inventory>();

                if (inventoryNPC == null)
                {
                    Debug.LogError($"CustomerShopper on {gameObject.name}: Inventory component for the NPC's shopping cart is not assigned and not found on the GameObject!", this);
                    enabled = false; // Cannot function without the NPC's own inventory
                    return;
                }
            }

            // Ensure the inventoryNPC has a Combiner and ObservableArray
             if (inventoryNPC.Combiner == null || inventoryNPC.InventoryState == null)
             {
                  Debug.LogError($"CustomerShopper on {gameObject.name}: NPC's Inventory component is missing required Combiner or ObservableArray. Cannot function.", this);
                  enabled = false;
             }
        }

        public bool SimulateShopping(Inventory shelfInventory) // Renamed parameter for clarity
         {
              bool hasBoughtItemsFromThisShelf = false;

              if (shelfInventory == null)
              {
                  Debug.LogWarning($"CustomerAI ({gameObject.name}): Cannot simulate shopping. Passed shelf inventory is null.", this);
                  return hasBoughtItemsFromThisShelf;
              }
              
              // Ensure the NPC has its own inventory to put items into
              if (inventoryNPC == null || inventoryNPC.Combiner == null)
              {
                  Debug.LogError($"CustomerShopper on {gameObject.name}: NPC's inventory or Combiner is null! Cannot store purchased items.", this);
                  return hasBoughtItemsFromThisShelf; // Cannot buy if nowhere to put items
              }


              Item[] availableItems = shelfInventory.InventoryState.GetCurrentArrayState();

              List<ItemDetails> availableOtcItemDetails = availableItems
                 .Where(item => item != null && item.details != null && item.quantity > 0 && shelfInventory.CanAddItem(item)) // Check if the NPC's inventory *can* accept this item type
                 .Select(item => item.details)
                 .Distinct()
                 .ToList();

              Debug.Log($"CustomerAI ({gameObject.name}): Found {availableOtcItemDetails.Count} distinct available OTC item types in this inventory.");

               // Determine how many *new* item types the customer wants to select from this shelf
               // Consider items already in the customer's inventory when deciding how many *more* types to look for.
              int currentDistinctItemsInCart = DistinctItemCount; // Use the new property
              int numItemTypesToSelect = Random.Range(minItemsToBuy - currentDistinctItemsInCart, maxItemsToBuy - currentDistinctItemsInCart + 1);
              numItemTypesToSelect = Mathf.Clamp(numItemTypesToSelect, 0, availableOtcItemDetails.Count); // Clamp by available items on shelf
              numItemTypesToSelect = Mathf.Max(0, numItemTypesToSelect); // Ensure it's not negative

              if (numItemTypesToSelect <= 0)
              {
                  Debug.Log($"CustomerAI ({gameObject.name}): No new item types selected from this location (already have {currentDistinctItemsInCart} distinct, targeting {minItemsToBuy}-{maxItemsToBuy}).");
                  // Even if no new types are selected, they might stack existing types?
                  // The current logic only picks *new* types. If you want stacking behavior here,
                  // you'd need separate logic to iterate through existing items in inventoryNPC
                  // and see if the shelf has more of those to add.
                  // For now, let's assume SimulateShopping means trying to find *new* types or quantities.
                  // If no new types are selected, check if any quantity was added via stacking attempts below (unlikely with current loop structure).
                  return hasBoughtItemsFromThisShelf; // Nothing selected from this shelf
              }

              List<ItemDetails> selectedItemTypes = availableOtcItemDetails.OrderBy(x => Random.value).Take(numItemTypesToSelect).ToList();

              Combiner shelfCombiner = shelfInventory.GetComponent<Combiner>();

              foreach(var itemDetails in selectedItemTypes)
              {
                   float rawQuantity = GenerateGaussian(quantityMean, quantityStandardDeviation);
                   int desiredQuantity = Mathf.RoundToInt(rawQuantity);
                   desiredQuantity = Mathf.Clamp(desiredQuantity, minQuantityPerItem, maxQuantityPerItem);

                   Debug.Log($"CustomerAI ({gameObject.name}): Trying to buy {desiredQuantity} of {itemDetails.Name}.");


                    if (shelfCombiner != null)
                   {
                        // Attempt to remove the desired quantity from the shelf
                       int actualQuantityRemoved = shelfCombiner.TryRemoveQuantity(itemDetails, desiredQuantity);

                       if (actualQuantityRemoved > 0)
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Successfully removed {actualQuantityRemoved} of {itemDetails.Name} from shelf.");
                            hasBoughtItemsFromThisShelf = true;

                            // --- Add the items to the customer's inventory (inventoryNPC) ---
                            // Create a new Item instance with the ACTUAL quantity removed from the shelf
                            Item purchasedItemInstance = itemDetails.Create(actualQuantityRemoved);

                            // Add the purchased item to the customer's inventory using its Combiner
                            // The Combiner should handle stacking if the item type already exists.
                            bool addedToNPCInventory = inventoryNPC.Combiner.AddItem(purchasedItemInstance);

                            if (addedToNPCInventory)
                            {
                                Debug.Log($"CustomerAI ({gameObject.name}): Successfully added {actualQuantityRemoved} of {itemDetails.Name} to NPC inventory.");
                                // Removed the itemsToBuy list manipulation here.
                                // The customer's inventory (inventoryNPC) now reflects what they "bought".
                            }
                            else
                            {
                                // This case is tricky - removed from shelf but couldn't add to NPC?
                                // This shouldn't happen if CanAddItem was checked, but defensive logging.
                                Debug.LogError($"CustomerShopper on {gameObject.name}: Failed to add {actualQuantityRemoved} of {itemDetails.Name} to NPC inventory after removing from shelf!", this);
                                // TODO: Decide recovery - maybe put item back on shelf?
                            }
                            // -----------------------------------------------------------
                       }
                       else
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Could not buy {desiredQuantity} of {itemDetails.Name} (none available or remove failed) from shelf.");
                       }
                   }
                    else
                   {
                       Debug.LogError($"CustomerAI ({gameObject.name}): Shelf Inventory '{shelfInventory.gameObject.name}' is missing Combiner component! Cannot simulate shopping.", shelfInventory);
                   }
              }

               // After attempting to buy from this shelf, check the total quantity collected
              Debug.Log($"CustomerAI ({gameObject.name}): Finished shopping simulation on this shelf. Items collected so far (in NPC Inventory): {DistinctItemCount} distinct types, Total Quantity: {TotalQuantityToBuy}.");

              // Check if *any* items were bought from *this specific shelf* to reset the counter
              if (hasBoughtItemsFromThisShelf)
              {
                  ResetConsecutiveShelvesCount(); // Reset if *anything* was bought from this shelf
              }
              else
              {
                  IncrementConsecutiveShelvesCount(); // Increment only if nothing was bought from this shelf
              }

              return hasBoughtItemsFromThisShelf; // Indicate if anything was successfully removed from *this shelf*
         }

         /// <summary>
         /// Increments the counter for consecutive shelves visited where no items were bought.
         /// </summary>
         public void IncrementConsecutiveShelvesCount()
         {
             consecutiveShelvesWithNoItemsFound++;
             Debug.Log($"CustomerShopper: Consecutive shelves with no items count incremented to {consecutiveShelvesWithNoItemsFound}.");
         }
         public void ResetConsecutiveShelvesCount()
         {
             consecutiveShelvesWithNoItemsFound = 0;
             Debug.Log("CustomerShopper: Consecutive shelves with no items counter reset.");
         }

         /// <summary>
         /// Gets the current count of consecutive shelves visited where no items were bought.
         /// </summary>
         public int GetConsecutiveShelvesCount()
         {
             return consecutiveShelvesWithNoItemsFound;
         }

        /// <summary>
        /// Generates a random number from a normal (Gaussian) distribution
        /// using the Box-Muller transform.
        /// </summary>
        private float GenerateGaussian(float mean, float stdDev)
        {
            if (_hasStoredVariate)
            {
                _hasStoredVariate = false;
                return mean + stdDev * _gaussianStoredVariate;
            }

            float u1 = Random.value;
            float u2 = Random.value;

            while (u1 == 0f)
            {
                u1 = Random.value;
            }

            float z0 = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Cos(2.0f * Mathf.PI * u2);
            float z1 = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);

            _gaussianStoredVariate = z1;
            _hasStoredVariate = true;

            return mean + stdDev * z0;
        }

         /// <summary>
         /// Public getter for the list of items the customer intends to buy (i.e., has in their inventory).
         /// Called by the CashRegister system.
         /// </summary>
         /// <returns>A list of (ItemDetails, quantity) pairs derived from the NPC's inventory.</returns>
         public List<(ItemDetails details, int quantity)> GetItemsToBuy()
         {
              // Get the current state of the NPC's inventory array
              if (inventoryNPC != null && inventoryNPC.InventoryState != null)
              {
                  // Filter out null items, select the ItemDetails and quantity, and convert to a list
                  return inventoryNPC.InventoryState.GetCurrentArrayState()
                      .Where(item => item != null)
                      .Select(item => (item.details, item.quantity))
                      .ToList();
              }
             Debug.LogError($"CustomerShopper ({gameObject.name}): Cannot get items to buy, NPC inventory or InventoryState is null!", this);
             return new List<(ItemDetails details, int quantity)>(); // Return empty list if inventory is null
         }

         /// <summary>
         /// Resets the shopper's state, including clearing their inventory.
         /// Called by CustomerAI when initializing from the pool.
         /// </summary>
         public void Reset()
        {
            // --- Clear the NPC's inventory ---
            if (inventoryNPC != null && inventoryNPC.InventoryState != null)
            {
                inventoryNPC.InventoryState.Clear(); // Clear the ObservableArray
                Debug.Log("CustomerShopper: NPC Inventory cleared.");
            }
            else
            {
                 Debug.LogWarning("CustomerShopper: Cannot clear NPC Inventory during Reset, inventoryNPC or InventoryState is null.");
            }
            // ---------------------------------

            // The itemsToBuy list was removed as inventoryNPC is the source of truth

            consecutiveShelvesWithNoItemsFound = 0;
            Debug.Log("CustomerShopper: Reset completed.");
        }
}

// --- CustomerAI.cs (No changes needed in CustomerAI itself for these fixes) ---
// The CustomerAI calls Shopper.GetItemsToBuy() and Shopper.Reset() which are updated above.
// The logic for transitioning states based on shopping progress and inventory status remains the same,
// but it now relies on the updated Shopper properties (TotalQuantityToBuy, DistinctItemCount, HasItems)
// and the GetItemsToBuy method, which correctly reflect the contents of inventoryNPC.

// The CustomerReturningLogic.cs also does not need changes as it simply returns the GameObject to the pool,
// and the reset happens during initialization from the pool.

// The CashRegisterInteractable.cs does not need changes as it correctly calls
// currentWaitingCustomer.Shopper.GetItemsToBuy() and currentWaitingCustomer.OnTransactionCompleted().
// The fix is in how Shopper provides this data.