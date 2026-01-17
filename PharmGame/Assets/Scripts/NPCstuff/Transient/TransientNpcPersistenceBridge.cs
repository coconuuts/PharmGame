using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Game.NPC; // For TransientNpcData and NpcStateMachineRunner
using Utils.Pooling; // Assuming PoolingManager is here

namespace Systems.Persistence
{
    public class TransientNpcPersistenceBridge : MonoBehaviour
    {
        public static TransientNpcPersistenceBridge Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("The prefab used to spawn standard transient NPCs.")]
        [SerializeField] private GameObject transientNpcPrefab;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Only attempt cleanup if the application is playing to avoid editor errors
            if (Application.isPlaying)
            {
                ClearActiveTransientNpcs();
            }
        }

        /// <summary>
        /// Scans the scene for active transient NPCs and packages their data.
        /// </summary>
        public List<TransientNpcData> GetAllTransientData()
        {
            List<TransientNpcData> dataList = new List<TransientNpcData>();

            // Find ALL runners in the scene
            // Note: FindObjectsByType is Unity 2023+. Use FindObjectsOfType for older versions.
            var allRunners = FindObjectsByType<NpcStateMachineRunner>(FindObjectsSortMode.None);

            foreach (var runner in allRunners)
            {
                // We only want Active GameObjects
                if (!runner.gameObject.activeInHierarchy) continue;

                // 1. Skip TI NPCs (they are saved by TiNpcManager)
                if (runner.IsTrueIdentityNpc) continue;

                // 2. Ask the runner to package its data
                // It will return null if it's in an invalid state (like ReturningToPool)
                var data = runner.CreateTransientSaveData();

                if (data != null)
                {
                    dataList.Add(data);
                }
            }

            Debug.Log($"TransientNpcPersistenceBridge: Saved {dataList.Count} transient NPCs.");
            return dataList;
        }

        /// <summary>
        /// Helper to return all currently active Transient NPCs to the pool.
        /// This prevents duplicates when loading a save file.
        /// </summary>
        public void ClearActiveTransientNpcs()
        {
            // Check if PoolingManager exists to avoid errors on game shutdown
            if (PoolingManager.Instance == null) return;

            // Find all runners currently in the scene
            var allRunners = FindObjectsByType<NpcStateMachineRunner>(FindObjectsSortMode.None);

            foreach (var runner in allRunners)
            {
                // Safety check
                if (runner == null) continue;

                // 1. Ensure we don't destroy TI (Persistent) NPCs
                if (runner.IsTrueIdentityNpc) continue;

                // 2. Ensure we don't try to return something that is already inactive/pooled
                if (!runner.gameObject.activeInHierarchy) continue;

                // 3. Return to pool
                PoolingManager.Instance.ReturnPooledObject(runner.gameObject);
            }
            
            Debug.Log("TransientNpcPersistenceBridge: Cleared active transient NPCs.");
        }

        /// <summary>
        /// Spawns clones and restores their state from the loaded list.
        /// </summary>
        public void LoadAllTransientData(List<TransientNpcData> dataList)
        {
            ClearActiveTransientNpcs();
            
            if (dataList == null || dataList.Count == 0) return;
            
            if (PoolingManager.Instance == null || transientNpcPrefab == null)
            {
                Debug.LogError("TransientNpcPersistenceBridge: Cannot load NPCs. PoolingManager or Prefab is missing.");
                return;
            }

            dataList.Sort(SortByStatePriority);
            Debug.Log($"TransientNpcPersistenceBridge: Restoring {dataList.Count} transient NPCs...");

            foreach (var data in dataList)
            {
                // 1. Get a fresh clone
                GameObject npcObj = PoolingManager.Instance.GetPooledObject(transientNpcPrefab);
                
                if (npcObj != null)
                {
                    // 2. Initialize Dependencies
                    // We need to ensure the runner has its dependencies (Manager) set up
                    // because we are bypassing the normal 'Initialize()' flow which usually sets them.
                    var runner = npcObj.GetComponent<NpcStateMachineRunner>();
                    if (runner != null)
                    {
                        // Ensure it's active so Start() runs and grabs singletons
                        npcObj.SetActive(true); 
                        
                        // Force assign Manager if Start() hasn't happened or to be safe
                        if (runner.Manager == null) 
                        {
                            // If your runner has a public/internal SetManager, use it. 
                            // Otherwise relying on runner.Start() to pick up CustomerManager.Instance is standard.
                        }

                        // 3. Restore Data
                        // This warps them, fills inventory, and sets state
                        runner.RestoreTransientData(data);
                    }
                }
            }
        }

        /// <summary>
        /// Helper to determine load order priority based on the saved state key.
        /// Lower number = Loads First.
        /// </summary>
        private int SortByStatePriority(TransientNpcData a, TransientNpcData b)
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