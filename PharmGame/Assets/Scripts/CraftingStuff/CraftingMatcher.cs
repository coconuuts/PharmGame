// systems/inventory/CraftingMatcher.cs (or similar path)
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // For Mathf.Min

namespace Systems.Inventory
{
    /// <summary>
    /// Helper class containing static methods for matching inventory items to crafting recipes.
    /// </summary>
    public static class CraftingMatcher
    {
        /// <summary>
        /// Attempts to find a matching recipe and calculate the maximum craftable batches
        /// based on the provided list of available input items.
        /// </summary>
        /// <param name="allRecipes">The collection of all possible crafting recipes.</param>
        /// <param name="availableInputItems">A combined list of all items currently in input inventories.</param>
        /// <returns>A RecipeMatchResult indicating the matched recipe and batches, or no match.</returns>
        public static RecipeMatchResult FindRecipeMatch(CraftingRecipesSO allRecipes, List<Item> availableInputItems)
        {
            if (allRecipes == null)
            {
                Debug.LogError("CraftingMatcher: Cannot find recipe match, Crafting Recipes SO is null.");
                return RecipeMatchResult.NoMatch();
            }

            if (availableInputItems == null || availableInputItems.Count == 0)
            {
                // No items means no possible match
                 return RecipeMatchResult.NoMatch();
            }

            foreach (var recipe in allRecipes.recipes)
            {
                // Ensure recipe and its inputs are valid before checking
                if (recipe?.inputs == null || recipe.inputs.Count == 0)
                {
                    Debug.LogWarning($"CraftingMatcher: Skipping recipe '{recipe?.recipeName ?? "Unknown"}' due to null or empty inputs.");
                    continue;
                }

                // --- Check if input item *types* match recipe inputs (exact set) ---
                HashSet<SerializableGuid> distinctInputItemDetailsIds = new HashSet<SerializableGuid>();
                foreach (var inputItem in availableInputItems)
                {
                    if (inputItem?.details != null && inputItem.details.Id != SerializableGuid.Empty)
                    {
                        distinctInputItemDetailsIds.Add(inputItem.details.Id);
                    }
                }

                // First check: do the *types* of items present exactly match the *types* required by the recipe?
                // Ensure no extra types are present, and all required types are present.
                bool typesMatchExactly = (distinctInputItemDetailsIds.Count == recipe.inputs.Count) &&
                                         recipe.inputs.All(required => required?.itemDetails != null && distinctInputItemDetailsIds.Contains(required.itemDetails.Id));

                if (!typesMatchExactly)
                {
                    // Debug.Log($"Checking recipe '{recipe.recipeName}': Item types do not match exactly."); // Verbose
                    continue; // Item types don't match this recipe, skip
                }

                // --- If types match, calculate max batches based on quantities ---
                int potentialBatches = int.MaxValue; // Start with a high number
                bool canCraftAtLeastOne = true; // Flag to ensure we can craft at least one batch

                // For each required input in the recipe
                foreach (var requiredInput in recipe.inputs)
                {
                    if (requiredInput.itemDetails == null)
                    {
                        Debug.LogWarning($"CraftingMatcher: Recipe '{recipe.recipeName}' has a required input with null ItemDetails. Cannot check batch count.");
                        canCraftAtLeastOne = false; // Cannot craft this recipe if an input is invalid
                        break;
                    }
                     if (requiredInput.quantity <= 0)
                    {
                         Debug.LogWarning($"CraftingMatcher: Recipe '{recipe.recipeName}' has input '{requiredInput.itemDetails.Name}' with quantity <= 0 ({requiredInput.quantity}). Cannot craft.");
                         canCraftAtLeastOne = false; // Cannot craft if a required quantity is invalid
                         break;
                    }


                    // Find the total quantity of this item type across all input slots
                    int totalInputQuantity = availableInputItems
                                             .Where(item => item.details != null && item.details == requiredInput.itemDetails)
                                             .Sum(item => item.quantity);

                    // If we don't even have the minimum quantity for one batch, this recipe isn't craftable
                    if (totalInputQuantity < requiredInput.quantity)
                    {
                        canCraftAtLeastOne = false;
                        // Debug.Log($"Checking recipe '{recipe.recipeName}': Not enough quantity for {requiredInput.itemDetails.Name} (needed {requiredInput.quantity}, found {totalInputQuantity})."); // Verbose
                        break; // Not enough quantity for this ingredient, cannot craft this recipe at all
                    }

                    // Calculate how many batches *this* ingredient can support
                    int batchesSupportedByThisIngredient = totalInputQuantity / requiredInput.quantity;

                    // The limiting factor is the ingredient that supports the fewest batches
                    potentialBatches = Mathf.Min(potentialBatches, batchesSupportedByThisIngredient);
                }

                // If we can craft at least one batch (all required ingredients are present in sufficient quantity)
                if (canCraftAtLeastOne && potentialBatches >= 1)
                {
                    // Found a match!
                    // Debug.Log($"CraftingMatcher: Found match for recipe '{recipe.recipeName}' with {potentialBatches} potential batches."); // Verbose
                    return new RecipeMatchResult(recipe, potentialBatches);
                }
            }

            // No recipe matched
            return RecipeMatchResult.NoMatch();
        }
    }
}