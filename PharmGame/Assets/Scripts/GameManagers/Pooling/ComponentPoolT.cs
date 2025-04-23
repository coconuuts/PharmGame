using UnityEngine;
using System.Collections.Generic;
using System; // Needed for Action

namespace Utils.Pooling // A common namespace for utility systems
{
    /// <summary>
    /// A generic object pool for managing instances of a specific Component type.
    /// Instances are created from a GameObject prefab.
    /// </summary>
    /// <typeparam name="T">The type of Component to pool.</typeparam>
    public class ComponentPool<T> where T : Component // Correct constraint
    {
        private GameObject prefab; // The prefab this pool creates instances from
        private Transform parentTransform; // The parent for pooled objects in the hierarchy
        private Stack<T> availableObjects = new Stack<T>(); // The stack of available pooled objects

        // Optional: Events for when an object is requested or returned, for custom setup/cleanup
        public event Action<T> OnObjectGet;
        public event Action<T> OnObjectReturn;

        /// <summary>
        /// Constructor for the ComponentPool.
        /// </summary>
        /// <param name="prefab">The GameObject prefab containing the component T.</param>
        /// <param name="initialSize">The initial number of objects (GameObjects) to create in the pool.</param>
        /// <param name="parentTransform">The transform to parent pooled objects under (optional).</param>
        public ComponentPool(GameObject prefab, int initialSize, Transform parentTransform = null) // Corrected constructor name
        {
            if (prefab == null)
            {
                Debug.LogError("ComponentPool: Prefab provided is null!");
                return;
            }

            if (prefab.GetComponent<T>() == null)
            {
                 Debug.LogError($"ComponentPool: Prefab '{prefab.name}' does not have a component of type {typeof(T).Name} required by the pool!");
                 return;
            }

            this.prefab = prefab;
            this.parentTransform = parentTransform;

            // Pre-populate the pool
            for (int i = 0; i < initialSize; i++)
            {
                CreateAndPoolObject();
            }

            Debug.Log($"ComponentPool: Created pool for '{prefab.name}' ({typeof(T).Name}) with initial size {initialSize}.");
        }

        /// <summary>
        /// Gets an object (Component T) from the pool. Creates a new one if none are available.
        /// The associated GameObject is activated.
        /// </summary>
        /// <returns>An available Component T from the pool.</returns>
        public T Get()
        {
            T obj;
            if (availableObjects.Count > 0)
            {
                obj = availableObjects.Pop();
            }
            else
            {
                // Pool is empty, create a new instance (can add growth logic here)
                Debug.LogWarning($"ComponentPool: Pool for '{prefab.name}' ({typeof(T).Name}) is empty. Creating a new instance.");
                obj = CreateInstance();
            }

            if (obj != null && obj.gameObject != null)
            {
                 // Activate and prepare the object's GameObject
                 obj.gameObject.SetActive(true);
                 OnObjectGet?.Invoke(obj); // Trigger event for custom setup
            }
             else if (obj == null)
             {
                  Debug.LogError($"ComponentPool: Failed to get or create instance for '{prefab.name}' ({typeof(T).Name}).");
             }


            return obj;
        }

        /// <summary>
        /// Returns an object (Component T) to the pool.
        /// The associated GameObject is deactivated.
        /// </summary>
        /// <param name="obj">The Component T instance to return.</param>
        public void Return(T obj)
        {
            if (obj == null || obj.gameObject == null)
            {
                Debug.LogWarning("ComponentPool: Attempted to return a null object or object with null GameObject.");
                return;
            }

            // You might want to add a check here to ensure the object belongs to this pool (e.g., compare origins via PooledObjectInfo)
            // For simplicity now, we trust that only objects from this pool are returned.

            OnObjectReturn?.Invoke(obj); // Trigger event for custom cleanup

            // Deactivate and return the object's GameObject to the pool
            obj.gameObject.SetActive(false);
            availableObjects.Push(obj);
        }

        /// <summary>
        /// Creates a new instance of the prefab, adds PooledObjectInfo, and gets the required component T.
        /// </summary>
        private T CreateInstance()
        {
            if (prefab == null)
            {
                Debug.LogError("ComponentPool: Cannot create instance, prefab is null!");
                return null; // Return default value for T (null for reference types)
            }

            GameObject instanceGo = GameObject.Instantiate(prefab, parentTransform);

            // --- Use PoolingManager helper to add PooledObjectInfo ---
             if (PoolingManager.Instance == null) // Defensive check
             {
                  Debug.LogError("ComponentPool: PoolingManager instance not found! Cannot add PooledObjectInfo.");
                   // Decide how to handle - destroy the instance? Pool without info (risky)?
                   // Let's proceed without info for now, will warn later if returned.
             }
             else
             {
                 // Use the helper method on PoolingManager to add the component and store info
                 PoolingManager.Instance.AddPooledObjectInfo(instanceGo, this.prefab);
             }
            // --------------------------------------------------------


            // Initially deactivate new instances until they are requested via Get()
            instanceGo.SetActive(false);

            // Get and return the required component
            T component = instanceGo.GetComponent<T>();
            if (component == null)
            {
                 Debug.LogError($"ComponentPool: Created instance from prefab '{prefab.name}' but could not find component of type {typeof(T).Name}!");
                 // Decide how to handle - destroy the instance?
                 GameObject.Destroy(instanceGo);
                 return null;
            }

            return component;
        }

         /// <summary>
         /// Creates a new instance (GameObject and component T) and immediately returns the component to the pool (for pre-population).
         /// </summary>
        private void CreateAndPoolObject()
        {
            T newObj = CreateInstance();
            if (newObj != null)
            {
                availableObjects.Push(newObj);
            }
        }

         /// <summary>
         /// Destroys all GameObjects associated with the components currently held in the pool.
         /// </summary>
         public void DestroyAllPooledObjects()
         {
             while(availableObjects.Count > 0)
             {
                  T obj = availableObjects.Pop();
                  if (obj != null && obj.gameObject != null)
                  {
                       GameObject.Destroy(obj.gameObject); // Destroy the GameObject
                  }
             }
             // Note: This only destroys objects currently *in* the pool.
             // Objects currently *in use* (active in the scene) will not be destroyed by this.
         }
    }
}