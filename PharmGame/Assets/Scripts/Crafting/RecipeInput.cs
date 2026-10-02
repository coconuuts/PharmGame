// Systems/Inventory/RecipeInput.cs

using System;
using UnityEngine; // Needed for Tooltip and SerializeField

namespace Systems.Inventory // Ensure this is the correct namespace
{
    /// <summary>
    /// Defines a required input item and amount (quantity or health) for a crafting recipe.
    /// Also identifies if this is the primary input item.
    /// </summary>
    [Serializable] // Mark as Serializable so it appears in the inspector when used in lists
    public class RecipeInput
    {
        [Tooltip("The type of item required as input.")]
        public ItemDetails itemDetails; // Reference to the ItemDetails ScriptableObject

        [Tooltip("The amount required. Interpretation depends on Amount Type (quantity for stackable, health for durable).")]
        public int amount; // Renamed from quantity

        [Tooltip("Specifies whether the Amount represents quantity or health.")]
        public AmountType amountType; // Added field

        [Tooltip("If true, this input is considered the primary item (e.g., the main stock for prescriptions).")]
        public bool isPrimaryInput; // Added field


        // Optional: Constructor for easier creation in code
        // Updated constructor signature to match new fields
        public RecipeInput(ItemDetails details, int amount, AmountType amountType, bool isPrimaryInput = false)
        {
            itemDetails = details;
            this.amount = amount;
            this.amountType = amountType;
            this.isPrimaryInput = isPrimaryInput;
        }

        // Optional: Add validation if needed (e.g., amount > 0, amountType matches itemDetails.maxStack/maxHealth)
    }
}