using System.Collections.Generic;
using UnityEngine;
using Systems.Inventory; // Ensure this namespace matches your scripts
using Systems.Crafting; // Needed for CraftingItemModifier
using System.Linq; // Needed for Linq

namespace Systems.Inventory
{
    /// <summary>
    /// Helper class containing static methods to execute the crafting process
    /// (consume inputs, produce outputs) for a matched recipe and batch count.
    /// --- MODIFIED: Now uses the actual crafted amount from the minigame for health-based items. ---
    /// </summary>
    public static class CraftingExecutor
    {
        /// <summary>
        /// Executes the crafting process: consumes required inputs and produces outputs.
        /// Handles quantity-based inputs/outputs and health-based primary inputs/outputs
        /// for prescription recipes based on the actual amount crafted by the minigame.
        /// </summary>
        /// <param name="recipeToCraft">The recipe to craft.</param>
        /// <param name="batchesToCraft">The number of batches to craft (should be 1 for prescription recipes).</param>
        /// <param name="primaryInputInventory">The primary input inventory.</param>
        /// <param name="secondaryInputInventory">The secondary input inventory (can be null).</param>
        /// <param name="outputInventory">The output inventory.</param>
        /// <param name="totalPrescriptionUnits">The total units required by the prescription order (needed for delivery validation later). Should be 0 for non-prescription recipes.</param>
        /// <param name="actualCraftedAmount">The actual number of units/amount successfully crafted by the minigame (used for health consumption/production).</param>
        /// <returns>True if input consumption was successful, false otherwise. Output production success is not guaranteed by this return value.</returns>
        public static bool ExecuteCraft(
            CraftingRecipe recipeToCraft,
            int batchesToCraft,
            Inventory primaryInputInventory,
            Inventory secondaryInputInventory,
            Inventory outputInventory,
            int totalPrescriptionUnits, // <-- Parameter for required units (for delivery validation later)
            int actualCraftedAmount) // <-- Parameter for actual crafted amount (for consumption/production)
        {
            if (recipeToCraft == null || batchesToCraft <= 0)
            {
                Debug.LogError("CraftingExecutor: ExecuteCraft called with invalid recipe or batch count.");
                return false; // Indicate failure due to invalid input
            }

            // Check if required inventories are assigned and have Combiners (as Inventory.AddItem relies on Combiner)
            if (primaryInputInventory == null || primaryInputInventory.Combiner == null || outputInventory == null || outputInventory.Combiner == null)
            {
                if (primaryInputInventory == null || primaryInputInventory.Combiner == null) Debug.LogError($"CraftingExecutor: Cannot execute craft, primary input inventory is null or missing Combiner ({(primaryInputInventory != null ? primaryInputInventory.gameObject.name : "NULL")}).");
                if (outputInventory == null || outputInventory.Combiner == null) Debug.LogError($"CraftingExecutor: Cannot execute craft, output inventory is null or missing Combiner ({(outputInventory != null ? outputInventory.gameObject.name : "NULL")}).");
                return false; // Indicate failure due to missing inventories/combiners
            }

            if (secondaryInputInventory != null && secondaryInputInventory.Combiner == null)
            {
                 Debug.LogError($"CraftingExecutor: Secondary input inventory is assigned but its Combiner is null ({(secondaryInputInventory != null ? secondaryInputInventory.gameObject.name : "NULL")}). Cannot execute craft.");
                 // Decide if this is a hard fail or soft fail. Let's make it a hard fail for consumption logic safety.
                 return false; // Indicate failure due to missing secondary inventory combiner
            }

            Debug.Log($"CraftingExecutor: Executing craft for recipe '{recipeToCraft.recipeName}' x {batchesToCraft} batches. Required Units: {totalPrescriptionUnits}, Actual Crafted Amount: {actualCraftedAmount}.", primaryInputInventory.gameObject);


            // --- Consume Inputs (based on batchesToCraft for quantity, actualCraftedAmount for health) ---
            bool consumptionSuccess = true;

            // Find the primary input item instance first, as it's needed for health consumption
            // Assuming the primary input item is in the first physical slot of the primary input inventory
            // This aligns with the vision of a dedicated primary input slot.
            Item primaryInputItemInstance = null;
            RecipeInput primaryInputRequirement = recipeToCraft.inputs.FirstOrDefault(input => input.isPrimaryInput);

            if (primaryInputRequirement != null)
            {
                 Item[] primaryItems = primaryInputInventory.InventoryState.GetCurrentArrayState();
                 // Check if the first physical slot exists and contains an item matching the primary requirement details
                 if (primaryInputInventory.Combiner.PhysicalSlotCount > 0 && primaryItems != null && primaryItems.Length > 0 && primaryItems[0] != null && primaryItems[0].details == primaryInputRequirement.itemDetails)
                 {
                      primaryInputItemInstance = primaryItems[0];
                      // Corrected context to primaryInputInventory.gameObject
                      Debug.Log($"CraftingExecutor: Identified primary input item instance '{primaryInputItemInstance.details.Name}' (ID: {primaryInputItemInstance.Id}) in slot 0 of primary inventory.", primaryInputInventory.gameObject);
                 }
                 else
                 {
                      // This should have been caught by CraftingMatcher/CraftingStation validation, but defensive check.
                      Debug.LogError($"CraftingExecutor: Primary input item '{primaryInputRequirement.itemDetails?.Name ?? "NULL"}' required by recipe '{recipeToCraft.recipeName}' not found in expected primary input slot (index 0). Cannot consume health.", primaryInputInventory.gameObject);
                      consumptionSuccess = false; // Cannot proceed without the primary item
                 }
            }
            // Note: If primaryInputRequirement is null, this is a pure quantity recipe, and primaryInputItemInstance remains null.

            if (consumptionSuccess) // Only proceed with consumption if primary item was found (if required)
            {
                foreach (var requiredInput in recipeToCraft.inputs)
                {
                    if (requiredInput?.itemDetails == null)
                    {
                        Debug.LogError($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has a required input with null ItemDetails. Aborting consumption.");
                        consumptionSuccess = false;
                        break; // Cannot proceed if input is invalid
                    }
                    // Note: requiredInput.amount <= 0 check for Quantity type is done in CraftingMatcher.

                    // --- Consume based on AmountType ---
                    if (requiredInput.isPrimaryInput && requiredInput.amountType == AmountType.Health)
                    {
                        // This is the primary input, consume HEALTH based on the ACTUAL amount crafted
                        if (primaryInputItemInstance != null) // Should be true if consumptionSuccess is true here
                        {
                            // Use the dedicated modifier method, passing the ACTUAL amount to consume
                            // --- MODIFIED: Use actualCraftedAmount for health consumption ---
                            Debug.Log($"CraftingExecutor: Attempting to consume {actualCraftedAmount} health from primary input '{primaryInputItemInstance.details.Name}' (Health type).", primaryInputInventory.gameObject);
                            bool healthConsumed = CraftingItemModifier.ConsumePrimaryInputHealth(primaryInputInventory, primaryInputItemInstance, actualCraftedAmount); // <-- Use actualCraftedAmount
                            // --- END MODIFIED ---
                            if (!healthConsumed)
                            {
                                Debug.LogError($"CraftingExecutor: Failed to consume health from primary input item '{primaryInputItemInstance.details.Name}' (ID: {primaryInputItemInstance.Id}). Aborting consumption.", primaryInputInventory.gameObject); // Corrected context
                                consumptionSuccess = false; // Health consumption failed
                                break; // Stop consuming other inputs
                            }
                        }
                        // else: Error already logged above if primaryInputItemInstance was null.
                    }
                    else // This is a quantity-based input (either primary quantity or secondary quantity)
                    {
                        if (requiredInput.amountType != AmountType.Quantity)
                        {
                             Debug.LogError($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has a non-primary or primary non-health input with unexpected AmountType: {requiredInput.amountType}. Aborting consumption.");
                             consumptionSuccess = false;
                             break; // Cannot proceed with unexpected input type
                        }
                        if (requiredInput.amount <= 0) // Double-check quantity is positive
                        {
                             Debug.LogWarning($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' specifies quantity input '{requiredInput.itemDetails.Name}' with zero or negative quantity ({requiredInput.amount}). Skipping consumption for this input.");
                             continue; // Skip invalid quantity input requirement
                        }

                        // Calculate total quantity to consume for this item type across ALL input inventories
                        // Quantity inputs are consumed based on the recipe amount * batches, NOT the actual crafted amount.
                        int totalQuantityToConsume = requiredInput.amount * batchesToCraft;
                        Debug.Log($"CraftingExecutor: Attempting to consume {totalQuantityToConsume} of {requiredInput.itemDetails.Name} (Quantity type).", primaryInputInventory.gameObject);

                        // --- Refined Logic for Consuming Quantity Inputs ---
                        // Try to remove the quantity from inventories using Inventory.TryRemoveQuantity.
                        // This method searches within the inventory it's called on.
                        // By calling it sequentially on primary and then secondary inventories,
                        // we effectively consume the total required quantity from wherever it exists
                        // across the combined input slots.

                        int removedFromPrimary = primaryInputInventory.TryRemoveQuantity(requiredInput.itemDetails, totalQuantityToConsume);

                        int remainingToRemove = totalQuantityToConsume - removedFromPrimary;
                        int removedFromSecondary = 0;
                        if (secondaryInputInventory != null && remainingToRemove > 0)
                        {
                            removedFromSecondary = secondaryInputInventory.TryRemoveQuantity(requiredInput.itemDetails, remainingToRemove);
                        }

                        if (removedFromPrimary + removedFromSecondary != totalQuantityToConsume)
                        {
                            // This is a critical error - indicates discrepancy between batch calculation and actual removal
                            Debug.LogError($"CraftingExecutor: CRITICAL ERROR: Failed to fully consume input '{requiredInput.itemDetails.Name}' (needed {totalQuantityToConsume}, removed {removedFromPrimary + removedFromSecondary}) during craft execution! This indicates a major bug in batch calculation or removal logic.");
                            // TODO: Implement more robust error handling here - maybe try to return consumed items?
                            consumptionSuccess = false;
                            break; // Consumption failed for this item
                        }
                         else
                         {
                              Debug.Log($"CraftingExecutor: Successfully consumed {totalQuantityToConsume} of {requiredInput.itemDetails.Name}.");
                         }
                    }
                    // --- End Consume based on AmountType ---
                }
            }


            if (!consumptionSuccess)
            {
                Debug.LogError("CraftingExecutor: Input consumption failed. Aborting craft output.");
                return false; // Indicate consumption failed
            }

            // --- Produce Outputs (based on batchesToCraft for quantity, actualCraftedAmount for health) ---
            // Note: Even if adding outputs fails (e.g., output inventory full), we still consider consumption successful
            // and allow the state transition, so the player can clear space.
            bool outputProductionAttempted = false; // Track if we tried to produce anything
            foreach (var producedOutput in recipeToCraft.outputs)
            {
                 outputProductionAttempted = true; // We are attempting production

                 if (producedOutput?.itemDetails == null)
                {
                    Debug.LogError($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has a produced output with null ItemDetails. Skipping this output item.");
                    continue; // Skip this invalid output
                }
                 // Note: producedOutput.amount <= 0 check is done below based on AmountType.

                // --- Produce based on AmountType ---
                if (producedOutput.amountType == AmountType.Health)
                {
                    // This is a health-based output (prepared prescription)
                    // The health is set based on the ACTUAL amount crafted by the minigame.
                    if (actualCraftedAmount <= 0)
                    {
                         Debug.LogWarning($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has a health-based output '{producedOutput.itemDetails.Name}', but actualCraftedAmount is 0 or less ({actualCraftedAmount}). Skipping output production for this item.");
                         continue; // Cannot produce a health-based item with 0 health
                    }

                    // --- MODIFIED: Use actualCraftedAmount for health production ---
                    Debug.Log($"CraftingExecutor: Preparing health-based output '{producedOutput.itemDetails.Name}' with initial health {actualCraftedAmount} (Actual Crafted Amount).", producedOutput.itemDetails);

                    // Use the dedicated modifier method to create the item instance, passing the ACTUAL amount
                    Item outputItemInstance = CraftingItemModifier.PrepareCraftedOutput(producedOutput.itemDetails, actualCraftedAmount); // <-- Use actualCraftedAmount
                    // --- END MODIFIED ---

                    if (outputItemInstance != null)
                    {
                         // Attempt to add the output item instance to the output inventory
                         // AddItem handles finding an empty slot for non-stackable items.
                         bool added = outputInventory.AddItem(outputItemInstance); // AddItem returns true if fully added (sets input quantity to 0)

                         if (!added)
                         {
                             // Log failure, including the remaining quantity on the instance (should be 1 if not fully added)
                             Debug.LogError($"CraftingExecutor: Failed to add health-based output item '{outputItemInstance.details?.Name ?? "Unknown"}' (Initial Health: {actualCraftedAmount}) to output inventory. Remaining on instance (should be 1): {outputItemInstance.quantity}. Output inventory might be full!");
                         }
                          else
                          {
                              // Log success, including the remaining quantity on the instance (should be 0 if fully added)
                               Debug.Log($"CraftingExecutor: Produced and added health-based output '{outputItemInstance.details.Name}' to output inventory. Remaining on instance: {outputItemInstance.quantity}.");
                          }
                    }
                    else
                    {
                         Debug.LogError($"CraftingExecutor: Failed to prepare health-based output item instance for '{producedOutput.itemDetails.Name}'. Skipping adding to inventory.");
                    }
                }
                else // This is a quantity-based output
                {
                    if (producedOutput.amountType != AmountType.Quantity)
                    {
                         Debug.LogError($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has an output with unexpected AmountType: {producedOutput.amountType}. Skipping output production for this item.");
                         continue; // Cannot proceed with unexpected output type
                    }
                    if (producedOutput.amount <= 0) // Check quantity is positive
                    {
                         Debug.LogWarning($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' specifies quantity output '{producedOutput.itemDetails.Name}' with zero or negative quantity ({producedOutput.amount}). Skipping this output item.");
                         continue; // Skip invalid quantity
                    }

                    // Calculate total quantity to produce for this output item type
                    // Quantity outputs are produced based on the recipe amount * batches, NOT the actual crafted amount.
                    int totalQuantityToProduce = producedOutput.amount * batchesToCraft;
                    Debug.Log($"CraftingExecutor: Preparing quantity-based output '{producedOutput.itemDetails.Name}' with total quantity {totalQuantityToProduce}.", producedOutput.itemDetails);


                    // Create a new Item instance for this output type with the total produced quantity.
                    // Inventory.AddItem will handle creating stacks or adding single instances based on item.maxStack.
                    Item outputItemInstance = producedOutput.itemDetails.Create(totalQuantityToProduce); // Use the Create method

                    // Attempt to add the output item to the output inventory using the public AddItem method on Inventory
                    // Inventory.AddItem will handle whether it's stackable or non-stackable.
                    bool added = outputInventory.AddItem(outputItemInstance); // AddItem returns true if fully added (sets input quantity to 0)

                    if (!added)
                    {
                        // Log failure, including the remaining quantity on the instance (should be > 0 if not fully added)
                        Debug.LogError($"CraftingExecutor: Failed to add quantity-based output item '{outputItemInstance.details?.Name ?? "Unknown"}' (Initial Qty: {totalQuantityToProduce}) to output inventory. Remaining on instance: {outputItemInstance.quantity}. Output inventory might be full!");
                    }
                     else
                     {
                         // Log success, including the remaining quantity on the instance (should be 0 if fully added)
                          Debug.Log($"CraftingExecutor: Produced and added {totalQuantityToProduce} of {outputItemInstance.details.Name} to output inventory. Remaining on instance: {outputItemInstance.quantity}.");
                     }
                }
                // --- End Produce based on AmountType ---
            }

            if (!outputProductionAttempted)
            {
                 Debug.LogWarning($"CraftingExecutor: Craft execution finished, but no outputs were defined or valid in recipe '{recipeToCraft.recipeName}'.");
            }

            // If we attempted to produce outputs, craft execution is conceptually complete from the executor's perspective.
            // The calling code (CraftingStation) will handle the state transition based on consumption success.
            return consumptionSuccess; // Return whether input consumption succeeded
        }
    }
}