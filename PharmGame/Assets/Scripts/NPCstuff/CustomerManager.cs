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

        [Tooltip("List of Browse locations, pairing a point with its associated inventory.")]
        [SerializeField] private List<BrowseLocation> BrowseLocations;

        [Tooltip("Point where customers will wait at the cash register.")]
        [SerializeField] private Transform registerPoint; 

        [Tooltip("Points where customers will form a queue for the cash register, ordered from closest to furthest.")]
        [SerializeField] private List<Transform> queuePoints;

        [Tooltip("Points where customers will form a secondary queue outside the store, ordered from furthest to closest to the entrance.")]
        [SerializeField] private List<Transform> secondaryQueuePoints;


        [Tooltip("Points where customers will exit the store.")]
        [SerializeField] private List<Transform> exitPoints;


        // --- Internal State ---
        private PoolingManager poolingManager;
        private List<Game.NPC.CustomerAI> activeCustomers = new List<Game.NPC.CustomerAI>(); // Track active customers (explicit namespace use here)
        private Queue<Game.NPC.CustomerAI> customerQueue = new Queue<Game.NPC.CustomerAI>(); // FIFO queue of customers waiting

        private Game.NPC.CustomerAI customerAtRegister = null; // Reference to the customer currently being served or moving to register
        private bool[] queueSpotOccupied; // Tracks if a specific queuePoint index is occupied
        private Queue<Game.NPC.CustomerAI> secondaryCustomerQueue = new Queue<Game.NPC.CustomerAI>(); // FIFO queue of customers waiting in the secondary queue
        private bool[] secondaryQueueSpotOccupied; // Tracks if a specific secondary queuePoint index is occupied
        [Tooltip("If the main register queue has this many customers or fewer, release the next secondary queue customer.")]
        [SerializeField] private int mainQueueReleaseThreshold = 2; // <-- ADD THIS VARIABLE


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

            if (queuePoints == null || queuePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No queue points assigned! Queue system will not function.");
                queueSpotOccupied = new bool[0]; // Initialize as empty if no points
            }
            else
            {
                queueSpotOccupied = new bool[queuePoints.Count];
                // Array is initialized to false by default
                Debug.Log($"CustomerManager: Initialized queue with {queuePoints.Count} spots.");
            }

            if (secondaryQueuePoints == null || secondaryQueuePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No secondary queue points assigned! Secondary queue system will not function.");
                secondaryQueueSpotOccupied = new bool[0];
            }
            else
            {
                secondaryQueueSpotOccupied = new bool[secondaryQueuePoints.Count];
                Debug.Log($"CustomerManager: Initialized secondary queue with {secondaryQueuePoints.Count} spots.");
            }


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

         public Transform GetSecondaryQueuePoint(int index)
         {
            if (secondaryQueuePoints != null && index >= 0 && index < secondaryQueuePoints.Count)
            {
                return secondaryQueuePoints[index];
            }
            Debug.LogWarning($"CustomerManager: Requested secondary queue point index {index} is out of bounds or queuePoints list is null!");
            return null;
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

        /// <summary>
        /// Attempts to add a customer to the queue.
        /// </summary>
        /// <param name="customer">The customer AI trying to join.</param>
        /// <param name="assignedSpot">Output: The Transform of the assigned queue spot, or null.</param>
        /// <param name="spotIndex">Output: The index of the assigned queue spot, or -1.</param>
        /// <returns>True if successfully joined the queue, false otherwise (e.g., queue is full).</returns>
        public bool TryJoinQueue(Game.NPC.CustomerAI customer, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (queuePoints == null || queuePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: Cannot join queue - no queue points defined!");
                return false;
            }
            if (queueSpotOccupied == null || queueSpotOccupied.Length != queuePoints.Count)
            {
                Debug.LogError("CustomerManager: queueSpotOccupied array is not initialized correctly!");
                // Attempt to re-initialize, but indicate failure for this attempt
                if(queuePoints != null) queueSpotOccupied = new bool[queuePoints.Count];
                return false;
            }

            // Find the first available spot
            for (int i = 0; i < queueSpotOccupied.Length; i++)
            {
                if (!queueSpotOccupied[i]) // If this spot is NOT occupied
                {
                    // Assign this spot
                    queueSpotOccupied[i] = true;
                    customerQueue.Enqueue(customer); // Add the customer to the queue
                    assignedSpot = queuePoints[i];
                    spotIndex = i;
                    Debug.Log($"CustomerManager: {customer.gameObject.name} successfully joined queue at spot {i}. Queue size: {customerQueue.Count}");
                    return true; // Successfully joined
                }
            }

            // If loop finishes, no available spots were found
            Debug.Log($"CustomerManager: {customer.gameObject.name} could not join queue - queue is full.");
            return false; // Queue is full
        }

        /// <summary>
        /// Signals that a customer is leaving a specific queue spot in either queue.
        /// </summary>
        /// <param name="type">The type of queue (Main or Secondary).</param>
        /// <param name="spotIndex">The index of the spot being vacated.</param>
        public void SignalQueueSpotFree(QueueType type, int spotIndex)
        {
            Debug.Log($"CustomerManager: Queue spot {spotIndex} in {type} queue signalled free.");
            bool[] occupiedArray = null;
            List<Transform> pointsList = null;
            Queue<Game.NPC.CustomerAI> targetQueue = null; // The queue to check for the next customer

            if (type == QueueType.Main)
            {
                occupiedArray = queueSpotOccupied;
                pointsList = queuePoints;
                targetQueue = customerQueue; // Check the main queue
            }
            else if (type == QueueType.Secondary)
            {
                occupiedArray = secondaryQueueSpotOccupied;
                pointsList = secondaryQueuePoints;
                targetQueue = secondaryCustomerQueue; // Check the secondary queue
            }
            else
            {
                Debug.LogError($"CustomerManager: Received signal for unknown QueueType: {type}!");
                return;
            }

            if (occupiedArray != null && pointsList != null && targetQueue != null && spotIndex >= 0 && spotIndex < occupiedArray.Length)
            {
                occupiedArray[spotIndex] = false;
                Debug.Log($"CustomerManager: Spot {spotIndex} in {type} queue is now free.");

                // --- Logic to potentially move the next customer up ---
                // The next customer to potentially move to this newly freed spot (spotIndex)
                // is the one who was previously assigned to spotIndex + 1 in THIS queue type.
                int nextCustomerSpotIndex = spotIndex + 1;

                // Check if the next spot index is valid within THIS queue's points
                if (nextCustomerSpotIndex < pointsList.Count)
                {
                    // Find the customer in the target queue whose *assigned* spot is nextCustomerSpotIndex.
                    Game.NPC.CustomerAI customerToMove = null;
                    // Iterate the target queue to find the customer assigned to the next spot
                    foreach (var customer in targetQueue)
                    {
                        // Accessing AssignedQueueSpotIndex from the AI's CustomerAI script
                        if (customer.AssignedQueueSpotIndex == nextCustomerSpotIndex)
                        {
                            customerToMove = customer; // Found the customer who should move up
                            break; // Found the relevant customer, stop searching
                        }
                    }

                    if (customerToMove != null)
                    {
                        // We found the customer who should move to spotIndex (the one who was at spotIndex + 1)
                        Debug.Log($"CustomerManager: Signalling {customerToMove.gameObject.name} at spot {nextCustomerSpotIndex} to move up to spot {spotIndex} in {type} queue.");

                        // Tell the customer's BaseQueueLogic component to move to the new spot
                        // Assuming BaseQueueLogic is on the same GameObject as CustomerAI
                        BaseQueueLogic customerQueueLogic = customerToMove.GetComponent<BaseQueueLogic>();
                        if (customerQueueLogic != null)
                        {
                            // Call the MoveToNextQueueSpot method defined in BaseQueueLogic
                            customerQueueLogic.MoveToNextQueueSpot(pointsList[spotIndex], spotIndex);
                        }
                        else
                        {
                            Debug.LogError($"CustomerManager: Could not find BaseQueueLogic component on customer {customerToMove.gameObject.name} to signal move up!");
                        }
                    }
                    else
                    {
                        // This is expected if spotIndex + 1 is the end of this queue's line and no one is there.
                        Debug.Log($"CustomerManager: No customer found waiting for spot {nextCustomerSpotIndex} or beyond in {type} queue.");
                    }
                }
                else
                {
                    // If spotIndex is the last spot in this queue's points list, no one needs to move up *to this spot*.
                    Debug.Log($"CustomerManager: Freed spot {spotIndex} is the last in the {type} queue. No customer behind needs to move up this specific spot.");
                }
                // ----------------------------------------------------
            }
            else
            {
                Debug.LogWarning($"CustomerManager: Received invalid queue spot index {spotIndex} or null array/list/queue for {type} queue to signal free.");
            }
        }
        
        /// <summary>
        /// Releases the next customer from the secondary queue to enter the store.
        /// </summary>
        public void ReleaseNextSecondaryCustomer()
        {
            Debug.Log("CustomerManager: Attempting to release next secondary customer.");
            if (secondaryCustomerQueue.Count > 0)
            {
                Game.NPC.CustomerAI nextCustomer = secondaryCustomerQueue.Dequeue();
                Debug.Log($"CustomerManager: Dequeued {nextCustomer.gameObject.name} from secondary queue. Signalling them to enter store.");

                // Tell the customer's SecondaryQueueLogic to transition to Entering
                // This requires accessing the CustomerSecondaryQueueLogic component
                CustomerSecondaryQueueLogic secondaryLogic = nextCustomer.GetComponent<CustomerSecondaryQueueLogic>();
                if (secondaryLogic != null)
                {
                    secondaryLogic.ReleaseFromSecondaryQueue(); // Call the method on the logic component
                }
                else
                {
                    Debug.LogError($"CustomerManager: Could not find CustomerSecondaryQueueLogic component on customer {nextCustomer.gameObject.name} to signal release!");
                    // Fallback: Return to pool if logic component is missing
                    nextCustomer.SetState(CustomerState.ReturningToPool);
                }

                // The first secondary queue spot (index 0) is now free because the person who was at the front is entering
                SignalQueueSpotFree(QueueType.Secondary, 0); // Signal that the first spot is free
            }
            else
            {
                Debug.Log("CustomerManager: Secondary queue is empty, no one to release.");
            }
        }

        /// <summary>
        /// Signals that the customer at the register has finished and is leaving the register area.
        /// </summary>
        public void SignalRegisterFree()
        {
            Debug.Log("CustomerManager: Register signalled free.");
            customerAtRegister = null;

            // If there are customers in the main queue
            if (customerQueue.Count > 0)
            {
                Game.NPC.CustomerAI nextCustomer = customerQueue.Dequeue();
                Debug.Log($"CustomerManager: Dequeued {nextCustomer.gameObject.name} from main queue. Signalling them to move to register.");

                nextCustomer.GoToRegisterFromQueue(); // Tell them to go to register

                // The first main queue spot (index 0) is now free
                SignalQueueSpotFree(QueueType.Main, 0); // Signal that the first main spot is free

                // --- Check and trigger Secondary Queue release ---
                // AFTER a customer leaves the front of the main queue, check if the main queue is now below the threshold
                if (customerQueue.Count < mainQueueReleaseThreshold) // Check the count *after* dequeuing
                {
                    ReleaseNextSecondaryCustomer(); // Try to release a customer from the secondary queue
                }
                // -------------------------------------------------
            }
            else
            {
                Debug.Log("CustomerManager: Main queue is empty.");
                // If the main queue is now empty, check if we can release a secondary customer
                if (customerQueue.Count < mainQueueReleaseThreshold) // Check threshold (will be 0 here)
                {
                    ReleaseNextSecondaryCustomer();
                }
            }
        }

        /// <summary>
        /// Signals that a customer is currently moving towards or is at the register.
        /// </summary>
        /// <param name="customer">The customer AI that is now occupying the register spot.</param>
        public void SignalCustomerAtRegister(Game.NPC.CustomerAI customer)
        {
            if (customer == null)
            {
                Debug.LogWarning("CustomerManager: SignalCustomerAtRegister called with null customer.");
                return;
            }
            if (customerAtRegister != null && customerAtRegister != customer)
            {
                Debug.LogWarning($"CustomerManager: {customer.gameObject.name} is signalling at register, but {customerAtRegister.gameObject.name} was already there!");
                // This might indicate an issue with state transitions or multiple customers trying to claim the register.
                // For now, we'll just overwrite, but in a real game, you might handle this differently.
            }
            customerAtRegister = customer;
            Debug.Log($"CustomerManager: {customer.gameObject.name} is now registered as being at the register.");
        }

        /// <summary>
        /// Gets the Transform for a specific queue point.
        /// </summary>
        /// <param name="index">The index of the desired queue point.</param>
        /// <returns>The Transform of the queue point, or null if index is out of bounds.</returns>
        public Transform GetQueuePoint(int index)
        {
            if (queuePoints != null && index >= 0 && index < queuePoints.Count)
            {
                return queuePoints[index];
            }
            Debug.LogWarning($"CustomerManager: Requested queue point index {index} is out of bounds or queuePoints list is null!");
            return null;
        }


        /// <summary>
        /// Gets the CustomerAI currently at the register point (either moving, waiting, or in transaction).
        /// </summary>
        public Game.NPC.CustomerAI GetCustomerAtRegister()
        {
            return customerAtRegister;
        }

        /// <summary>
        /// Checks if the register is currently occupied by a customer.
        /// </summary>
        public bool IsRegisterOccupied()
        {
            return customerAtRegister != null;
        }

        /// <summary>
        /// Attempts to add a customer to the secondary queue.
        /// </summary>
        /// <param name="customer">The customer AI trying to join.</param>
        /// <param name="assignedSpot">Output: The Transform of the assigned secondary queue spot, or null.</param>
        /// <param name="spotIndex">Output: The index of the assigned secondary queue spot, or -1.</param>
        /// <returns>True if successfully joined the secondary queue, false otherwise (e.g., queue is full).</returns>
        public bool TryJoinSecondaryQueue(Game.NPC.CustomerAI customer, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (secondaryQueuePoints == null || secondaryQueuePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: Cannot join secondary queue - no points defined!");
                return false;
            }
            if (secondaryQueueSpotOccupied == null || secondaryQueueSpotOccupied.Length != secondaryQueuePoints.Count)
            {
                Debug.LogError("CustomerManager: secondaryQueueSpotOccupied array is not initialized correctly!");
                if(secondaryQueuePoints != null) secondaryQueueSpotOccupied = new bool[secondaryQueuePoints.Count];
                return false;
            }

            // Find the first available spot in the secondary queue
            for (int i = 0; i < secondaryQueueSpotOccupied.Length; i++)
            {
                if (!secondaryQueueSpotOccupied[i]) // If this spot is NOT occupied
                {
                    // Assign this spot
                    secondaryQueueSpotOccupied[i] = true;
                    secondaryCustomerQueue.Enqueue(customer); // Add the customer to the secondary queue
                    assignedSpot = secondaryQueuePoints[i];
                    spotIndex = i;
                    Debug.Log($"CustomerManager: {customer.gameObject.name} successfully joined secondary queue at spot {i}. Secondary queue size: {secondaryCustomerQueue.Count}");
                    return true; // Successfully joined
                }
            }

            // If loop finishes, no available spots were found
            Debug.Log($"CustomerManager: {customer.gameObject.name} could not join secondary queue - secondary queue is full.");
            return false; // Secondary queue is full
        }

        // Add getter for main queue count
        public int GetMainQueueCount()
        {
            return customerQueue.Count;
        }

        // Add getter for checking if main queue is full
        public bool IsMainQueueFull()
        {
            if (queuePoints == null || queuePoints.Count == 0) return false; // Cannot be full if no spots
            // Check if the last spot in the main queue is occupied
            if (queueSpotOccupied == null || queueSpotOccupied.Length == 0) return false; // Should match queuePoints.Count
            return queueSpotOccupied[queueSpotOccupied.Length - 1];
        }
    }
}