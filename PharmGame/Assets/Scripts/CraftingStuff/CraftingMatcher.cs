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
        /// based on the provided lists of available input items from primary and secondary inventories.
        /// For prescription recipes (primary input is health-based), max batches is always 1 if ingredients are present.
        /// </summary>
        /// <param name="allRecipes">The collection of all possible crafting recipes.</param>
        /// <param name="primaryInputItems">A list of items currently in the primary input inventory physical slots.</param>
        /// <param name="secondaryInputItems">A list of items currently in the secondary input inventory physical slots (can be null or empty).</param>
        /// <returns>A RecipeMatchResult indicating the matched recipe and batches, or no match.</returns>
        public static RecipeMatchResult FindRecipeMatch(CraftingRecipesSO allRecipes, List<Item> primaryInputItems, List<Item> secondaryInputItems)
        {
            if (allRecipes == null)
            {
                Debug.LogError("CraftingMatcher: Cannot find recipe match, Crafting Recipes SO is null.");
                return RecipeMatchResult.NoMatch();
            }

            // Ensure input lists are not null for safety
            primaryInputItems ??= new List<Item>();
            secondaryInputItems ??= new List<Item>();

            // Combine all input items for easier searching later (especially for secondary quantity inputs)
            List<Item> allInputItems = new List<Item>();
            allInputItems.AddRange(primaryInputItems);
            allInputItems.AddRange(secondaryInputItems);


            foreach (var recipe in allRecipes.recipes)
            {
                // Ensure recipe and its inputs are valid before checking
                if (recipe?.inputs == null || recipe.inputs.Count == 0)
                {
                    Debug.LogWarning($"CraftingMatcher: Skipping recipe '{recipe?.recipeName ?? "Unknown"}' due to null or empty inputs.");
                    continue;
                }

                bool recipeMatches = true; // Assume match until proven otherwise
                int potentialBatches = int.MaxValue; // Used only for quantity-based limitations

                // --- Step 1: Validate Primary Input ---
                RecipeInput primaryInputRequirement = recipe.inputs.FirstOrDefault(input => input.isPrimaryInput);

                if (primaryInputRequirement != null)
                {
                    // Recipe requires a primary input. Check the primary input slots.
                    // Expecting exactly one item in the primary input slots that matches the requirement.
                    // If primaryInputItems has more than one item, or the single item doesn't match, it's not a match.
                    Item primaryInputItemInstance = primaryInputItems.FirstOrDefault(item => item != null && item.details == primaryInputRequirement.itemDetails);

                    if (primaryInputItems.Count != 1 || primaryInputItemInstance == null)
                    {
                        // Primary input slots must contain exactly one item, and it must match the required type.
                        // Debug.Log($"CraftingMatcher: Primary input mismatch for recipe '{recipe.recipeName}'. Expected 1 item of type '{primaryInputRequirement.itemDetails?.Name ?? "NULL"}', found {primaryInputItems.Count} items."); // Verbose debug
                        recipeMatches = false;
                    }
                    else
                    {
                        // Primary item type is present in the primary slot. Validate its AmountType and properties.
                        if (primaryInputRequirement.amountType == AmountType.Health)
                        {
                            // For a health-based primary input, check if the item is durable and non-stackable
                            if (primaryInputItemInstance.details.maxStack > 1 || primaryInputItemInstance.details.maxHealth <= 0)
                            {
                                // Corrected context to primaryInputItemInstance.details (ScriptableObject is Object)
                                Debug.LogWarning($"CraftingMatcher: Primary input item '{primaryInputItemInstance.details.Name}' (ID: {primaryInputItemInstance.Id}) found for recipe '{recipe.recipeName}' but it is not a non-stackable durable item as required by AmountType.Health. Skipping recipe.", primaryInputItemInstance.details);
                                recipeMatches = false;
                            }
                            // Note: The *amount* in the recipe input for health is not used for batch calculation here.
                            // We just need the item to be present and durable.
                            // The check for *sufficient health* happens in OnCraftButtonClicked/CompleteCraft.
                            // For matching purposes, if the correct durable item is present, assume it supports crafting *if other ingredients allow*.
                            // It doesn't impose a batch limit based on its health *at this stage*.
                        }
                        else if (primaryInputRequirement.amountType == AmountType.Quantity)
                        {
                            // If the primary input is quantity-based (less common for prescription, but possible)
                            // Check if the single item in the primary slot has enough quantity for one batch.
                            if (primaryInputItemInstance.quantity < primaryInputRequirement.amount)
                            {
                                // Debug.Log($"CraftingMatcher: Primary quantity input mismatch for recipe '{recipe.recipeName}'. Item '{primaryInputItemInstance.details.Name}' has quantity {primaryInputItemInstance.quantity}, required {primaryInputRequirement.amount}."); // Verbose debug
                                recipeMatches = false;
                            }
                            else
                            {
                                // Calculate batches supported by this single item in the primary slot
                                int batchesSupportedByPrimaryQuantity = primaryInputItemInstance.quantity / primaryInputRequirement.amount;
                                potentialBatches = Mathf.Min(potentialBatches, batchesSupportedByPrimaryQuantity);
                            }
                        }
                        else
                        {
                            // Corrected context to recipe.allCraftingRecipes or null
                            Debug.LogWarning($"CraftingMatcher: Primary input requirement for recipe '{recipe.recipeName}' has unhandled AmountType: {primaryInputRequirement.amountType}. Skipping recipe."); // Removed context, as recipe is not Object
                            recipeMatches = false;
                        }
                    }
                }
                else
                {
                    // Recipe has NO primary input defined. This is a pure quantity recipe.
                    // Ensure the primary input slots are empty.
                    if (primaryInputItems.Count > 0)
                    {
                        // Debug.Log($"CraftingMatcher: Recipe '{recipe.recipeName}' has no primary input, but items found in primary input slots."); // Verbose debug
                        recipeMatches = false;
                    }
                }

                if (!recipeMatches) continue; // If primary input validation failed, skip this recipe


                // --- Step 2: Validate Secondary Inputs (Quantity-Based) ---
                // Collect all non-primary input requirements from the recipe
                List<RecipeInput> secondaryInputRequirements = recipe.inputs.Where(input => !input.isPrimaryInput).ToList();

                // Check if the number of items in secondary slots matches the number of secondary requirements
                // This assumes each secondary requirement corresponds to a single item type placed in secondary slots.
                // If a recipe requires 2 different items as secondary inputs, the player must place one of each.
                // If a recipe requires 5 of the SAME item as a secondary input, the player needs a stack of 5.
                // The current logic sums quantity across all slots, which works for the latter case.
                // Let's stick to summing quantity across all input slots for secondary requirements for simplicity,
                // but ensure the *types* required are present in the secondary slots.

                // Check if the distinct types in secondary slots match the distinct types required by secondary inputs
                HashSet<SerializableGuid> distinctSecondaryAvailableItemDetailsIds = new HashSet<SerializableGuid>();
                foreach (var inputItem in secondaryInputItems)
                {
                    if (inputItem?.details != null && inputItem.details.Id != SerializableGuid.Empty)
                    {
                        distinctSecondaryAvailableItemDetailsIds.Add(inputItem.details.Id);
                    }
                }

                HashSet<SerializableGuid> distinctSecondaryRequiredItemDetailsIds = new HashSet<SerializableGuid>();
                foreach (var requiredInput in secondaryInputRequirements)
                {
                     if (requiredInput?.itemDetails != null && requiredInput.itemDetails.Id != SerializableGuid.Empty)
                     {
                          distinctSecondaryRequiredItemDetailsIds.Add(requiredInput.itemDetails.Id);
                     }
                }

                // Check if the set of available secondary item types exactly matches the set of required secondary item types
                if (!distinctSecondaryAvailableItemDetailsIds.SetEquals(distinctSecondaryRequiredItemDetailsIds))
                {
                    // Debug.Log($"CraftingMatcher: Secondary item types mismatch for recipe '{recipe.recipeName}'. Available: {string.Join(",", distinctSecondaryAvailableItemDetailsIds.Select(id => id.ToString()))}, Required: {string.Join(",", distinctSecondaryRequiredItemDetailsIds.Select(id => id.ToString()))}"); // Verbose debug
                    recipeMatches = false;
                }

                if (!recipeMatches) continue; // If secondary type validation failed, skip this recipe


                // --- Step 3: Calculate Batch Limitations from Secondary Quantity Inputs ---
                // Iterate through all non-primary inputs (which we expect to be quantity-based)
                foreach (var requiredInput in secondaryInputRequirements)
                {
                    // We already checked AmountType is Quantity and amount > 0 above, but defensive check again.
                    if (requiredInput.amountType != AmountType.Quantity || requiredInput.itemDetails == null || requiredInput.amount <= 0)
                    {
                         // Corrected context
                         Debug.LogWarning($"CraftingMatcher: Invalid secondary quantity input requirement for recipe '{recipe.recipeName}'. Skipping recipe."); // Removed context
                         recipeMatches = false;
                         break; // Cannot process this recipe
                    }

                    // Find the total quantity of this item type across ALL input slots (primary and secondary)
                    // Summing across all is safest for quantity inputs, as player might place them anywhere.
                    int totalAvailableQuantity = allInputItems
                                                 .Where(item => item != null && item.details != null && item.details == requiredInput.itemDetails) // Added null check for details
                                                 .Sum(item => item.quantity);

                    // If we don't have the required quantity for one batch, this recipe isn't craftable
                    if (totalAvailableQuantity < requiredInput.amount)
                    {
                        // Debug.Log($"CraftingMatcher: Insufficient quantity for secondary input '{requiredInput.itemDetails.Name}' for recipe '{recipe.recipeName}'. Required {requiredInput.amount}, found {totalAvailableQuantity}."); // Verbose debug
                        recipeMatches = false;
                        break; // Not enough quantity for this ingredient, cannot craft this recipe at all
                    }

                    // Calculate how many batches *this* quantity ingredient can support
                    int batchesSupportedByThisQuantity = totalAvailableQuantity / requiredInput.amount;

                    // The limiting factor is the ingredient that supports the fewest batches
                    potentialBatches = Mathf.Min(potentialBatches, batchesSupportedByThisQuantity);
                }

                if (!recipeMatches) continue; // If secondary quantity validation failed, skip this recipe


                // --- Step 4: Determine Final Batch Count ---
                // If we reached here, all required inputs (primary and secondary) are present with correct types/roles,
                // and quantity inputs are sufficient for at least 'potentialBatches'.

                // If the recipe has a primary input (prescription recipe)
                if (primaryInputRequirement != null && primaryInputRequirement.isPrimaryInput)
                {
                    // For prescription recipes, the batch size is always 1 (one prescription = one craft sequence).
                    // 'potentialBatches' calculated from secondary quantity inputs ensures enough secondary ingredients are present for *that single batch*.
                    // So, if all checks passed, the max batches is 1.
                    if (potentialBatches >= 1) // Ensure secondary inputs support at least 1 batch
                    {
                         // Debug.Log($"CraftingMatcher: Matched prescription recipe '{recipe.recipeName}'. Primary input present, secondary inputs sufficient for 1 batch. Max batches: 1."); // Verbose debug
                         return new RecipeMatchResult(recipe, 1); // Prescription recipes are always 1 batch
                    }
                    else
                    {
                         // Should be caught by recipeMatches = false earlier, but defensive.
                         // Corrected context
                         Debug.LogWarning($"CraftingMatcher: Matched prescription recipe '{recipe.recipeName}' types/roles, primary input present, but secondary quantity inputs ({potentialBatches} batches) are insufficient for 1 batch. This indicates a logic error in previous checks."); // Removed context
                         continue; // Skip this recipe
                    }
                }
                else
                {
                    // If the recipe has NO primary input defined (pure quantity recipe)
                    // The max batches is determined solely by the minimum batches supported by any quantity ingredient.
                    if (potentialBatches >= 1)
                    {
                         // Debug.Log($"CraftingMatcher: Matched quantity-based recipe '{recipe.recipeName}'. Max batches: {potentialBatches}."); // Verbose debug
                         return new RecipeMatchResult(recipe, potentialBatches);
                    }
                    else
                    {
                         // Should be caught by recipeMatches = false earlier, but defensive.
                         // Corrected context
                         Debug.LogWarning($"CraftingMatcher: Matched quantity-based recipe '{recipe.recipeName}' types/roles, but quantity inputs insufficient for 1 batch. This indicates a logic error in previous checks."); // Removed context
                         continue; // Skip this recipe
                    }
                }
            }

            // No recipe matched after checking all
            // Debug.Log("CraftingMatcher: No recipe matched with current input items."); // Verbose debug
            return RecipeMatchResult.NoMatch();
        }
    }
}