using UnityEngine;
using System.Collections.Generic; // For List
using Systems.Inventory; // For Item and ItemDetails, Inventory
using System.Linq; // For LINQ methods like Where, Select, Distinct, OrderBy, Take
using Random = UnityEngine.Random; // To ensure you use Unity's Random if needed

public class CustomerShopper : MonoBehaviour
{
        private List<(ItemDetails details, int quantity)> itemsToBuy = new List<(ItemDetails, int)>();
        [SerializeField] private int minItemsToBuy = 1;
        [SerializeField] private int maxItemsToBuy = 3;
        [SerializeField] private int minQuantityPerItem = 1;
        [SerializeField] private int maxQuantityPerItem = 5;
        [Tooltip("Mean for the normal distribution of item quantities.")]
        [SerializeField] private float quantityMean = 5f; // <-- ADD THIS
        [Tooltip("Standard deviation for the normal distribution of item quantities.")]
        [SerializeField] private float quantityStandardDeviation = 1f; // <-- ADD THIS


        [Tooltip("Drag the GameObject with the Inventory component here.")]
        [SerializeField] private Inventory inventoryNPC;

        public int MinItemsToBuy => minItemsToBuy;
        public int MaxItemsToBuy => maxItemsToBuy;

        private float _gaussianStoredVariate; // Stores the second value from Box-Muller
        private bool _hasStoredVariate = false; // Flag to indicate if a value is stored

        // You'll likely need the current count of *distinct* items or the *total quantity*
        // Based on your original code, targetClickCount was Sum(item => item.quantity)
        // Let's add a property for total quantity purchased so far
        public int TotalQuantityToBuy => itemsToBuy.Sum(item => item.quantity);
        // And maybe just the count of distinct items if needed
        public int DistinctItemCount => itemsToBuy.Count;

        // A simple check if the shopper has picked up any items yet
        public bool HasItems => itemsToBuy.Count > 0;

        private int consecutiveShelvesWithNoItemsFound = 0;

        void Awake()
        {
            // --- Finding the Inventory Component ---
            // Check if inventoryComponent is null (it will be for a prefab instance)
            if (inventoryNPC == null)
            {
                // Try to find the GameObject with the tag "NPCInventory"
                GameObject inventoryGameObject = GameObject.FindGameObjectWithTag("NPCInventory");

                if (inventoryGameObject != null)
                {
                    // Get the Inventory component from the found GameObject
                    inventoryNPC = inventoryGameObject.GetComponent<Inventory>();

                    if (inventoryNPC == null)
                    {
                        Debug.LogError("GameObject with tag 'NPCInventory' found, but no Inventory component attached for " + gameObject.name + "!");
                    }
                }
                else
                {
                    Debug.LogError("Inventory GameObject with tag 'NPCInventory' not found in the scene for " + gameObject.name + "!");
                }
            }
        }

        public bool SimulateShopping(Inventory inventory)
         {
              bool hasBoughtItemsFromThisShelf = false;

              if (inventory == null)
              {
                  Debug.LogWarning($"CustomerAI ({gameObject.name}): Cannot simulate shopping. Passed inventory is null.", this);
                  return hasBoughtItemsFromThisShelf;
              }

              Item[] availableItems = inventory.InventoryState.GetCurrentArrayState();

              List<ItemDetails> availableOtcItemDetails = availableItems
                 .Where(item => item != null && item.details != null && item.quantity > 0)
                 .Select(item => item.details)
                 .Distinct()
                 .ToList();

              Debug.Log($"CustomerAI ({gameObject.name}): Found {availableOtcItemDetails.Count} distinct available OTC item types in this inventory.");

              int numItemTypesToSelect = Random.Range(minItemsToBuy - itemsToBuy.Count, maxItemsToBuy - itemsToBuy.Count + 1);
              numItemTypesToSelect = Mathf.Clamp(numItemTypesToSelect, 0, availableOtcItemDetails.Count);
              numItemTypesToSelect = Mathf.Max(0, numItemTypesToSelect);

              if (numItemTypesToSelect <= 0)
              {
                  Debug.Log($"CustomerAI ({gameObject.name}): No new item types selected from this location.");
                  return hasBoughtItemsFromThisShelf;
              }

              List<ItemDetails> selectedItemTypes = availableOtcItemDetails.OrderBy(x => Random.value).Take(numItemTypesToSelect).ToList();

              foreach(var itemDetails in selectedItemTypes)
              {
                   float rawQuantity = GenerateGaussian(quantityMean, quantityStandardDeviation);
                   int desiredQuantity = Mathf.RoundToInt(rawQuantity);
                   desiredQuantity = Mathf.Clamp(desiredQuantity, minQuantityPerItem, maxQuantityPerItem);

                   Debug.Log($"CustomerAI ({gameObject.name}): Trying to buy {desiredQuantity} of {itemDetails.Name}.");

                   Combiner inventoryCombiner = inventory.GetComponent<Combiner>();

                    if (inventoryNPC != null && inventoryNPC.Combiner != null)
                    {
                        Item newItemInstance = itemDetails.Create(desiredQuantity);
                        
                        inventoryNPC.Combiner.AddItem(newItemInstance);
                    }

                   if (inventoryCombiner != null)
                   {
                       int actualQuantityRemoved = inventoryCombiner.TryRemoveQuantity(itemDetails, desiredQuantity);

                       if (actualQuantityRemoved > 0)
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Successfully bought {actualQuantityRemoved} of {itemDetails.Name}.");
                            hasBoughtItemsFromThisShelf = true;
                            var existingPurchase = itemsToBuy.FirstOrDefault(item => item.details == itemDetails);

                            if (existingPurchase.details != null) // Item already in list
                            {
                                 itemsToBuy.Remove(existingPurchase);
                                 itemsToBuy.Add((itemDetails, existingPurchase.quantity + actualQuantityRemoved));
                            }
                            else // First time buying this item type
                            {
                                itemsToBuy.Add((itemDetails, actualQuantityRemoved));
                            }
                       }
                       else
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Could not buy {desiredQuantity} of {itemDetails.Name} (none available or remove failed).");
                       }
                   }
                    else
                   {
                       Debug.LogError($"CustomerAI ({gameObject.name}): Inventory '{inventory.gameObject.name}' is missing Combiner component! Cannot simulate shopping.", inventory);
                   }
              }
              Debug.Log($"CustomerAI ({gameObject.name}): Finished shopping simulation. Items collected so far: {itemsToBuy.Count} distinct types.");
              return hasBoughtItemsFromThisShelf;
         }

         
        /// <summary>
        /// Increments the counter for consecutive shelves visited where no items were bought.
        /// </summary>
        public void IncrementConsecutiveShelvesCount() // <-- ADD THIS METHOD
        {
            consecutiveShelvesWithNoItemsFound++;
            Debug.Log($"CustomerShopper: Consecutive shelves with no items count incremented to {consecutiveShelvesWithNoItemsFound}."); // Add log
        }
        public void ResetConsecutiveShelvesCount()
        {
            consecutiveShelvesWithNoItemsFound = 0;
            Debug.Log("CustomerShopper: Consecutive shelves with no items counter reset.");
        }

        /// <summary>
        /// Gets the current count of consecutive shelves visited where no items were bought.
        /// </summary>
        public int GetConsecutiveShelvesCount() // <-- ADD THIS GETTER METHOD
        {
            return consecutiveShelvesWithNoItemsFound;
        }

        /// <summary>
        /// Generates a random number from a normal (Gaussian) distribution
        /// using the Box-Muller transform.
        /// </summary>
        /// <param name="mean">The mean of the distribution.</param>
        /// <param name="stdDev">The standard deviation of the distribution.</param>
        /// <returns>A random number from the specified normal distribution.</returns>
        private float GenerateGaussian(float mean, float stdDev)
        {
            // Use the stored variate if available
            if (_hasStoredVariate)
            {
                _hasStoredVariate = false;
                return mean + stdDev * _gaussianStoredVariate; // Scale and shift the stored value
            }

            // Otherwise, generate two new uniform random numbers
            float u1 = Random.value; // Uniform random number between 0.0 [inclusive] and 1.0 [inclusive]
            float u2 = Random.value; // Uniform random number between 0.0 [inclusive] and 1.0 [inclusive]

            // Handle edge case where u1 is 0, as log(0) is undefined
            // While Random.value is documented as [0.0, 1.0], log(0) is problematic.
            // A common approach is to add a tiny epsilon or check for 0.
            while (u1 == 0f) // Regenerate if u1 is exactly 0
            {
                u1 = Random.value;
            }


            // Apply the Box-Muller transform
            float z0 = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Cos(2.0f * Mathf.PI * u2);
            float z1 = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);

            // Store the second variate (z1) for the next call
            _gaussianStoredVariate = z1;
            _hasStoredVariate = true;

            // Return the first variate (z0), scaled and shifted
            return mean + stdDev * z0;
        }
                        
         /// <summary>
         /// Public getter for the list of items the customer intends to buy.
         /// Called by the CashRegister system in Phase 3.
         /// </summary>
         /// <returns>A list of (ItemDetails, quantity) pairs.</returns>
         public List<(ItemDetails details, int quantity)> GetItemsToBuy()
         {
             return itemsToBuy; // Return the collected list
         }

         public void Reset()
        {
            itemsToBuy.Clear();
            consecutiveShelvesWithNoItemsFound = 0;
            Debug.Log("CustomerShopper: Resetting items to buy.");
        }
}
