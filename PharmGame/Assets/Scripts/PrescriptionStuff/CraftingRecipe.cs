// CraftingRecipe.cs
using UnityEngine;
using Systems.Inventory; // Ensure this matches your Inventory system namespace
using System; // Needed for Serializable

namespace Prescription // Use the Prescription namespace
{
    /// <summary>
    /// Defines a crafting recipe for the prescription table.
    /// Maps ingredients from the main and specific inventories to a result item.
    /// </summary>
    [Serializable] // Make it serializable so it appears in the inspector
    public class CraftingRecipe
    {
        [Tooltip("The ItemDetails of the item expected in the SPECIFIC (secondary) inventory slot.")]
        public ItemDetails secondaryIngredient;

        [Tooltip("The quantity required of the secondary ingredient.")]
        public int secondaryQuantityToConsume = 1;

        [Tooltip("The ItemDetails of the item expected in the MAIN prescription table inventory slot.")]
        public ItemDetails mainIngredientToConsume; // Likely the stock item (PillStock, etc.)

        [Tooltip("The quantity required of the main inventory ingredient.")]
        public int mainQuantityToConsume = 1;

        [Tooltip("The ItemDetails of the item produced as a result of crafting (e.g., PrescriptionPrepared).")]
        public ItemDetails resultItem;

        [Tooltip("The quantity of the result item produced per craft.")]
        public int resultQuantity = 1;

        // Optional: Add other recipe properties like time, required tools, etc.
    }
}