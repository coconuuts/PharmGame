using System;
using UnityEngine;
using UnityEditor;
using VisualStorage; // Assuming this namespace is relevant for prefab3D/shelfArrangement
using System.Collections.Generic; // Needed for List

namespace Systems.Inventory
{
    /// <summary>
    /// Defines the different ways a durable item's health/durability can be reduced upon usage.
    /// </summary>
    public enum ItemUsageLogic
    {
        /// <summary> Default logic: Health reduces by 1 per usage event. </summary>
        BasicHealthReduction,

        /// <summary> Health reduces by a variable amount specified by the caller (e.g., crafting system). </summary>
        VariableHealthReduction,

        /// <summary> Health reduces by a fixed amount after a certain number of usage events. </summary>
        DelayedHealthReduction,

        /// <summary> This item uses quantity (maxStack > 1). Health logic is ignored. </summary>
        QuantityBased, // Added for clarity, though logic handles maxStack > 1 separately

        /// <summary> This item is a gun and uses magazine/reserve ammo logic (requires magazineSize > 0). </summary>
        GunLogic // NEW ENUM VALUE
    }


    /// <summary>
    /// Defines the properties and unique identifier for a specific type of item asset.
    /// Implements IEquatable for comparing item types based on their unique Id.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
    public class ItemDetails : ScriptableObject, IEquatable<ItemDetails> // Implement IEquatable
    {
        // Basic item properties
        public string Name;
        public int maxStack = 1;

        // Unique identifier for this item type asset.
        [SerializeField] public SerializableGuid Id; // Assuming SerializableGuid has proper Equals/GetHashCode/operators

        // Visuals and description
        public Sprite Icon;
        [TextArea] // Makes the Description field a multi-line text area in the Inspector
        public string Description;

        // --- ADD FIELDS FOR VISUAL STORAGE ---
        [Header("Visual Storage Settings")]
        [Tooltip("The 3D prefab to instantiate on shelves for this item.")]
        public GameObject prefab3D; // Reference to the 3D prefab
        [Tooltip("How this item prefab occupies ShelfSlot grid spaces.")]
        public ShelfSlotArrangement shelfArrangement = ShelfSlotArrangement.OneByOne;

        [Header("Item Classification")] // New Header for labels/categories
        [Tooltip("The primary classification or label for this item.")]
        public ItemLabel itemLabel = ItemLabel.None; // Use the enum field

        [Tooltip("The price of this item when sold to a customer.")]
        public float price = 5.0f;

        // --- NEW FIELDS FOR ITEM HEALTH/DURABILITY ---
        [Header("Durability/Health")]
        [Tooltip("The maximum health or durability for instances of this item type (only relevant if maxStack is 1). For guns, this is total ammo capacity.")]
        public int maxHealth = 1; // Default to 1 for non-stackable items

        [Tooltip("Defines how health is reduced for this item type (only relevant if maxStack is 1 and maxHealth > 0).")]
        public ItemUsageLogic usageLogic = ItemUsageLogic.BasicHealthReduction; // NEW FIELD

        [Tooltip("Default health loss amount for VariableHealthReduction if no specific amount is provided by the caller.")]
        public int defaultVariableHealthLoss = 1; // NEW FIELD

        [Tooltip("Number of usage events required before health is reduced (for DelayedHealthReduction).")]
        public int usageEventsPerHealthLoss = 1; // NEW FIELD

        [Tooltip("Amount of health lost when the usage event threshold is reached (for DelayedHealthReduction).")]
        public int healthLossPerEventThreshold = 1; // NEW FIELD

        // --- NEW FIELDS FOR GUN LOGIC ---
        [Header("Gun Settings (Requires Usage Logic: GunLogic)")] // NEW HEADER
        [Tooltip("The number of shots/uses before a reload is required (magazine capacity). Only relevant if usageLogic is GunLogic.")]
        public int magazineSize = 0; // NEW FIELD

        [Tooltip("The time in seconds required to complete a reload. Only relevant if usageLogic is GunLogic.")]
        public float reloadTime = 0.0f; // NEW FIELD


        // --- NEW FIELDS FOR USAGE TRIGGERS ---
        [Header("Usage Triggers")]
        [Tooltip("The list of allowed input or system triggers that can initiate usage for this item type.")]
        public List<UsageTriggerType> allowedUsageTriggers = new List<UsageTriggerType>();

        [Tooltip("The primary or default trigger type for UI display purposes (optional).")]
        public UsageTriggerType primaryUsageTrigger = UsageTriggerType.None;


        // --- Editor-Specific Logic ---

        private void OnValidate()
        {
            if (Id == SerializableGuid.Empty) // Uses the overloaded == operator if SerializableGuid supports it
            {
                Id = SerializableGuid.NewGuid();
                #if UNITY_EDITOR // Ensure this is editor-only
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }

            // Optional: Add validation to ensure magazineSize is <= maxHealth if usageLogic is GunLogic
            if (usageLogic == ItemUsageLogic.GunLogic && magazineSize > maxHealth)
            {
                Debug.LogWarning($"ItemDetails '{Name}': magazineSize ({magazineSize}) should not exceed maxHealth ({maxHealth}) for GunLogic.", this);
                // Optional: clamp magazineSize = maxHealth;
            }
            if (usageLogic == ItemUsageLogic.GunLogic && magazineSize <= 0)
            {
                 Debug.LogWarning($"ItemDetails '{Name}': magazineSize should be greater than 0 for GunLogic.", this);
            }
        }

        /// <summary>
        /// Creates a new runtime Item instance based on this ItemDetails template.
        /// </summary>
        /// <param name="quantity">The initial quantity of the item instance (relevant for stackable items).</param>
        /// <returns>A new Item instance.</returns>
        public Item Create(int quantity)
        {
            return new Item(this, quantity);
        }

        // --- Equality Implementation (Based on Id) ---

        /// <summary>
        /// Checks if this ItemDetails is equal to another ItemDetails based on their unique Id.
        /// </summary>
        public bool Equals(ItemDetails other)
        {
            if (ReferenceEquals(other, null)) // Check for null first
            {
                return false;
            }
            if (ReferenceEquals(this, other)) // Check for same instance
            {
                return true;
            }
            // Compare based on the unique Id
            return Id == other.Id; // Uses the overloaded == operator for SerializableGuid
        }

        /// <summary>
        /// Checks if this ItemDetails is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as ItemDetails); // Use the type-specific Equals
        }

        /// <summary>
        /// Gets the hash code for this ItemDetails, based on its unique Id.
        /// </summary>
        public override int GetHashCode()
        {
            return Id.GetHashCode(); // Uses the GetHashCode from SerializableGuid
        }

        // --- Equality Operators ---

        /// <summary>
        /// Checks if two ItemDetails objects are equal based on their unique Id.
        /// </summary>
        public static bool operator ==(ItemDetails left, ItemDetails right)
        {
            if (ReferenceEquals(left, null)) // Handle left being null
            {
                return ReferenceEquals(right, null); // True if both are null
            }
            // Use the object's Equals method (which we've overridden)
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two ItemDetails objects are not equal based on their unique Id.
        /// </summary>
        public static bool operator !=(ItemDetails left, ItemDetails right)
        {
            return !(left == right); // Use the overloaded == operator
        }

         // Optional: Implement inequality based on ID if needed, though == and != are sufficient
         // public bool IsSameType(ItemDetails otherDetails) { return this == otherDetails; }
    }
}