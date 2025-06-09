using System;
using System.Collections.Generic; // Needed for EqualityComparer
using UnityEngine;

namespace Systems.Inventory
{
    /// <summary>
    /// Represents a specific instance of an item in the inventory with a quantity, health/durability,
    /// and gun-specific state if applicable.
    /// Implements IEquatable for comparing item instances based on their unique instance Id.
    /// </summary>
    [Serializable]
    public class Item : IEquatable<Item> // Implement IEquatable
    {
        // Unique identifier for this specific item instance.
        public SerializableGuid Id; // Assuming SerializableGuid has proper Equals/GetHashCode/operators

        // Reference to the ItemDetails ScriptableObject defining the item type.
        public ItemDetails details;

        // The current quantity of this item instance (relevant for stackable items).
        public int quantity;

        // The current total health or durability of this item instance (relevant for non-stackable items with maxHealth > 0).
        // For guns, this represents the total remaining ammo (magazine + reserve).
        public int health;

        // Counter for usage events since the last health reduction (relevant for DelayedHealthReduction logic).
        public int usageEventsSinceLastLoss;

        // --- NEW FIELDS FOR GUN LOGIC (Relevant if details.usageLogic is GunLogic) ---
        // The current number of shots remaining in the magazine.
        public int currentMagazineHealth; // NEW FIELD
        // The total number of shots remaining outside the current magazine (reserve ammo).
        public int totalReserveHealth; // NEW FIELD
        // Flag indicating if the gun is currently in the process of reloading.
        public bool isReloading; // NEW FIELD
        // The time when the current reload started (used to track progress).
        public float reloadStartTime; // NEW FIELD
        // --- END NEW FIELDS ---


        // Constructor
        public Item(ItemDetails details, int quantity = 1)
        {
            if (details == null)
            {
                 Debug.LogError("Attempted to create an Item instance with null ItemDetails.");
                 // Initialize with default/empty state if details are null
                 Id = SerializableGuid.Empty;
                 this.details = null;
                 this.quantity = 0;
                 this.health = 0;
                 this.usageEventsSinceLastLoss = 0;
                 // Initialize new gun fields to defaults
                 this.currentMagazineHealth = 0;
                 this.totalReserveHealth = 0;
                 this.isReloading = false;
                 this.reloadStartTime = 0.0f;
                 return; // Exit constructor early
            }

            Id = SerializableGuid.NewGuid(); // Unique ID for THIS instance
            this.details = details;

            // --- Initialize quantity, health, and gun-specific fields based on ItemDetails ---
            if (details.maxStack > 1)
            {
                // This is a stackable item
                this.quantity = Mathf.Max(1, quantity); // Ensure quantity is at least 1 if creating
                this.health = 0; // Total health is not used for stackable items
                // Initialize new gun fields to defaults
                this.currentMagazineHealth = 0;
                this.totalReserveHealth = 0;
                this.isReloading = false;
                this.reloadStartTime = 0.0f;
            }
            else // maxStack is 1 (non-stackable)
            {
                this.quantity = 1; // Quantity is always 1 for non-stackable items
                this.usageEventsSinceLastLoss = 0; // Initialize counter for delayed logic

                if (details.usageLogic == ItemUsageLogic.GunLogic && details.magazineSize > 0)
                {
                    // This is a gun item
                    this.health = Mathf.Max(0, details.maxHealth); // Total ammo pool
                    // Initialize magazine and reserve based on total ammo and magazine size
                    this.currentMagazineHealth = Mathf.Min(details.magazineSize, this.health); // Start loaded up to magazine size
                    this.totalReserveHealth = this.health - this.currentMagazineHealth; // Remaining is reserve
                    this.isReloading = false;
                    this.reloadStartTime = 0.0f;
                     Debug.Log($"Created Gun Item instance: ID={Id}, Details='{details.Name}', TotalAmmo={this.health}, MagSize={details.magazineSize}, Initial Mag={this.currentMagazineHealth}, Initial Reserve={this.totalReserveHealth}");
                }
                else
                {
                    // This is a non-stackable item but NOT a gun (uses Basic/Variable/Delayed health or no health)
                    this.health = Mathf.Max(0, details.maxHealth); // Initialize total health from details
                    // Initialize new gun fields to defaults
                    this.currentMagazineHealth = 0;
                    this.totalReserveHealth = 0;
                    this.isReloading = false;
                    this.reloadStartTime = 0.0f;
                     // Debug.Log($"Created Non-Gun Durable Item instance: ID={Id}, Details='{details.Name}', Health={this.health}, MaxHealth={details.maxHealth}"); // Optional debug
                }
            }
            // --- End Initialization Logic ---

            // Debug.Log($"Created Item instance: ID={Id}, Details='{details?.Name ?? "NULL"}', Qty={this.quantity}, Health={this.health}, Mag={this.currentMagazineHealth}, Reserve={this.totalReserveHealth}, MaxStack={details?.maxStack ?? 0}, MaxHealth={details?.maxHealth ?? 0}, UsageLogic={details?.usageLogic.ToString() ?? "NULL"}"); // More detailed optional debug log
        }

        /// <summary>
        /// Checks if this item instance is currently usable (has quantity > 0 for stackables,
        /// or health > 0 and not reloading for durable non-stackables, including guns).
        /// </summary>
        public bool IsUsable()
        {
             if (details == null) return false;

             if (details.maxStack > 1)
             {
                 return quantity > 0;
             }
             else // Non-stackable
             {
                 if (details.maxHealth > 0)
                 {
                     // Durable non-stackable (includes guns)
                     if (details.usageLogic == ItemUsageLogic.GunLogic && details.magazineSize > 0)
                     {
                         // Gun logic: Usable if not reloading AND (has ammo in mag OR has ammo in reserve)
                         // A gun with 0 total ammo is not usable.
                         // A gun with 0 mag but reserve is usable *after* reload.
                         // A gun with mag ammo is usable.
                         return !isReloading && (currentMagazineHealth > 0 || totalReserveHealth > 0);
                     }
                     else
                     {
                         // Other durable non-stackable: Usable if total health > 0
                         return health > 0;
                     }
                 }
                 else
                 {
                     // Non-durable non-stackable (quantity 1 consumable)
                     return quantity > 0; // Quantity should be 1 initially, becomes 0 after use
                 }
             }
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
                 // If both details are null, they are considered the same type (the "null" type)
                 // Otherwise, if one is null and the other isn't, they are different types.
                 return details == otherItem.details; // This relies on the ItemDetails == operator handling nulls
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