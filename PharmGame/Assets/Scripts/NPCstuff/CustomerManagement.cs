using UnityEngine;
using System.Collections.Generic;
using Utils.Pooling; // Required for PoolingManager
using Game.NPC; // Required for CustomerAI component
using System.Collections; // Required for Coroutines
using Systems.Inventory; // Required to pass the store inventory reference in Phase 2

namespace CustomerManagement
{
    /// <summary>
    /// Manages the spawning, pooling, and overall flow of customer NPCs in the store.
    /// </summary>
    public class CustomerManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static CustomerManager Instance { get; private set; }

        [Header("NPC Setup")]
        [Tooltip("List of NPC prefabs that this manager can spawn.")]
        [SerializeField] private List<GameObject> npcPrefabs;
        [Tooltip("Maximum number of customers allowed in the store at any given time.")]
        [SerializeField] private int maxCustomersInStore = 5;
        [Tooltip("Minimum time between customer spawns.")]
        [SerializeField] private float minSpawnInterval = 5f;
        [Tooltip("Maximum time between customer spawns.")]
        [SerializeField] private float maxSpawnInterval = 15f;


        [Header("Navigation Points")]
        [Tooltip("Points where customers will enter the store.")]
        [SerializeField] private List<Transform> spawnPoints;
        [Tooltip("Points near shelves where customers will browse.")]
        [SerializeField] private List<Transform> BrowsePoints;
        [Tooltip("Point where customers will wait at the cash register.")]
        [SerializeField] private Transform registerPoint; // Assuming one register point for simplicity
        [Tooltip("Points where customers will exit the store.")]
        [SerializeField] private List<Transform> exitPoints;


        [Header("System References")]
        [Tooltip("Reference to the main store Inventory.")]
        [SerializeField] private Inventory storeInventory; // Reference to pass to CustomerAI (Phase 2)


        // --- Internal State ---
        private PoolingManager poolingManager;
        private List<CustomerAI> activeCustomers = new List<CustomerAI>(); // Track active customers


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // If the manager should persist
            }
            else
            {
                Debug.LogWarning("CustomerManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Get reference to the PoolingManager
            poolingManager = PoolingManager.Instance;
            if (poolingManager == null)
            {
                Debug.LogError("CustomerManager: PoolingManager instance not found! Customer pooling will not work. Please add a PoolingManager to your scene.", this);
                 enabled = false; // Disable if pooling is essential
                 return;
            }

            // Validate essential references
            if (npcPrefabs == null || npcPrefabs.Count == 0) Debug.LogError("CustomerManager: No NPC prefabs assigned!");
            if (spawnPoints == null || spawnPoints.Count == 0) Debug.LogWarning("CustomerManager: No spawn points assigned!");
            if (BrowsePoints == null || BrowsePoints.Count == 0) Debug.LogWarning("CustomerManager: No Browse points assigned!");
            if (registerPoint == null) Debug.LogWarning("CustomerManager: Register point not assigned!");
            if (exitPoints == null || exitPoints.Count == 0) Debug.LogWarning("CustomerManager: No exit points assigned!");
             if (storeInventory == null) Debug.LogError("CustomerManager: Store Inventory reference is not assigned! NPCs cannot shop.");


            Debug.Log("CustomerManager: Awake completed.");
        }

        private void Start()
        {
            // Begin spawning customers
            StartCoroutine(SpawnCustomerCoroutine());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // Clean up any remaining active customers if necessary (e.g., return to pool on scene unload)
                // The PoolingManager also handles destroying pooled objects, but active ones need handling too.
                // This might be handled by OnDisable/OnDestroy in CustomerAI signalling the manager.
                 StopAllCoroutines(); // Stop spawning
            }
            Debug.Log("CustomerManager: OnDestroy completed.");
        }


        /// <summary>
        /// Spawns a new customer from the pool if conditions allow.
        /// </summary>
        public void SpawnCustomer()
        {
            if (poolingManager == null || npcPrefabs == null || npcPrefabs.Count == 0 || spawnPoints == null || spawnPoints.Count == 0 || activeCustomers.Count >= maxCustomersInStore)
            {
                // Conditions not met for spawning (e.g., no pooling manager, no prefabs, no spawn points, max customers reached)
                // Debug.Log("CustomerManager: Cannot spawn customer (conditions not met).");
                return;
            }

            // Select a random NPC prefab
            GameObject npcPrefabToSpawn = npcPrefabs[Random.Range(0, npcPrefabs.Count)];

            // Get a customer instance from the pool
            // Initial pool size for this prefab will be determined by PoolingConfigSO or defaults
            GameObject customerObject = poolingManager.GetPooledObject(npcPrefabToSpawn);

            if (customerObject != null)
            {
                CustomerAI customerAI = customerObject.GetComponent<CustomerAI>();
                if (customerAI != null)
                {
                    // Select a random spawn point
                    Transform chosenSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

                    // Set initial position and rotation at the spawn point
                    customerObject.transform.position = chosenSpawnPoint.position;
                    customerObject.transform.rotation = chosenSpawnPoint.rotation;


                    // --- Initialize the CustomerAI (will be expanded in later phases) ---
                    // Provide necessary references and starting points.
                    customerAI.Initialize(this, chosenSpawnPoint.position);

                    // Add to our list of active customers
                    activeCustomers.Add(customerAI);

                    Debug.Log($"CustomerManager: Spawned customer '{customerObject.name}' from pool at {chosenSpawnPoint.position}. Total active: {activeCustomers.Count}");
                }
                else
                {
                    Debug.LogError($"CustomerManager: Pooled object '{customerObject.name}' does not have a CustomerAI component!", customerObject);
                    // Return the object back to the pool if it's malformed
                     poolingManager.ReturnPooledObject(customerObject);
                }
            }
             else
             {
                 Debug.LogWarning($"CustomerManager: Failed to get pooled object for prefab '{npcPrefabToSpawn.name}'. Pool might be exhausted and cannot grow.");
             }
        }

        /// <summary>
        /// Returns a customer GameObject back to the object pool.
        /// Should be called by the CustomerAI when it's done.
        /// </summary>
        /// <param name="customerObject">The customer GameObject to return.</param>
        public void ReturnCustomerToPool(GameObject customerObject)
        {
            if (customerObject == null || poolingManager == null) return;

            CustomerAI customerAI = customerObject.GetComponent<CustomerAI>();
            if (customerAI != null)
            {
                 // Remove from our list of active customers
                 activeCustomers.Remove(customerAI);
                 Debug.Log($"CustomerManager: Returning customer '{customerObject.name}' to pool. Total active: {activeCustomers.Count}");
            }
             else
             {
                  Debug.LogWarning($"CustomerManager: Attempted to return GameObject '{customerObject.name}' without CustomerAI component.", customerObject);
             }


            // Return the object to the pool
            poolingManager.ReturnPooledObject(customerObject);
        }

        /// <summary>
        /// Coroutine to handle timed customer spawning.
        /// </summary>
        private IEnumerator SpawnCustomerCoroutine()
        {
            while (true) // Loop indefinitely
            {
                float spawnDelay = Random.Range(minSpawnInterval, maxSpawnInterval);
                yield return new WaitForSeconds(spawnDelay);

                SpawnCustomer(); // Attempt to spawn a customer
            }
        }

        // --- Public methods for CustomerAI to request destinations (Phase 1 Step 4) ---

        /// <summary>
        /// Gets a random Browse point transform.
        /// </summary>
        public Transform GetRandomBrowsePoint()
        {
            if (BrowsePoints == null || BrowsePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No Browse points assigned!");
                return null; // Or return a default point
            }
            return BrowsePoints[Random.Range(0, BrowsePoints.Count)];
        }

         /// <summary>
         /// Gets the register point transform.
         /// </summary>
         public Transform GetRegisterPoint()
         {
             if (registerPoint == null)
             {
                 Debug.LogWarning("CustomerManager: Register point not assigned!");
                 return null;
             }
             return registerPoint;
         }

         /// <summary>
         /// Gets a random exit point transform.
         /// </summary>
         public Transform GetRandomExitPoint()
         {
             if (exitPoints == null || exitPoints.Count == 0)
             {
                 Debug.LogWarning("CustomerManager: No exit points assigned!");
                 return null; // Or return a default point
             }
             return exitPoints[Random.Range(0, exitPoints.Count)];
         }

         // Add method to get store inventory in Phase 2
         public Inventory GetStoreInventory()
         {
             return storeInventory;
         }
    }
}