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
                    // Sum quantities for stackable items, count instances for non-stackable (quantity should be 1)
                    return inventoryNPC.InventoryState.GetCurrentArrayState().Sum(item => item != null ? (item.details.maxStack > 1 ? item.quantity : 1) : 0); // *** MODIFIED SUM LOGIC ***
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
                         .Where(item => item != null && item.details != null) // Filter out null items and items with null details
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
             if (inventoryNPC.Combiner == null || inventoryNPC.InventoryState == null) // Check for Combiner as InventoryState is accessed via it
             {
                  Debug.LogError($"CustomerShopper on {gameObject.name}: NPC's Inventory component '{inventoryNPC.gameObject.name}' is missing required Combiner or its InventoryState is null. Cannot function.", this);
                  enabled = false;
             }
        }

        public bool SimulateShopping(Inventory shelfInventory) // Renamed parameter for clarity
         {
              bool hasBoughtItemsFromThisShelf = false;

              if (shelfInventory == null || shelfInventory.Combiner == null) // Check shelfInventory and its Combiner
              {
                  Debug.LogWarning($"CustomerAI ({gameObject.name}): Cannot simulate shopping. Passed shelf inventory is null or missing Combiner.", this);
                  return hasBoughtItemsFromThisShelf;
              }

              // Ensure the NPC has its own inventory to put items into
              // This check is also in Awake, but good defensive check before adding
              if (inventoryNPC == null || inventoryNPC.Combiner == null)
              {
                  Debug.LogError($"CustomerShopper on {gameObject.name}: NPC's inventory or Combiner is null! Cannot store purchased items.", this);
                  return hasBoughtItemsFromThisShelf; // Cannot buy if nowhere to put items
              }


              Item[] availableItems = shelfInventory.InventoryState.GetCurrentArrayState();

              // Filter for items on the shelf that the NPC's inventory *can* accept
              List<ItemDetails> availableOtcItemDetails = availableItems
                 .Where(item => item != null && item.details != null && item.quantity > 0 && inventoryNPC.CanAddItem(item)) // Use inventoryNPC.CanAddItem
                 .Select(item => item.details)
                 .Distinct()
                 .ToList();

              Debug.Log($"CustomerAI ({gameObject.name}): Found {availableOtcItemDetails.Count} distinct available OTC item types in shelf inventory '{shelfInventory.Id}' that NPC can accept.");

               // Determine how many *new* item types the customer wants to select from this shelf
               // Consider items already in the customer's inventory when deciding how many *more* types to look for.
              int currentDistinctItemsInCart = DistinctItemCount; // Use the new property
              int numItemTypesToSelect = Random.Range(minItemsToBuy - currentDistinctItemsInCart, maxItemsToBuy - currentDistinctItemsInCart + 1);
              numItemTypesToSelect = Mathf.Clamp(numItemTypesToSelect, 0, availableOtcItemDetails.Count); // Clamp by available items on shelf
              numItemTypesToSelect = Mathf.Max(0, numItemTypesToSelect); // Ensure it's not negative

              if (numItemTypesToSelect <= 0)
              {
                  Debug.Log($"CustomerAI ({gameObject.name}): No new item types selected from this location (already have {currentDistinctItemsInCart} distinct, targeting {minItemsToBuy}-{maxItemsToBuy}).");
                  // Even if no new types are selected, they might still want more quantity of items they already have?
                  // The current logic does not handle NPC customers wanting *more quantity* of items they already possess from *any* shelf.
                  // It only looks for new types up to maxItemsToBuy distinct types.
                  // If you want stacking behavior, this loop structure needs rethinking.
                  // For now, returning false if no *new types* are selected aligns with the current logic.
                  return hasBoughtItemsFromThisShelf;
              }

              List<ItemDetails> selectedItemTypes = availableOtcItemDetails.OrderBy(x => Random.value).Take(numItemTypesToSelect).ToList();

              // Use shelfInventory directly to access its Combiner
              Combiner shelfCombiner = shelfInventory.Combiner; // *** MODIFIED ***


              foreach(var itemDetails in selectedItemTypes)
              {
                   // --- Determine desired quantity based on item type ---
                   int desiredQuantity;
                   if (itemDetails.maxStack == 1)
                   {
                       desiredQuantity = 1; // Always desire 1 instance of a non-stackable item
                       Debug.Log($"CustomerAI ({gameObject.name}): Desiring 1 instance of non-stackable item {itemDetails.Name}.");
                   }
                   else
                   {
                       // For stackable, generate quantity based on Gaussian distribution
                       float rawQuantity = GenerateGaussian(quantityMean, quantityStandardDeviation);
                       desiredQuantity = Mathf.RoundToInt(rawQuantity);
                       desiredQuantity = Mathf.Clamp(desiredQuantity, minQuantityPerItem, maxQuantityPerItem);
                       Debug.Log($"CustomerAI ({gameObject.name}): Desiring {desiredQuantity} quantity of stackable item {itemDetails.Name}.");
                   }
                   // --- End determine desired quantity ---


                    if (shelfCombiner != null)
                   {
                        // Attempt to remove the desired quantity from the shelf
                        // TryRemoveQuantity works for both stackable (removes quantity) and non-stackable (removes instances)
                       int actualQuantityRemoved = shelfCombiner.TryRemoveQuantity(itemDetails, desiredQuantity);

                       if (actualQuantityRemoved > 0)
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Successfully removed {actualQuantityRemoved} of {itemDetails.Name} from shelf.");
                            hasBoughtItemsFromThisShelf = true;

                            // --- Add the items to the customer's inventory (inventoryNPC) ---
                            // Create a new Item instance with the ACTUAL quantity removed from the shelf
                            // This instance will be added to the NPC's inventory.
                            Item purchasedItemInstance = itemDetails.Create(actualQuantityRemoved);

                            // Add the purchased item to the customer's inventory using the public AddItem method on Inventory
                            // inventoryNPC.AddItem will handle whether it's stackable or non-stackable and call the appropriate Combiner method.
                            bool addedToNPCInventory = inventoryNPC.AddItem(purchasedItemInstance); // *** MODIFIED ***

                            if (addedToNPCInventory)
                            {
                                // Log success message including remaining quantity on the instance (should be 0 if fully added)
                                Debug.Log($"CustomerAI ({gameObject.name}): Successfully added {actualQuantityRemoved} of {itemDetails.Name} to NPC inventory. Remaining on instance: {purchasedItemInstance.quantity}.");
                                // The customer's inventory (inventoryNPC) now reflects what they "bought".
                            }
                            else
                            {
                                // This case is tricky - removed from shelf but couldn't add to NPC?
                                // This shouldn't happen if inventoryNPC.CanAddItem was checked, but defensive logging.
                                // The purchasedItemInstance.quantity reflects what couldn't be added.
                                Debug.LogError($"CustomerShopper on {gameObject.name}: Failed to add {actualQuantityRemoved} of {itemDetails.Name} to NPC inventory after removing from shelf! Remaining on instance: {purchasedItemInstance.quantity}.", this);
                                // TODO: Decide recovery - maybe try to put item back on shelf?
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
                       // This case should be caught by the initial check for shelfInventory.Combiner == null
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
                  // Filter out null items and items with null details
                  // Select the ItemDetails and the quantity (which is 1 for non-stackable instances)
                  // Note: This creates pairs (ItemDetails, count). For stackable, count is quantity.
                  // For non-stackable, count is always 1 per instance. This is what the CashRegister needs.
                  return inventoryNPC.InventoryState.GetCurrentArrayState()
                      .Where(item => item != null && item.details != null)
                      .Select(item => (item.details, item.details.maxStack > 1 ? item.quantity : 1)) // *** MODIFIED SELECT LOGIC ***
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