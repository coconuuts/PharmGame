using UnityEngine;
using System.Collections.Generic;
using Utils.Pooling; // Required for PoolingManager
using Game.NPC; // Required for CustomerAI component
using System.Collections; // Required for Coroutines
using Systems.Inventory; // Required for Inventory reference
using Random = UnityEngine.Random; // Specify UnityEngine.Random

namespace CustomerManagement
{
    /// <summary>
    /// Represents a location in the store where customers can browse,
    /// linked to the specific inventory at that location.
    /// </summary>
    [System.Serializable] // Make it serializable so it appears in the Inspector
    public struct BrowseLocation // --- CORRECTED NAME: BrowseLocation ---
    {
        [Tooltip("The Transform point where the customer will stand to browse.")]
        public Transform browsePoint;

        [Tooltip("The Inventory component associated with this Browse location (e.g., on the shelves).")]
        public Inventory inventory;

        // Optional: Could add other info like priority, item types found here, etc.
    }


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
        // --- Replaced List<Transform> BrowsePoints and Inventory storeInventory ---
        [Tooltip("List of Browse locations, pairing a point with its associated inventory.")]
        // --- CORRECTED NAME AND TYPE: List<BrowseLocation> BrowseLocations ---
        [SerializeField] private List<BrowseLocation> BrowseLocations;
        // -------------------------------------------------------------------------
        [Tooltip("Point where customers will wait at the cash register.")]
        [SerializeField] private Transform registerPoint; // Assuming one register point for simplicity
        [Tooltip("Points where customers will exit the store.")]
        [SerializeField] private List<Transform> exitPoints;


        // --- Internal State ---
        private PoolingManager poolingManager;
        private List<Game.NPC.CustomerAI> activeCustomers = new List<Game.NPC.CustomerAI>(); // Track active customers (explicit namespace use here)


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
            // --- Updated validation for BrowseLocations ---
            if (BrowseLocations == null || BrowseLocations.Count == 0) Debug.LogError("CustomerManager: No Browse locations assigned!");
             else
             {
                  // Optional: Validate each Browse location entry
                  foreach(var location in BrowseLocations)
                  {
                       if (location.browsePoint == null) Debug.LogWarning("CustomerManager: A Browse location has a null browse point!");
                       if (location.inventory == null) Debug.LogWarning($"CustomerManager: Browse location '{location.browsePoint?.name}' has a null inventory reference!");
                  }
             }
            // ----------------------------------------------
            if (registerPoint == null) Debug.LogWarning("CustomerManager: Register point not assigned!");
            if (exitPoints == null || exitPoints.Count == 0) Debug.LogWarning("CustomerManager: No exit points assigned!");


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
                // Clean up any remaining active customers if necessary
                 StopAllCoroutines(); // Stop spawning
            }
            Debug.Log("CustomerManager: OnDestroy completed.");
        }


        /// <summary>
        /// Spawns a new customer from the pool if conditions allow.
        /// </summary>
        public void SpawnCustomer()
        {
            // --- Corrected field name: BrowseLocations ---
            if (poolingManager == null || npcPrefabs == null || npcPrefabs.Count == 0 || spawnPoints == null || spawnPoints.Count == 0 || activeCustomers.Count >= maxCustomersInStore || BrowseLocations == null || BrowseLocations.Count == 0)
            {
                // Conditions not met for spawning
                // Debug.Log("CustomerManager: Cannot spawn customer (conditions not met).");
                return;
            }

            // Select a random NPC prefab
            GameObject npcPrefabToSpawn = npcPrefabs[Random.Range(0, npcPrefabs.Count)];

            // Get a customer instance from the pool
            GameObject customerObject = poolingManager.GetPooledObject(npcPrefabToSpawn);

            if (customerObject != null)
            {
                // Using fully qualified name Game.NPC.CustomerAI just to be extra clear
                Game.NPC.CustomerAI customerAI = customerObject.GetComponent<Game.NPC.CustomerAI>();
                if (customerAI != null)
                {
                    // Select a random spawn point
                    Transform chosenSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

                    // Set initial position and rotation at the spawn point
                    customerObject.transform.position = chosenSpawnPoint.position;
                    customerObject.transform.rotation = chosenSpawnPoint.rotation;


                    // --- Initialize the CustomerAI ---
                    customerAI.Initialize(this, chosenSpawnPoint.position); // Initialize with manager and start pos

                    // Add to our list of active customers
                    activeCustomers.Add(customerAI);

                    Debug.Log($"CustomerManager: Spawned customer '{customerObject.name}' from pool at {chosenSpawnPoint.position}. Total active: {activeCustomers.Count}");
                }
                else
                {
                    Debug.LogError($"CustomerManager: Pooled object '{customerObject.name}' does not have a CustomerAI component!", customerObject);
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

            // Using fully qualified name Game.NPC.CustomerAI
            Game.NPC.CustomerAI customerAI = customerObject.GetComponent<Game.NPC.CustomerAI>();
            if (customerAI != null)
            {
                 // Remove from our list of active customers
                 activeCustomers.Remove(customerAI);
                 Debug.Log($"CustomerManager: Returning customer '{customerObject.name}' to pool. Total active: {activeCustomers.Count}");

                 // --- FIX: Added the call to return the object to the pool ---
                 poolingManager.ReturnPooledObject(customerObject);
                 // --------------------------------------------------------
            }
             else
             {
                  Debug.LogWarning($"CustomerManager: Attempted to return GameObject '{customerObject.name}' without CustomerAI component.", customerObject);
                  // If it has pooledObjectInfo, return it anyway, otherwise destroy
                  if(customerObject.GetComponent<PooledObjectInfo>() != null)
                  {
                       poolingManager.ReturnPooledObject(customerObject); // This was already here as a fallback
                  }
                  else
                  {
                       Destroy(customerObject);
                  }
             }
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

        // --- Public methods for CustomerAI to request navigation/system info ---

        /// <summary>
        /// Gets a random Browse location (point and associated inventory).
        /// </summary>
        public BrowseLocation? GetRandomBrowseLocation() // --- CORRECTED RETURN TYPE: BrowseLocation? ---
        {
            // --- Corrected field name: BrowseLocations ---
            if (BrowseLocations == null || BrowseLocations.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No Browse locations assigned!");
                return null; // Return null for nullable struct
            }
            return BrowseLocations[Random.Range(0, BrowseLocations.Count)];
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
                 return null;
             }
             return exitPoints[Random.Range(0, exitPoints.Count)];
         }
    }
}