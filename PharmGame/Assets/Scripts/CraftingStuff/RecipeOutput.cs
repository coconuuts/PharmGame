using System;
using UnityEngine; // Needed for Tooltip and SerializeField

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// Defines a resulting output item and quantity for a crafting recipe.
    /// </summary>
    [Serializable] // Mark as Serializable so it appears in the inspector when used in lists
    public class RecipeOutput
    {
        [Tooltip("The type of item produced as output.")]
        public ItemDetails itemDetails; // Reference to the ItemDetails ScriptableObject

        [Tooltip("The quantity of the item produced.")]
        public int quantity;

        // Optional: Constructor for easier creation in code
        public RecipeOutput(ItemDetails details, int qty)
        {
            itemDetails = details;
            quantity = qty;
        }

        // Optional: Add validation if needed (e.g., quantity > 0)
    }
}