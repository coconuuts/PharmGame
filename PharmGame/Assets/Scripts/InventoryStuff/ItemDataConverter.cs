using Systems.Inventory;
using Systems.Persistence;
using UnityEngine;

namespace Systems.Persistence {
    
    /// <summary>
    /// Static helper class for converting runtime Item instances into serializable ItemData structures.
    /// </summary>
    public static class ItemDataConverter {
        
        /// <summary>
        /// Converts a live runtime Item instance to its serializable ItemData representation for saving.
        /// </summary>
        public static ItemData ToItemData(Item item) {
            if (item == null || item.details == null) {
                // Return an empty/invalid data structure if the runtime item is invalid
                return new ItemData { 
                    Id = SerializableGuid.Empty, 
                    ItemDetailsId = SerializableGuid.Empty,
                    quantity = 0
                };
            }

            ItemData data = new ItemData {
                Id = item.Id,
                ItemDetailsId = item.details.Id,
                quantity = item.quantity, // Captures stack size or '1' for single items
                health = item.health,
                usageEventsSinceLastLoss = item.usageEventsSinceLastLoss,
                currentMagazineHealth = item.currentMagazineHealth,
                totalReserveHealth = item.totalReserveHealth,
                isReloading = item.isReloading,
                reloadStartTime = item.reloadStartTime,
                // Mapping the newly added field
                patientNameTag = item.patientNameTag 
            };

            // Sanity check: If the item is stackable, ensure durability/gun-state fields are zeroed out in the save data
            if (item.details.maxStack > 1) {
                data.health = 0;
                data.currentMagazineHealth = 0;
                data.totalReserveHealth = 0;
                data.isReloading = false;
                data.reloadStartTime = 0.0f;
            }

            return data;
        }
    }
}