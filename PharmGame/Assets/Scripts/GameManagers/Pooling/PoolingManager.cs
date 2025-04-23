using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq; // Needed for FirstOrDefault and ToList()

namespace Utils.Pooling // Same namespace as pools
{
    // PooledObjectInfo is now in its own script: PooledObjectInfo.cs
    // internal class PooledObjectInfo : MonoBehaviour { ... } // Removed from here


    /// <summary>
    /// Singleton manager for accessing various object pools (GameObject and Component).
    /// Initializes pools based on a configuration ScriptableObject.
    /// </summary>
    public class PoolingManager : MonoBehaviour
    {
        // Singleton instance
        public static PoolingManager Instance { get; private set; }

        [Tooltip("Assign the ScriptableObject with pooling configurations.")]
        [SerializeField] private PoolingConfigSO poolingConfig; // Assign the SO in the inspector

        // Dictionary to hold GameObject pools, keyed by the prefab GameObject
        private Dictionary<GameObject, GameObjectPool> gameObjectPools = new Dictionary<GameObject, GameObjectPool>();

        // Dictionary to hold Component pools (if needed later)
        // private Dictionary<(GameObject prefab, Type componentType), IComponentPool> componentPools; // Example


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // If the manager should persist between scenes
            }
            else
            {
                Debug.LogWarning("PoolingManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Debug.Log("PoolingManager: Awake completed.");

            // --- Pre-initialize pools based on configuration ---
            InitializePoolsFromConfig();
            // -------------------------------------------------
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                 // Clean up all managed pools when the manager is destroyed
                 DestroyAllManagedPools();
            }
             Debug.Log("PoolingManager: OnDestroy completed.");
        }

        /// <summary>
        /// Initializes pools based on the assigned PoolingConfigSO.
        /// Called in Awake.
        /// </summary>
        private void InitializePoolsFromConfig()
        {
            if (poolingConfig == null)
            {
                Debug.LogError("PoolingManager: Pooling Configuration ScriptableObject is not assigned! No pools will be pre-initialized.");
                return;
            }

            if (poolingConfig.gameObjectPoolConfigs != null)
            {
                Debug.Log($"PoolingManager: Initializing {poolingConfig.gameObjectPoolConfigs.Count} GameObject pools from configuration.");
                foreach (var poolConfig in poolingConfig.gameObjectPoolConfigs)
                {
                    if (poolConfig.prefab != null)
                    {
                        // Check if a pool already exists for this prefab (shouldn't if called only in Awake)
                        if (gameObjectPools.ContainsKey(poolConfig.prefab))
                        {
                            Debug.LogWarning($"PoolingManager: Pool for prefab '{poolConfig.prefab.name}' already exists during initialization from config. Skipping.");
                            continue; // Skip if pool already exists
                        }

                        // Create a new GameObjectPool instance with the config
                        Transform poolParent = this.transform; // Parent pooled objects under the PoolingManager GO
                        GameObjectPool newPool = new GameObjectPool(poolConfig, poolParent);
                        gameObjectPools.Add(poolConfig.prefab, newPool);
                    }
                    else
                    {
                         Debug.LogWarning("PoolingManager: Skipping pool configuration with null prefab.");
                    }
                }
            }
             else
             {
                 Debug.LogWarning("PoolingManager: GameObject pool configurations list in the ScriptableObject is null or empty.");
             }

            // Initialize Component pools here if needed later...
        }


        /// <summary>
        /// Gets a pooled GameObject instance for the specified prefab.
        /// Creates a new pool lazily if it wasn't pre-initialized by config.
        /// Uses the configuration (if found) for growth/size rules.
        /// </summary>
        /// <param name="prefab">The prefab to get a pooled instance of.</param>
        /// <returns>An active GameObject instance from the pool, or null if none available and cannot grow.</returns>
/// <summary>
        /// Gets a pooled GameObject instance for the specified prefab.
        /// Creates a new pool lazily if it wasn't pre-initialized by config.
        /// Uses the configuration (if found) for growth/size rules.
        /// </summary>
        /// <param name="prefab">The prefab to get a pooled instance of.</param>
        /// <returns>An active GameObject instance from the pool, or null if none available and cannot grow.</returns>
        public GameObject GetPooledObject(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("PoolingManager: Cannot get pooled object, prefab is null!");
                return null;
            }

            // Find or create the pool for this prefab
            if (!gameObjectPools.TryGetValue(prefab, out GameObjectPool pool))
            {
                // Pool doesn't exist. Determine its configuration based on SO or defaults.

                // --- Initialize configToUse with the default configuration ---
                // This guarantees configToUse has a value regardless of the subsequent checks.
                GameObjectPoolConfig configToUse = new GameObjectPoolConfig { prefab = prefab, initialSize = 0, maxSize = 0, canGrow = true };
                Debug.LogWarning($"PoolingManager: Pool for '{prefab.name}' not found. Initializing config with default settings (size 0, no max, can grow).");
                // ----------------------------------------------------------


                // Try to find configuration for this prefab in the SO
                if (poolingConfig != null && poolingConfig.gameObjectPoolConfigs != null)
                {
                     GameObjectPoolConfig foundPoolConfig = poolingConfig.gameObjectPoolConfigs.FirstOrDefault(c => c.prefab == prefab);

                     // Check if FirstOrDefault actually found an entry for this prefab with a matching prefab.
                     // FirstOrDefault on a list of structs returns the default struct if not found.
                     // The default struct has all fields as default values (prefab would be null or default).
                     // So, checking if foundPoolConfig.prefab matches the requested prefab and isn't null
                     // is a way to confirm a valid configuration entry was found in the list.
                     if (foundPoolConfig.prefab == prefab && prefab != null)
                     {
                         // --- Overwrite configToUse with the configuration from the SO ---
                         configToUse = foundPoolConfig;
                         Debug.Log($"PoolingManager: Found and applied configuration from SO for '{prefab.name}'.");
                         // -------------------------------------------------------------
                     }
                      else
                     {
                         // If FirstOrDefault returned the default struct, it means the prefab wasn't in the list.
                         Debug.Log($"PoolingManager: No specific configuration found in SO for '{prefab.name}'. Using initial default config.");
                     }
                }
                 else
                 {
                     // If poolingConfig or its list is null, we'll stick with the initial default config.
                     Debug.LogWarning("PoolingManager: Pooling Configuration ScriptableObject or its list is null. Using initial default config.");
                 }

                // At this point, configToUse is GUARANTEED to be assigned (either the initial default or overwritten by SO config).

                // Create the pool using the determined config
                Transform poolParent = this.transform; // Parent pooled objects under the PoolingManager GO
                pool = new GameObjectPool(configToUse, poolParent); // Use the guaranteed assigned configToUse
                gameObjectPools.Add(prefab, pool);
            }

            // Now that we have the pool, get an object from it
            return pool.Get();
        }

        /// <summary>
        /// Returns a pooled GameObject instance back to its pool.
        /// It finds the correct pool based on the object's original prefab via PooledObjectInfo.
        /// </summary>
        /// <param name="objectToReturn">The GameObject instance to return.</param>
        public void ReturnPooledObject(GameObject objectToReturn)
        {
            if (objectToReturn == null)
            {
                Debug.LogWarning("PoolingManager: Attempted to return a null object.");
                return;
            }

             PooledObjectInfo poolInfo = objectToReturn.GetComponent<PooledObjectInfo>();

             if (poolInfo == null || poolInfo.OriginalPrefab == null)
             {
                 Debug.LogWarning($"PoolingManager: Object '{objectToReturn.name}' does not have PooledObjectInfo or OriginalPrefab assigned. Cannot return to pool. Destroying instead.", objectToReturn);
                 GameObject.Destroy(objectToReturn); // Destroy if it can't be pooled
                 return;
             }

             // Find the correct pool based on the original prefab
             if (gameObjectPools.TryGetValue(poolInfo.OriginalPrefab, out GameObjectPool pool))
             {
                 pool.Return(objectToReturn);
             }
             else
             {
                 // This can happen if an object from a pool that was destroyed is returned,
                 // or if it was created lazily and the manager was reloaded/reset.
                 Debug.LogWarning($"PoolingManager: Pool for prefab '{poolInfo.OriginalPrefab.name}' not found for object '{objectToReturn.name}'. Cannot return to pool. Destroying instead.", objectToReturn);
                 GameObject.Destroy(objectToReturn); // Destroy if pool is missing
             }
        }

         /// <summary>
         /// Helper method called by pools to add PooledObjectInfo to a new instance.
         /// Needs to be public or internal so pools can access it.
         /// </summary>
         /// <param name="instanceGo">The newly created GameObject instance.</param>
         /// <param name="originalPrefab">The original prefab this instance came from.</param>
         public void AddPooledObjectInfo(GameObject instanceGo, GameObject originalPrefab)
         {
              if (instanceGo == null || originalPrefab == null) return;
              // Ensure we don't add multiple PooledObjectInfo components
              if (instanceGo.GetComponent<PooledObjectInfo>() == null)
              {
                  PooledObjectInfo poolInfo = instanceGo.AddComponent<PooledObjectInfo>();
                  poolInfo.OriginalPrefab = originalPrefab;
              }
              else
              {
                  Debug.LogWarning($"PoolingManager: Tried to add PooledObjectInfo to '{instanceGo.name}' which already has one.", instanceGo);
              }
         }


         /// <summary>
         /// Destroys all objects across all managed pools.
         /// </summary>
        private void DestroyAllManagedPools()
        {
            Debug.Log("PoolingManager: Destroying all managed pools.");
            // Iterate over a copy in case DestroyAllPooledObjects modifies the collection
            foreach(var pool in gameObjectPools.Values.ToList()) // ToList() is fine here with System.Linq
            {
                pool.DestroyAllPooledObjects();
            }
            gameObjectPools.Clear();
            // Destroy Component pools here if needed later...
             Debug.Log("PoolingManager: All pools destroyed.");
        }
    }
}