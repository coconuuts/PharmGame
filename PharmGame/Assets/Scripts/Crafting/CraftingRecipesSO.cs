using System.Collections.Generic; // Needed for List
using UnityEngine; // Needed for ScriptableObject and CreateAssetMenu

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// A ScriptableObject holding a collection of crafting recipes.
    /// </summary>
    [CreateAssetMenu(fileName = "New Crafting Recipes", menuName = "Crafting/Crafting Recipes")] 
    public class CraftingRecipesSO : ScriptableObject
    {
        [Tooltip("The list of all defined crafting recipes.")]
        public List<CraftingRecipe> recipes = new List<CraftingRecipe>();
    }
}