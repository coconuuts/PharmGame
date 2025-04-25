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

        public int MinItemsToBuy => minItemsToBuy;
        public int MaxItemsToBuy => maxItemsToBuy;
        // You'll likely need the current count of *distinct* items or the *total quantity*
        // Based on your original code, targetClickCount was Sum(item => item.quantity)
        // Let's add a property for total quantity purchased so far
        public int TotalQuantityToBuy => itemsToBuy.Sum(item => item.quantity);
        // And maybe just the count of distinct items if needed
        public int DistinctItemCount => itemsToBuy.Count;

        // A simple check if the shopper has picked up any items yet
        public bool HasItems => itemsToBuy.Count > 0;

        public void SimulateShopping(Inventory inventory)
         {
              if (inventory == null)
              {
                  Debug.LogWarning($"CustomerAI ({gameObject.name}): Cannot simulate shopping. Passed inventory is null.", this);
                  return;
              }

              Item[] availableItems = inventory.InventoryState.GetCurrentArrayState();

              List<ItemDetails> availableOtcItemDetails = availableItems
                 .Where(item => item != null && item.details != null && item.quantity > 0 && item.details.isOverTheCounter)
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
                  return; // Don't proceed if no items are selected
              }

              List<ItemDetails> selectedItemTypes = availableOtcItemDetails.OrderBy(x => Random.value).Take(numItemTypesToSelect).ToList();

              foreach(var itemDetails in selectedItemTypes)
              {
                   int desiredQuantity = Random.Range(minQuantityPerItem, maxQuantityPerItem + 1);
                   Debug.Log($"CustomerAI ({gameObject.name}): Trying to buy {desiredQuantity} of {itemDetails.Name}.");

                   Combiner inventoryCombiner = inventory.GetComponent<Combiner>();

                   if (inventoryCombiner != null)
                   {
                       int actualQuantityRemoved = inventoryCombiner.TryRemoveQuantity(itemDetails, desiredQuantity);

                       if (actualQuantityRemoved > 0)
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Successfully bought {actualQuantityRemoved} of {itemDetails.Name}.");
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
            Debug.Log("CustomerShopper: Resetting items to buy.");
        }
}
