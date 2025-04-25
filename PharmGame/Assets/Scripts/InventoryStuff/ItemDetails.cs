using System;
using UnityEngine;
using UnityEditor; // Still needed for OnValidate and EditorUtility
using VisualStorage;

namespace Systems.Inventory
{
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
        public ShelfSlotArrangement shelfArrangement = ShelfSlotArrangement.OneByOne; // ADD THIS FIELD

        [Header("Customer & Sale Info")] // Optional header for organization
        [Tooltip("Can this item be legally bought by customers?")]
        public bool isOverTheCounter;

        [Tooltip("The price of this item when sold to a customer.")]
        public float price = 5.0f;

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
        }

        /// <summary>
        /// Creates a new runtime Item instance based on this ItemDetails template.
        /// </summary>
        /// <param name="quantity">The initial quantity of the item instance.</param>
        /// <returns>A new Item instance.</returns>
        public Item Create(int quantity)
        {
            // Assumes you have an 'Item' class defined elsewhere that takes ItemDetails and quantity.
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