using UnityEngine;
using System.Collections.Generic;
using System; // Needed for Action
using System.Linq; // --- FIX for CS1061: Add System.Linq ---

namespace Utils.Pooling // Same namespace
{
    // PooledObjectInfo is defined in its own script: PooledObjectInfo.cs
    // internal class PooledObjectInfo : MonoBehaviour { ... } // Removed from here


    /// <summary>
    /// An object pool specifically for managing GameObject instances directly.
    /// Uses a configuration struct to define behavior.
    /// </summary>
    public class GameObjectPool
    {
        private GameObjectPoolConfig config; // Store the configuration for this pool
        private Transform parentTransform; // The parent for pooled objects in the hierarchy
        private Stack<GameObject> availableObjects = new Stack<GameObject>(); // The stack of available pooled GameObjects
        private HashSet<GameObject> activeObjects = new HashSet<GameObject>(); // Track objects currently in use

        // Optional: Events for when an object is requested or returned, for custom setup/cleanup
        public event Action<GameObject> OnObjectGet;
        public event Action<GameObject> OnObjectReturn;

        /// <summary>
        /// Constructor for the GameObjectPool.
        /// </summary>
        /// <param name="config">The configuration for this pool.</param>
        /// <param name="parentTransform">The transform to parent pooled objects under (optional).</param>
        public GameObjectPool(GameObjectPoolConfig config, Transform parentTransform = null) // Constructor takes config
        {
            // Storing a copy of the struct to avoid external modification
            this.config = config; // Store the configuration

            if (this.config.prefab == null) // Use this.config.prefab
            {
                Debug.LogError("GameObjectPool: Prefab provided in config is null!");
                // Cannot proceed without a valid prefab
                // Consider throwing an exception or setting an invalid state flag
                return;
            }

            this.parentTransform = parentTransform;

            // Pre-populate the pool based on initialSize from config
            for (int i = 0; i < this.config.initialSize; i++) // Use this.config.initialSize
            {
                CreateAndPoolObject();
            }

            Debug.Log($"GameObjectPool: Created pool for '{this.config.prefab.name}' with initial size {this.config.initialSize}, max size {this.config.maxSize}, can grow {this.config.canGrow}."); // Use this.config
        }

        /// <summary>
        /// Gets a GameObject from the pool. Creates a new one if none are available and allowed to grow.
        /// </summary>
        /// <returns>An active GameObject from the pool, or null if none available and cannot grow.</returns>
        public GameObject Get()
        {
            // Use this.config throughout
            if (this.config.prefab == null) // Defensive check if somehow initialized with null prefab
            {
                 Debug.LogError("GameObjectPool: Cannot get object, pool was initialized with null prefab!");
                 return null;
            }

            GameObject obj = null;

            if (availableObjects.Count > 0)
            {
                obj = availableObjects.Pop();
            }
            else // Pool is empty
            {
                // Check if we can grow or if there's a max size
                // Calculate current total count (available + active)
                int currentTotalCount = availableObjects.Count + activeObjects.Count;
                bool canCreateNew = this.config.canGrow && (this.config.maxSize == 0 || currentTotalCount < this.config.maxSize);

                if (canCreateNew)
                {
                    Debug.LogWarning($"GameObjectPool: Pool for '{this.config.prefab.name}' is empty. Creating a new instance.");
                    obj = CreateInstance();
                }
                else
                {
                    Debug.LogWarning($"GameObjectPool: Pool for '{this.config.prefab.name}' is empty and cannot grow or has reached max size ({this.config.maxSize}). Returning null.");
                    return null; // Cannot get an object
                }
            }

            if (obj != null)
            {
                 // Activate and prepare the object
                 obj.SetActive(true);
                 // Add to the set of active objects
                 activeObjects.Add(obj); // Add to active tracking BEFORE invoking events
                 OnObjectGet?.Invoke(obj); // Trigger event for custom setup
            }

            return obj;
        }

        /// <summary>
        /// Returns a GameObject to the pool.
        /// </summary>
        /// <param name="obj">The GameObject instance to return.</param>
        public void Return(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("GameObjectPool: Attempted to return a null object.");
                return;
            }

            // Remove from the set of active objects
            if (!activeObjects.Remove(obj))
            {
                 // This object was not in our list of active objects.
                 // Could indicate it was returned twice or belongs to a different pool/origin.
                 Debug.LogWarning($"GameObjectPool: Attempted to return object '{obj.name}' that was not tracked as active in this pool. Destroying instead.", obj);
                 GameObject.Destroy(obj); // Destroy instead of pooling potentially foreign object
                 return; // Stop processing
            }


            OnObjectReturn?.Invoke(obj); // Trigger event for custom cleanup

            // Deactivate and return the object to the pool
            obj.SetActive(false);

            // Check if we are within the max size before returning to the available stack
            // Note: activeObjects count has already been reduced by Remove(obj) above
            if (this.config.maxSize > 0 && availableObjects.Count + activeObjects.Count >= this.config.maxSize)
            {
                 Debug.LogWarning($"GameObjectPool: Pool for '{this.config.prefab.name}' has reached max size ({this.config.maxSize}). Destroying returned object instead of pooling.");
                 GameObject.Destroy(obj); // Destroy if over max size
            }
            else
            {
                 availableObjects.Push(obj); // Return to available stack
            }
        }

        /// <summary>
        /// Creates a new instance of the prefab and adds PooledObjectInfo.
        /// </summary>
        private GameObject CreateInstance()
        {
            if (this.config.prefab == null) // Use this.config.prefab
            {
                Debug.LogError("GameObjectPool: Cannot create instance, prefab in config is null!");
                return null;
            }

            GameObject instanceGo = GameObject.Instantiate(this.config.prefab, parentTransform); // Use this.config.prefab

            // --- Use PoolingManager helper to add PooledObjectInfo ---
             if (PoolingManager.Instance == null)
             {
                  Debug.LogError("GameObjectPool: PoolingManager instance not found! Cannot add PooledObjectInfo.");
             }
             else
             {
                 PoolingManager.Instance.AddPooledObjectInfo(instanceGo, this.config.prefab); // Use this.config.prefab
             }
            // --------------------------------------------------------


            // Initially deactivate new instances until they are requested via Get()
            instanceGo.SetActive(false);

            return instanceGo; // Return the GameObject directly
        }

         /// <summary>
         /// Creates a new instance and immediately returns it to the pool (for pre-population).
         /// </summary>
        private void CreateAndPoolObject()
        {
            GameObject newObj = CreateInstance();
            if (newObj != null)
            {
                availableObjects.Push(newObj);
            }
        }

         /// <summary>
         /// Destroys all GameObjects currently held in the pool AND all objects currently active.
         /// Clears the active and available lists.
         /// </summary>
         public void DestroyAllPooledObjects()
         {
             // Destroy available objects
             while(availableObjects.Count > 0)
             {
                  GameObject obj = availableObjects.Pop();
                  if (obj != null)
                  {
                       GameObject.Destroy(obj);
                  }
             }
             availableObjects.Clear();

             // Destroy active objects
             // Use ToList() because destroying objects might modify the activeObjects HashSet during iteration
             foreach(var obj in activeObjects.ToList()) // --- FIX for CS1061: System.Linq is used and ToList() is available ---
             {
                 if (obj != null)
                 {
                     GameObject.Destroy(obj);
                 }
             }
             activeObjects.Clear();

             Debug.Log($"GameObjectPool: Destroyed all objects for prefab '{config.prefab?.name ?? "Null Prefab"}'."); // Use config.prefab
         }
    }
}