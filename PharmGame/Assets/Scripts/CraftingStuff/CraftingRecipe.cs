using System;
using System.Collections.Generic; // Needed for List
using UnityEngine; // Needed for Tooltip and SerializeField

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// Defines a single crafting recipe with required inputs and produced outputs.
    /// </summary>
    [Serializable] // Mark as Serializable so it appears in the inspector when used in lists
    public class CraftingRecipe
    {
        [Tooltip("The list of items required to craft this recipe.")]
        public List<RecipeInput> inputs = new List<RecipeInput>();

        [Tooltip("The list of items produced by crafting this recipe.")]
        public List<RecipeOutput> outputs = new List<RecipeOutput>();

        [Tooltip("Optional internal name or identifier for the recipe.")]
        public string recipeName; // Useful for debugging or display

        // Optional: Add methods here later for checking if inputs match or getting a list of outputs
        // Example (basic check - full check goes in CraftingStation):
        // public bool MatchesInputs(List<Item> currentInputItems) { ... }
        // public List<Item> GetOutputItems() { ... }
    }
}