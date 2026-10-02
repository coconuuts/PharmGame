// Systems/Inventory/RecipeOutput.cs

using System;
using UnityEngine; // Needed for Tooltip and SerializeField

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// Defines a resulting output item and amount (quantity or health) for a crafting recipe.
    /// </summary>
    [Serializable] // Mark as Serializable so it appears in the inspector when used in lists
    public class RecipeOutput
    {
        [Tooltip("The type of item produced as output.")]
        public ItemDetails itemDetails; // Reference to the ItemDetails ScriptableObject

        [Tooltip("The amount produced. Interpretation depends on Amount Type (quantity for stackable, health for durable).")]
        public int amount; // Renamed from quantity

        [Tooltip("Specifies whether the Amount represents quantity or health.")]
        public AmountType amountType; // Added field


        // Optional: Constructor for easier creation in code
        // Updated constructor signature to match new fields
        public RecipeOutput(ItemDetails details, int amount, AmountType amountType)
        {
            itemDetails = details;
            this.amount = amount;
            this.amountType = amountType;
        }

        // Optional: Add validation if needed (e.g., amount > 0, amountType matches itemDetails.maxStack/maxHealth)
    }
}   