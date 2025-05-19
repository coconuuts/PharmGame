// systems/inventory/CraftingExecutor.cs (or similar path)
using System.Collections.Generic;
using UnityEngine;

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

            if (primaryInputInventory?.Combiner == null || outputInventory?.Combiner == null)
            {
                Debug.LogError("CraftingExecutor: Cannot execute craft, primary input or output inventory combiner is null.");
                return false; // Indicate failure due to missing inventories
            }

            if (secondaryInputInventory != null && secondaryInputInventory.Combiner == null)
            {
                 Debug.LogError("CraftingExecutor: Secondary input inventory is assigned but its Combiner is null. Cannot execute craft.");
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

                int totalQuantityToConsume = requiredInput.quantity * batchesToCraft;
                Debug.Log($"CraftingExecutor: Attempting to consume {totalQuantityToConsume} of {requiredInput.itemDetails.Name}.", primaryInputInventory.gameObject); // Log from primary inventory's game object

                int removedFromPrimary = 0;
                if (primaryInputInventory.Combiner != null)
                {
                    removedFromPrimary = primaryInputInventory.Combiner.TryRemoveQuantity(requiredInput.itemDetails, totalQuantityToConsume);
                }

                int remainingToRemove = totalQuantityToConsume - removedFromPrimary;
                int removedFromSecondary = 0;
                if (secondaryInputInventory?.Combiner != null && remainingToRemove > 0)
                {
                    removedFromSecondary = secondaryInputInventory.Combiner.TryRemoveQuantity(requiredInput.itemDetails, remainingToRemove);
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

                // Create a new Item instance for this output type with the total produced quantity
                // AddItem will handle stacking in the output inventory if needed.
                Item outputItemInstance = new Item(producedOutput.itemDetails, totalQuantityToProduce);

                // Attempt to add the output item to the output inventory
                bool added = outputInventory.Combiner.AddItem(outputItemInstance);

                if (!added)
                {
                    Debug.LogError($"CraftingExecutor: Failed to add output item '{producedOutput.itemDetails.Name}' (Qty: {totalQuantityToProduce}) to output inventory. Output inventory might be full!");
                    // TODO: Handle this failure. Drop item? Place in player inventory?
                    // Crafting still succeeds conceptually, but items couldn't be placed.
                }
                 else
                 {
                      Debug.Log($"CraftingExecutor: Produced and added {totalQuantityToProduce} of {producedOutput.itemDetails.Name} to output inventory.");
                 }
            }

            // If we attempted to produce outputs, craft execution is conceptually complete from the executor's perspective.
            // The calling code (CraftingStation) will handle the state transition based on consumption success.
            return consumptionSuccess; // Return whether input consumption succeeded
        }
    }
}