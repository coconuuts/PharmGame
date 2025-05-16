using System;
using UnityEngine; // Needed for Tooltip and SerializeField

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// Defines a required input item and quantity for a crafting recipe.
    /// </summary>
    [Serializable] // Mark as Serializable so it appears in the inspector when used in lists
    public class RecipeInput
    {
        [Tooltip("The type of item required as input.")]
        public ItemDetails itemDetails; // Reference to the ItemDetails ScriptableObject

        [Tooltip("The quantity of the item required.")]
        public int quantity;

        // Optional: Constructor for easier creation in code
        public RecipeInput(ItemDetails details, int qty)
        {
            itemDetails = details;
            quantity = qty;
        }

        // Optional: Add validation if needed (e.g., quantity > 0)
    }
}