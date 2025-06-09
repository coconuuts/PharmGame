// Systems/Inventory/UsageTriggerType.cs

namespace Systems.Inventory
{
    /// <summary>
    /// Defines the different ways an item's usage can be triggered.
    /// </summary>
    public enum UsageTriggerType
    {
        /// <summary> Default or unassigned trigger type. </summary>
        None,

        /// <summary> Triggered by a specific key press (e.g., 'F'). </summary>
        FKey,

        /// <summary> Triggered by a mouse click (e.g., Left Mouse Button). </summary>
        LeftClick,

        /// <summary> Triggered by the crafting system consuming the item. </summary>
        Crafting,

        /// <summary> Triggered by interacting with an object in the world (e.g., placing an item). </summary>
        WorldInteraction,

        // Add other trigger types as needed (e.g., Equip, Throw, etc.)
    }
}