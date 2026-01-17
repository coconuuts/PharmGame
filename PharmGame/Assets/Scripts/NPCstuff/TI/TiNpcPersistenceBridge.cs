using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Game.NPC.TI
{
    /// <summary>
    /// Acts as a bridge between the generic SaveLoadSystem and the specific TiNpcManager.
    /// Responsible for extracting the full list of NPC data for saving, and repopulating the Manager on load.
    /// </summary>
    public class TiNpcPersistenceBridge : MonoBehaviour
    {
        public static TiNpcPersistenceBridge Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Retrieves ALL TI NPC data (Active and Inactive/Simulated) from the Manager.
        /// Called by SaveLoadSystem during the Save process.
        /// </summary>
        public List<TiNpcData> GetAllTiNpcData()
        {
            if (TiNpcManager.Instance == null)
            {
                Debug.LogError("TiNpcPersistenceBridge: TiNpcManager is missing! Cannot save NPC data.");
                return new List<TiNpcData>();
            }

            // Return a list based on the Values of the internal dictionary
            // This assumes the "Flush" has already happened for active NPCs.
            return TiNpcManager.Instance.allTiNpcs.Values.ToList();
        }

        /// <summary>
        /// Injects a list of loaded NPC data back into the Manager.
        /// Called by SaveLoadSystem during the Load process.
        /// </summary>
        public void LoadAllTiNpcData(List<TiNpcData> loadedData)
        {
            if (TiNpcManager.Instance == null)
            {
                Debug.LogError("TiNpcPersistenceBridge: TiNpcManager is missing! Cannot load NPC data.");
                return;
            }

            if (loadedData == null)
            {
                Debug.LogWarning("TiNpcPersistenceBridge: Loaded data list is null. Skipping load.");
                return;
            }

            loadedData.Sort(SortByStatePriority);

            // Access the internal dictionary of the manager (allowed since we are in the same namespace)
            TiNpcManager.Instance.allTiNpcs.Clear();

            // Clear the Grid as well to prevent ghost entries from previous dummy data
            // (Assumes Manager has a reference or we can access GridManager)
            // Ideally TiNpcManager exposes a 'ClearAllData' method, but for now we manipulate directly/via known methods.
            // Since we can't easily clear the grid from here without extra deps, we rely on TiNpcManager to handle grid re-population on next update or manually here.
            
            // Re-populate dictionary
            foreach (var data in loadedData)
            {
                if (data != null && !string.IsNullOrEmpty(data.Id))
                {
                    // Reset Runtime Flags
                    // Since 'isActiveGameObject' is serialized, it might load as true.
                    // We must force it to false because the GameObject definitely does not exist yet.
                    data.isActiveGameObject = false; 
                    data.NpcGameObject = null;

                    // CRITICAL: Re-link the Prefab! 
                    // Save files don't store the Prefab reference. We must find it.
                    // For Phase 2, we might not have a lookup yet. 
                    // TODO: Implement a "Prefab Lookup" if not present. 
                    // For now, if the loaded data has a null prefab, the NPC won't activate visually.
                    // We will assume TiNpcManager or a Database can restore this later.
                    
                    TiNpcManager.Instance.allTiNpcs[data.Id] = data;

                    // Confirm the position retrieved from disk
                    Debug.Log($"[TiNpcPersistenceBridge] Loaded '{data.Id}'. WorldPos: {data.CurrentWorldPosition}. State: {data.CurrentStateEnumKey}");
                }
            }

            Debug.Log($"TiNpcPersistenceBridge: Successfully loaded {loadedData.Count} TI NPCs into the Manager.");
            
            // Note: SimulationManager will pick these up automatically on its next tick.
            // GridManager will be updated when SimulationManager runs or when active objects are spawned.
        }

        /// <summary>
        /// Helper to determine load order priority based on the saved state key.
        /// Lower number = Loads First.
        /// </summary>
        private int SortByStatePriority(TiNpcData a, TiNpcData b)
        {
            int priorityA = GetStateLoadPriority(a.CurrentStateEnumKey);
            int priorityB = GetStateLoadPriority(b.CurrentStateEnumKey);
            return priorityA.CompareTo(priorityB);
        }

        private int GetStateLoadPriority(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey)) return 100;

            // Priority 0: Holding Critical Resources (Register)
            if (stateKey.Contains("TransactionActive") || 
                stateKey.Contains("WaitingAtRegister") || 
                stateKey.Contains("MovingToRegister") ||
                stateKey.Contains("ProcessingCheckout")) 
                return 0;

            // Priority 1: Occupying Main Queue Spots (Finite resource)
            // Ensure we don't accidentally catch 'SecondaryQueue' here
            if (stateKey.Contains("Queue") && !stateKey.Contains("Secondary")) 
                return 1;

            // Priority 2: Shopping and outside
            if (stateKey.Contains("Browse") || 
                stateKey.Contains("Exiting") || 
                stateKey.Contains("SecondaryQueue")) 
                return 2;

            // Priority 3: Looking to Shop
            if (stateKey.Contains("Entering") || 
                stateKey.Contains("LookToShop")) 
                return 3;

            // Priority 4: Default/Other
            return 4;
        }
    }
}