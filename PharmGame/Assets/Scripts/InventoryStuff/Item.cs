using System;
using System.Collections.Generic; // Needed for EqualityComparer
using UnityEngine;

namespace Systems.Inventory
{
    /// <summary>
    /// Represents a specific instance of an item in the inventory with a quantity.
    /// Implements IEquatable for comparing item instances based on their unique instance Id.
    /// </summary>
    [Serializable]
    public class Item : IEquatable<Item> // Implement IEquatable
    {
        // Unique identifier for this specific item instance.
        public SerializableGuid Id; // Assuming SerializableGuid has proper Equals/GetHashCode/operators

        // Reference to the ItemDetails ScriptableObject defining the item type.
        public ItemDetails details;

        // The current quantity of this item instance.
        public int quantity;

        // Constructor
        public Item(ItemDetails details, int quantity = 1)
        {
            if (details == null)
            {
                 Debug.LogError("Attempted to create an Item instance with null ItemDetails."); // Added error check
            }
            Id = SerializableGuid.NewGuid(); // Unique ID for THIS instance
            this.details = details;
            this.quantity = Mathf.Max(0, quantity); // Ensure quantity is not negative
        }

        // --- Equality Implementation (Based on Instance Id) ---

        /// <summary>
        /// Checks if this Item is equal to another Item based on their unique instance Id.
        /// </summary>
        /// <remarks>
        /// This checks for instance equality (are they the same item object in memory/data),
        /// not type equality (are they the same kind of item). Use IsSameType() for that.
        /// </remarks>
        public bool Equals(Item other)
        {
            if (ReferenceEquals(other, null)) // Check for null first
            {
                return false;
            }
            if (ReferenceEquals(this, other)) // Check for same instance
            {
                return true;
            }
            // Compare based on the unique instance Id
            return Id == other.Id; // Uses the overloaded == operator for SerializableGuid
        }

        /// <summary>
        /// Checks if this Item is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as Item); // Use the type-specific Equals
        }

        /// <summary>
        /// Gets the hash code for this Item instance, based on its unique instance Id.
        /// </summary>
        public override int GetHashCode()
        {
            return Id.GetHashCode(); // Uses the GetHashCode from SerializableGuid
        }

        // --- Equality Operators ---

        /// <summary>
        /// Checks if two Item objects are equal based on their unique instance Id.
        /// </summary>
        /// <remarks>
        /// This checks for instance equality, not type equality. Use IsSameType() for that.
        /// </remarks>
        public static bool operator ==(Item left, Item right)
        {
            if (ReferenceEquals(left, null)) // Handle left being null
            {
                return ReferenceEquals(right, null); // True if both are null
            }
            // Use the object's Equals method (which we've overridden)
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two Item objects are not equal based on their unique instance Id.
        /// </summary>
        /// <remarks>
        /// This checks for instance inequality, not type inequality. Use IsSameType() for that.
        /// </remarks>
        public static bool operator !=(Item left, Item right)
        {
            return !(left == right); // Use the overloaded == operator
        }

        // --- Type Comparison ---

        /// <summary>
        /// Checks if this Item instance is of the same item type as another Item instance.
        /// Compares the Id of their associated ItemDetails.
        /// </summary>
        public bool IsSameType(Item otherItem)
        {
            if (ReferenceEquals(otherItem, null))
            {
                return false;
            }
            // Must have valid details to compare types
            if (details == null || otherItem.details == null)
            {
                 // Decide how to handle null details - perhaps never same type?
                 // Or maybe if both are null, they are the "same null type"?
                 // Comparing IDs is safer.
                 return details?.Id == otherItem.details?.Id; // Compare ItemDetails IDs, handles null details safely if SerializableGuid == null is defined
            }
            // Use the ItemDetails equality operator (which compares ItemDetails.Id)
            return details == otherItem.details;
        }

         /// <summary>
         /// Checks if this Item instance can stack with another Item instance.
         /// Currently based only on being the same type and having a max stack > 1.
         /// </summary>
         public bool CanStackWith(Item otherItem)
         {
             if (ReferenceEquals(otherItem, null)) return false;
             if (details == null || otherItem.details == null) return false; // Cannot stack if details are missing
             if (details.maxStack <= 1) return false; // Cannot stack if item type max stack is 1 or less

             return IsSameType(otherItem); // They must be the same type
         }


         // TODO Serialize and Deserialize (Ensure ItemDetails reference is handled correctly during serialization)
    }
}