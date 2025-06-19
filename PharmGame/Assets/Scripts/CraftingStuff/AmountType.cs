// Systems/Inventory/AmountType.cs

namespace Systems.Inventory
{
    /// <summary>
    /// Specifies whether a numerical value in a recipe input or output
    /// represents a standard item quantity or an amount of health/durability.
    /// </summary>
    public enum AmountType
    {
        /// <summary> The amount represents a standard item quantity (relevant for stackable items). </summary>
        Quantity,

        /// <summary> The amount represents health or durability (relevant for non-stackable durable items). </summary>
        Health
    }
}