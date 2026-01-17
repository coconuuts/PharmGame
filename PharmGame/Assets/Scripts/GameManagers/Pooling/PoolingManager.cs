using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Utils.Pooling
{
    /// <summary>
    /// Singleton manager for accessing various object pools (GameObject and Component).
    /// Initializes pools based on a configuration ScriptableObject.
    /// </summary>
    public class PoolingManager : MonoBehaviour
    {
        // Singleton instance
        public static PoolingManager Instance { get; private set; }

        [Tooltip("Assign the ScriptableObject with pooling configurations.")]
        [SerializeField] private PoolingConfigSO poolingConfig;

        private Dictionary<GameObject, GameObjectPool> gameObjectPools = new Dictionary<GameObject, GameObjectPool>();
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); 
            }
            else
            {
                Debug.LogWarning("PoolingManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Debug.Log("PoolingManager: Awake completed.");
            InitializePoolsFromConfig();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                DestroyAllManagedPools();
            }
            Debug.Log("PoolingManager: OnDestroy completed.");
        }

        private void InitializePoolsFromConfig()
        {
            if (isInitialized) return;

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
                        if (gameObjectPools.ContainsKey(poolConfig.prefab))
                        {
                            Debug.LogWarning($"PoolingManager: Pool for prefab '{poolConfig.prefab.name}' already exists during initialization from config. Skipping.");
                            continue;
                        }

                        Transform poolParent = this.transform;
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
            isInitialized = true;
        }

        public GameObject GetPooledObject(GameObject prefab)
        {
            // Ensure initialization has happened before we try to look up pools.
            if (!isInitialized) 
            {
                InitializePoolsFromConfig();
            }
            
            if (prefab == null)
            {
                Debug.LogError("PoolingManager: Cannot get pooled object, prefab is null!");
                return null;
            }

            if (!gameObjectPools.TryGetValue(prefab, out GameObjectPool pool))
            {
                GameObjectPoolConfig configToUse = new GameObjectPoolConfig { prefab = prefab, initialSize = 0, maxSize = 0, canGrow = true };
                Debug.LogWarning($"PoolingManager: Pool for '{prefab.name}' not found. Initializing config with default settings (size 0, no max, can grow).");

                if (poolingConfig != null && poolingConfig.gameObjectPoolConfigs != null)
                {
                    GameObjectPoolConfig foundPoolConfig = poolingConfig.gameObjectPoolConfigs.FirstOrDefault(c => c.prefab == prefab);
                    if (foundPoolConfig.prefab == prefab && prefab != null)
                    {
                        configToUse = foundPoolConfig;
                        Debug.Log($"PoolingManager: Found and applied configuration from SO for '{prefab.name}'.");
                    }
                    else
                    {
                        Debug.Log($"PoolingManager: No specific configuration found in SO for '{prefab.name}'. Using initial default config.");
                    }
                }
                else
                {
                    Debug.LogWarning("PoolingManager: Pooling Configuration ScriptableObject or its list is null. Using initial default config.");
                }

                Transform poolParent = this.transform;
                pool = new GameObjectPool(configToUse, poolParent);
                gameObjectPools.Add(prefab, pool);
            }

            GameObject pooledObject = pool.Get();
            // Call the reset method immediately after getting the object from the pool
            if (pooledObject != null)
            {
                ResetPooledObject(pooledObject);
            }
            return pooledObject;
        }

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
                GameObject.Destroy(objectToReturn);
                return;
            }

            // Find the correct pool based on the original prefab
            if (gameObjectPools.TryGetValue(poolInfo.OriginalPrefab, out GameObjectPool pool))
            {
                // Before returning, deactivate it and potentially reset some common properties here if needed.
                // However, it's often better to reset *before* getting, as individual components might need
                // to react to activation/deactivation during their own OnEnable/OnDisable.
                // For now, the ResetPooledObject is called upon GetPooledObject.
                pool.Return(objectToReturn);
            }
            else
            {
                Debug.LogWarning($"PoolingManager: Pool for prefab '{poolInfo.OriginalPrefab.name}' not found for object '{objectToReturn.name}'. Cannot return to pool. Destroying instead.", objectToReturn);
                GameObject.Destroy(objectToReturn);
            }
        }

        public void AddPooledObjectInfo(GameObject instanceGo, GameObject originalPrefab)
        {
            if (instanceGo == null || originalPrefab == null) return;
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
        /// Resets common properties of a pooled GameObject to ensure it's ready for reuse.
        /// Call this immediately after getting an object from the pool.
        /// </summary>
        /// <param name="obj">The GameObject to reset.</param>
        public void ResetPooledObject(GameObject obj)
        {
            if (obj == null) return;

            // Reset Transform:
            obj.transform.localPosition = Vector3.zero; // Reset local position
            obj.transform.localRotation = Quaternion.identity; // Reset local rotation
            obj.transform.localScale = Vector3.one; // Reset local scale

            // Reset Rigidbody (if present):
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false; // Ensure physics is enabled unless specific for the prefab
                rb.useGravity = true; // Ensure gravity is enabled unless specific for the prefab
                // If you have specific physics layers, you might need to reset them here as well
            }

            // Reset Collider (if present):
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true; // Ensure collider is enabled
            }

            // Reset other common components or states:
            // For example, if your pills have a custom script that manages their state:
            // PillScript pillScript = obj.GetComponent<PillScript>();
            // if (pillScript != null)
            // {
            //     pillScript.ResetPillState(); // A custom method to reset specific pill properties
            // }

            // Ensure the object is active (though Get() usually handles this)
            obj.SetActive(true);
        }

        private void DestroyAllManagedPools()
        {
            Debug.Log("PoolingManager: Destroying all managed pools.");
            foreach (var pool in gameObjectPools.Values.ToList())
            {
                pool.DestroyAllPooledObjects();
            }
            gameObjectPools.Clear();
            Debug.Log("PoolingManager: All pools destroyed.");
        }
    }
}