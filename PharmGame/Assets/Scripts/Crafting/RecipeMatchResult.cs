using System.Collections.Generic;

namespace Systems.Inventory
{
    /// <summary>
    /// Represents the result of attempting to match input items to a crafting recipe.
    /// </summary>
    public struct RecipeMatchResult
    {
        public CraftingRecipe MatchedRecipe { get; }
        public int MaxCraftableBatches { get; }
        public bool HasMatch => MatchedRecipe != null && MaxCraftableBatches > 0;

        public RecipeMatchResult(CraftingRecipe recipe, int batches)
        {
            MatchedRecipe = recipe;
            MaxCraftableBatches = batches;
        }

        public static RecipeMatchResult NoMatch()
        {
            return new RecipeMatchResult(null, 0);
        }

        /// <summary>
        /// Gets the name of the matched recipe, or "No Match" if no recipe was matched.
        /// </summary>
        public string GetRecipeName()
        {
            return HasMatch ? MatchedRecipe.recipeName : "No Match";
        }
    }
}