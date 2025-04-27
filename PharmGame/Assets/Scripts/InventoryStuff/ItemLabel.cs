// ItemLabel.cs
// You can place this in a common 'Enums' folder or within your 'Systems.Inventory' namespace

namespace Systems.Inventory
{
    public enum ItemLabel
    {
        /// <summary>
        /// Default state, no specific label assigned.
        /// </summary>
        None,

        /// <summary>
        /// Items that can be sold over the counter without a prescription.
        /// </summary>
        OverTheCounter,

        /// <summary>
        /// Prescription medication kept as stock, ready to be dispensed.
        /// </summary>
        PillStock,
        LiquidStock,
        InhalerStock,
        InsulinStock,
        IllegalStock,

        /// <summary>
        /// Fully compounded or prepared prescription medication ready for patient.
        /// </summary>
        PrescriptionPrepared,

        /// <summary>
        /// General consumables not necessarily medication (e.g., food, drink).
        /// </summary>
        Consumable,

        /// <summary>
        /// Items primarily used as weapons.
        /// </summary>
        Weapon,

        /// <summary>
        /// Items considered illegal to possess or sell in general.
        /// </summary>
        Illegal,

        /// <summary>
        /// Items that can be placed in the environment (e.g., furniture, decorations).
        /// </summary>
        Placeable,

        /// <summary>
        /// Containers specifically for illegal medical items or drugs.
        /// </summary>
        IllegalMedContainer,
        PillMedContainer,
        LiquidMedContainer,
        InhalerMedContainer,
        InsulinMedContainer

    }
}