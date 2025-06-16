using System.Collections.Generic;
using UnityEngine;
using Systems.Inventory; // Ensure this namespace matches your scripts

namespace Systems.Inventory
{
    /// <summary>
    /// Helper class containing static methods to execute the crafting process
    /// (consume inputs, produce outputs) for a matched recipe and batch count.
    /// </summary>
    public static class CraftingExecutor
    {
        /// <summary>
        /// Executes the crafting process: consumes required inputs and produces outputs.
        /// </summary>
        /// <param name="recipeToCraft">The recipe to craft.</param>
        /// <param name="batchesToCraft">The number of batches to craft.</param>
        /// <param name="primaryInputInventory">The primary input inventory.</param>
        /// <param name="secondaryInputInventory">The secondary input inventory (can be null).</param>
        /// <param name="outputInventory">The output inventory.</param>
        /// <returns>True if input consumption was successful, false otherwise. Output production success is not guaranteed by this return value.</returns>
        public static bool ExecuteCraft(
            CraftingRecipe recipeToCraft,
            int batchesToCraft,
            Inventory primaryInputInventory,
            Inventory secondaryInputInventory,
            Inventory outputInventory)
        {
            if (recipeToCraft == null || batchesToCraft <= 0)
            {
                Debug.LogError("CraftingExecutor: ExecuteCraft called with invalid recipe or batch count.");
                return false; // Indicate failure due to invalid input
            }

            // Check if required inventories are assigned and have Combiners (as Inventory.AddItem relies on Combiner)
            if (primaryInputInventory == null || primaryInputInventory.Combiner == null || outputInventory == null || outputInventory.Combiner == null) // *** MODIFIED CHECK ***
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

            // --- Consume Inputs (based on batchesToCraft) ---
            bool consumptionSuccess = true;
            foreach (var requiredInput in recipeToCraft.inputs)
            {
                if (requiredInput?.itemDetails == null)
                {
                    Debug.LogError($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has a required input with null ItemDetails. Aborting consumption.");
                    consumptionSuccess = false;
                    break; // Cannot proceed if input is invalid
                }
                if (requiredInput.quantity <= 0)
                {
                     Debug.LogWarning($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' specifies input '{requiredInput.itemDetails.Name}' with zero or negative quantity ({requiredInput.quantity}). Skipping this input item for consumption.");
                     continue; // Skip invalid quantity input requirement
                }


                int totalQuantityToConsume = requiredInput.quantity * batchesToCraft;
                Debug.Log($"CraftingExecutor: Attempting to consume {totalQuantityToConsume} of {requiredInput.itemDetails.Name}.", primaryInputInventory.gameObject);

                // Try to remove the quantity from inventories using Inventory.TryRemoveQuantity
                // TryRemoveQuantity handles both stackable quantities and non-stackable instances.
                int removedFromPrimary = primaryInputInventory.TryRemoveQuantity(requiredInput.itemDetails, totalQuantityToConsume); // *** MODIFIED ***

                int remainingToRemove = totalQuantityToConsume - removedFromPrimary;
                int removedFromSecondary = 0;
                if (secondaryInputInventory != null && remainingToRemove > 0)
                {
                    removedFromSecondary = secondaryInputInventory.TryRemoveQuantity(requiredInput.itemDetails, remainingToRemove); // *** MODIFIED ***
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

            if (!consumptionSuccess)
            {
                Debug.LogError("CraftingExecutor: Input consumption failed. Aborting craft output.");
                return false; // Indicate consumption failed
            }

            // --- Produce Outputs (based on batchesToCraft) ---
            // Note: Even if adding outputs fails (e.g., output inventory full), we still consider consumption successful
            // and allow the state transition, so the player can clear space.
            foreach (var producedOutput in recipeToCraft.outputs)
            {
                 if (producedOutput?.itemDetails == null)
                {
                    Debug.LogError($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' has a produced output with null ItemDetails. Skipping this output item.");
                    continue; // Skip this invalid output
                }
                 if (producedOutput.quantity <= 0)
                 {
                      Debug.LogWarning($"CraftingExecutor: Recipe '{recipeToCraft.recipeName}' specifies output '{producedOutput.itemDetails.Name}' with zero or negative quantity ({producedOutput.quantity}). Skipping this output item.");
                      continue; // Skip invalid quantity
                 }
                // Calculate total quantity to produce for this output item type
                int totalQuantityToProduce = producedOutput.quantity * batchesToCraft;

                // Create a new Item instance for this output type with the total produced quantity.
                // Inventory.AddItem will handle creating stacks or adding single instances based on item.maxStack.
                Item outputItemInstance = producedOutput.itemDetails.Create(totalQuantityToProduce); // Use the Create method

                // Attempt to add the output item to the output inventory using the public AddItem method on Inventory
                // Inventory.AddItem will handle whether it's stackable or non-stackable.
                bool added = outputInventory.AddItem(outputItemInstance); // *** MODIFIED ***

                if (!added)
                {
                    // Log failure, including the remaining quantity on the instance (should be > 0 if not fully added)
                    Debug.LogError($"CraftingExecutor: Failed to add output item '{outputItemInstance.details?.Name ?? "Unknown"}' (Initial Qty: {totalQuantityToProduce}) to output inventory. Remaining on instance: {outputItemInstance.quantity}. Output inventory might be full!");
                }
                 else
                 {
                     // Log success, including the remaining quantity on the instance (should be 0 if fully added)
                      Debug.Log($"CraftingExecutor: Produced and added {totalQuantityToProduce} of {outputItemInstance.details.Name} to output inventory. Remaining on instance: {outputItemInstance.quantity}.");
                 }
            }
            // If we attempted to produce outputs, craft execution is conceptually complete from the executor's perspective.
            // The calling code (CraftingStation) will handle the state transition based on consumption success.
            return consumptionSuccess; // Return whether input consumption succeeded
        }
    }
}