// Systems/Crafting/CraftingItemModifier.cs

using UnityEngine;
using Systems.Inventory; // Still needed for Item, ItemDetails, Combiner, AmountType, RecipeInput, RecipeOutput
using System.Collections.Generic; // Needed for Dictionary (if used)
// Explicitly using the Inventory type with its full namespace to avoid conflict

namespace Systems.Crafting // Place in a suitable namespace for crafting logic
{
    /// <summary>
    /// Static helper class containing methods specifically for modifying inventory items
    /// during the crafting process, particularly handling health-based consumption/production
    /// for prescription recipes.
    /// </summary>
    public static class CraftingItemModifier
    {
        static CraftingItemModifier()
        {
            // Static constructor for initialization if needed (optional)
            Debug.Log("CraftingItemModifier static class initialized.");
        }

        /// <summary>
        /// Consumes a specific amount of health from the primary input item instance.
        /// This is used for durable items like stock containers in prescription crafting.
        /// Delegates to the Inventory's method.
        /// </summary>
        /// <param name="primaryInputInventory">The inventory containing the primary input item.</param>
        /// <param name="primaryInputItem">The specific Item instance of the primary input.</param>
        /// <param name="healthToConsume">The amount of health to deduct (typically from the prescription order).</param>
        /// <returns>True if the item was found and health was reduced (or item removed), false otherwise.</returns>
        public static bool ConsumePrimaryInputHealth(Systems.Inventory.Inventory primaryInputInventory, Item primaryInputItem, int healthToConsume) // <-- Using fully qualified name
        {
            if (primaryInputInventory == null)
            {
                Debug.LogError("CraftingItemModifier: ConsumePrimaryInputHealth called with null primaryInputInventory.");
                return false;
            }
            if (primaryInputItem == null || primaryInputItem.details == null)
            {
                Debug.LogWarning("CraftingItemModifier: ConsumePrimaryInputHealth called with null or detail-less primaryInputItem.");
                return false;
            }
            if (healthToConsume <= 0)
            {
                Debug.LogWarning($"CraftingItemModifier: ConsumePrimaryInputHealth called with non-positive healthToConsume ({healthToConsume}).");
                return false;
            }

            // Optional: Robustness check - ensure it's a non-stackable durable item before attempting health reduction
            if (primaryInputItem.details.maxStack > 1 || primaryInputItem.details.maxHealth <= 0)
            {
                 Debug.LogError($"CraftingItemModifier: ConsumePrimaryInputHealth called for item '{primaryInputItem.details.Name}' (ID: {primaryInputItem.Id}) which is not a non-stackable durable item. Aborting consumption.", primaryInputInventory.gameObject);
                 return false;
            }

            Debug.Log($"CraftingItemModifier: Attempting to consume {healthToConsume} health from primary input item '{primaryInputItem.details.Name}' (ID: {primaryInputItem.Id}) in inventory '{primaryInputInventory.Id}'.", primaryInputInventory.gameObject);

            // Delegate the actual health reduction to the Inventory's wrapper method
            bool success = primaryInputInventory.ReduceItemHealth(primaryInputItem, healthToConsume);

            if (success)
            {
                Debug.Log($"CraftingItemModifier: Successfully consumed {healthToConsume} health from primary input. Item new health: {primaryInputItem.health}.");
            }
            else
            {
                Debug.LogError($"CraftingItemModifier: Failed to consume health from primary input item '{primaryInputItem.details.Name}' (ID: {primaryInputItem.Id}). Item not found or other issue.", primaryInputInventory.gameObject);
            }

            return success;
        }

        /// <summary>
        /// Creates a new Item instance representing the crafted output, specifically setting its initial health.
        /// This is used for durable output items like prepared prescriptions.
        /// --- MODIFIED: Added patientNameTag parameter. --- // <-- ADDED NOTE
        /// </summary>
        /// <param name="outputItemDetails">The ItemDetails of the crafted output item type.</param>
        /// <param name="targetHealth">The desired initial health for the new item instance (typically from the prescription order).</param>
        /// <param name="patientNameTag">The patient name to tag this item instance with (from the active prescription order).</param> // <-- ADDED PARAM
        /// <returns>A new Item instance with the specified details, initial health, and patient name tag, or null if details are invalid.</returns>
        public static Item PrepareCraftedOutput(ItemDetails outputItemDetails, int targetHealth, string patientNameTag) // <-- ADDED PARAM
        {
            if (outputItemDetails == null)
            {
                Debug.LogError("CraftingItemModifier: PrepareCraftedOutput called with null outputItemDetails.");
                return null;
            }

            // Optional: Robustness check - ensure the output item type is intended to be durable
            // This method is specifically for AmountType.Health outputs, so it *should* be a durable non-stackable.
            if (outputItemDetails.maxStack > 1 || outputItemDetails.maxHealth <= 0)
            {
                 Debug.LogWarning($"CraftingItemModifier: PrepareCraftedOutput called for item type '{outputItemDetails.Name}' which is not a non-stackable durable item type as expected for AmountType.Health. Creating instance with quantity 1 instead of setting health.", outputItemDetails);
                 // Fallback: Create a single instance with quantity 1 if it's not a durable type intended for health
                 Item fallbackItem = outputItemDetails.Create(1);
                 // Still assign the tag, even if it's not the expected type, for consistency
                 if (fallbackItem != null) fallbackItem.patientNameTag = patientNameTag; // <-- ASSIGN TAG ON FALLBACK
                 return fallbackItem; // Use the standard Create method
            }

            // --- Corrected Logic: Create the instance, then set its specific crafting health ---
            // Create the basic instance using the standard Create method.
            // For non-stackable items (maxStack == 1), Create(int quantity) results in Item.quantity = 1
            // and Item.health initialized from ItemDetails.maxHealth.
            Item craftedItemInstance = outputItemDetails.Create(1); // Pass 1 for quantity for non-stackable instance

            if (craftedItemInstance != null)
            {
                 // Now, set the health of THIS SPECIFIC INSTANCE to the targetHealth from the prescription order.
                 // The SetHealth method handles clamping the value between 0 and details.maxHealth,
                 // and also updates gun-specific health fields if applicable.
                 craftedItemInstance.SetHealth(targetHealth); // <-- Set the correct health here

                 // --- NEW: Assign the patient name tag --- // <-- ADDED
                 craftedItemInstance.patientNameTag = patientNameTag; // <-- ASSIGN THE TAG
                 // --- END NEW ---

                 // Log the health *after* setting it to confirm the value
                 Debug.Log($"CraftingItemModifier: Prepared crafted output item '{craftedItemInstance.details.Name}' (ID: {craftedItemInstance.Id}) and set initial health to {craftedItemInstance.health} (Target: {targetHealth}). Assigned Patient Tag: '{patientNameTag ?? "NULL"}'.", craftedItemInstance.details); // <-- MODIFIED LOG
            }
            else
            {
                 Debug.LogError($"CraftingItemModifier: Failed to create crafted output item instance for '{outputItemDetails.Name}'.", outputItemDetails);
            }

            return craftedItemInstance;
        }

        /// <summary>
        /// Consumes a specific quantity of a secondary input item type from the specified inventory.
        /// This is used for quantity-based items like container vials in prescription crafting.
        /// Delegates to the Inventory's method.
        /// </summary>
        /// <param name="inputInventory">The inventory containing the secondary input item.</param>
        /// <param name="inputItemDetails">The ItemDetails of the secondary input item type to consume.</param>
        /// <param name="quantityToConsume">The total quantity to deduct (recipe amount per batch * batches).</param>
        /// <returns>True if the full quantity was successfully removed, false otherwise.</returns>
        public static bool ConsumeSecondaryInputQuantity(Systems.Inventory.Inventory inputInventory, ItemDetails inputItemDetails, int quantityToConsume) // <-- Using fully qualified name
        {
            if (inputInventory == null)
            {
                Debug.LogError("CraftingItemModifier: ConsumeSecondaryInputQuantity called with null inputInventory.");
                return false;
            }
            if (inputItemDetails == null)
            {
                Debug.LogWarning("CraftingItemModifier: ConsumeSecondaryInputQuantity called with null inputItemDetails.");
                return false;
            }
            if (quantityToConsume <= 0)
            {
                Debug.LogWarning($"CraftingItemModifier: ConsumeSecondaryInputQuantity called with non-positive quantityToConsume ({quantityToConsume}).");
                return false;
            }

             // Optional: Robustness check - ensure it's a stackable item before attempting quantity reduction
             // Note: Secondary inputs *could* be non-stackable quantity=1 items (like a single tool consumed).
             // Inventory.TryRemoveQuantity handles both, so this check isn't strictly necessary for function,
             // but helps ensure the recipe definition matches the intended usage.
             // For now, let's rely on TryRemoveQuantity handling both.

            Debug.Log($"CraftingItemModifier: Attempting to consume {quantityToConsume} quantity of secondary input item '{inputItemDetails.Name}' in inventory '{inputInventory.Id}'.", inputInventory.gameObject);

            // Delegate the actual quantity removal to the Inventory's method
            int quantityRemoved = inputInventory.TryRemoveQuantity(inputItemDetails, quantityToConsume);

            bool success = (quantityRemoved == quantityToConsume);

            if (success)
            {
                Debug.Log($"CraftingItemModifier: Successfully consumed {quantityRemoved} quantity of secondary input.");
            }
            else
            {
                Debug.LogError($"CraftingItemModifier: Failed to consume full quantity of secondary input item '{inputItemDetails.Name}'. Needed {quantityToConsume}, removed {quantityRemoved}.", inputInventory.gameObject);
            }

            return success; // Return true only if the full requested quantity was removed
        }
    }
}   