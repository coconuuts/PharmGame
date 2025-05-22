using UnityEngine;
using System.Collections.Generic;
using Utils.Pooling; // Required for PoolingManager
using Game.NPC; 
using System.Collections; // Required for Coroutines
using Systems.Inventory; // Required for Inventory reference
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using Game.Events;
using Game.NPC.States;

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
        private List<Game.NPC.NpcStateMachineRunner> activeCustomers = new List<Game.NPC.NpcStateMachineRunner>(); // Track active customers (explicit namespace use here)
        private Queue<Game.NPC.NpcStateMachineRunner> customerQueue = new Queue<Game.NPC.NpcStateMachineRunner>(); // FIFO queue of customers waiting
        private Game.NPC.NpcStateMachineRunner customerAtRegister = null; // Reference to the customer currently being served or moving to register
        private bool[] queueSpotOccupied; // Tracks if a specific queuePoint index is occupied
        private Queue<Game.NPC.NpcStateMachineRunner> secondaryCustomerQueue = new Queue<Game.NPC.NpcStateMachineRunner>(); // FIFO queue of customers waiting in the secondary queue
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
        
        private void OnEnable() // Subscribe to events when the GameObject is enabled
        {
            // Subscribe to events published by NPCs or other systems
            EventManager.Subscribe<NpcReturningToPoolEvent>(HandleNpcReturningToPool);
            EventManager.Subscribe<QueueSpotFreedEvent>(HandleQueueSpotFreed);
            EventManager.Subscribe<CashRegisterFreeEvent>(HandleCashRegisterFree);
            // Subscribe to future interruption events if CustomerManager needs to react
            // EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);

            Debug.Log("CustomerManager: Subscribed to events.");
        }

        private void OnDisable() // Unsubscribe from events when the GameObject is disabled
        {
            // Unsubscribe from events to prevent memory leaks and calls on null objects
            EventManager.Unsubscribe<NpcReturningToPoolEvent>(HandleNpcReturningToPool);
            EventManager.Unsubscribe<QueueSpotFreedEvent>(HandleQueueSpotFreed);
            EventManager.Unsubscribe<CashRegisterFreeEvent>(HandleCashRegisterFree);
            // Unsubscribe from future interruption events
            // EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);

            Debug.Log("CustomerManager: Unsubscribed from events.");
            StopAllCoroutines(); 
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
            if (poolingManager == null || npcPrefabs == null || npcPrefabs.Count == 0 || spawnPoints == null || spawnPoints.Count == 0 || activeCustomers.Count >= maxCustomersInStore || BrowseLocations == null || BrowseLocations.Count == 0)
            {
                return;
            }

            GameObject npcPrefabToSpawn = npcPrefabs[Random.Range(0, npcPrefabs.Count)];
            GameObject customerObject = poolingManager.GetPooledObject(npcPrefabToSpawn);

            if (customerObject != null)
            {
                // Get the new NpcStateMachineRunner component
                Game.NPC.NpcStateMachineRunner customerRunner = customerObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
                if (customerRunner != null)
                {
                    Transform chosenSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
                    customerObject.transform.position = chosenSpawnPoint.position;
                    customerObject.transform.rotation = chosenSpawnPoint.rotation;

                    // --- Initialize the NpcStateMachineRunner instead of CustomerAI ---
                    customerRunner.Initialize(this, chosenSpawnPoint.position); // Initialize the Runner

                    // Add the Runner to our list of active customers
                    activeCustomers.Add(customerRunner);

                    Debug.Log($"CustomerManager: Spawned customer '{customerObject.name}' (Runner) from pool at {chosenSpawnPoint.position}. Total active: {activeCustomers.Count}");
                }
                else
                {
                    Debug.LogError($"CustomerManager: Pooled object '{customerObject.name}' does not have an NpcStateMachineRunner component!", customerObject);
                     poolingManager.ReturnPooledObject(customerObject);
                }
            }
             else
             {
                 Debug.LogWarning($"CustomerManager: Failed to get pooled object for prefab '{npcPrefabToSpawn.name}'. Pool might be exhausted and cannot grow.");
             }
        }
        
        // --- Event Handlers (replace old direct call methods) ---

        /// <summary>
        /// Handles the NpcReturningToPoolEvent. Returns a customer GameObject back to the object pool.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the NPC GameObject.</param>
        private void HandleNpcReturningToPool(NpcReturningToPoolEvent eventArgs)
        {
            GameObject customerObject = eventArgs.NpcObject;
            if (customerObject == null || poolingManager == null) return;

            Game.NPC.NpcStateMachineRunner customerRunner = customerObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (customerRunner != null)
            {
                 activeCustomers.Remove(customerRunner); // Remove the Runner from our list
                 Debug.Log($"CustomerManager: Handling NpcReturningToPoolEvent for '{customerObject.name}' (Runner). Total active: {activeCustomers.Count}");
                 poolingManager.ReturnPooledObject(customerObject);
            }
             else
             {
                  Debug.LogWarning($"CustomerManager: Received NpcReturningToPoolEvent for GameObject '{customerObject.name}' without NpcStateMachineRunner component. Attempting direct return.", customerObject);
                  if(customerObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(customerObject);
                  else Destroy(customerObject);
             }
        }

        /// <summary>
        /// Handles the QueueSpotFreedEvent. Signals that a customer is leaving a specific queue spot.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the queue type and spot index.</param>
        private void HandleQueueSpotFreed(QueueSpotFreedEvent eventArgs)
        {
            QueueType type = eventArgs.Type;
            int spotIndex = eventArgs.SpotIndex;

            Debug.Log($"CustomerManager: Handling QueueSpotFreedEvent for spot {spotIndex} in {type} queue.");
            bool[] occupiedArray = null;
            List<Transform> pointsList = null;
            Queue<Game.NPC.NpcStateMachineRunner> targetQueue = null; // Queue of Runners

            if (type == QueueType.Main)
            {
                occupiedArray = queueSpotOccupied;
                pointsList = queuePoints;
                targetQueue = customerQueue;
            }
            else if (type == QueueType.Secondary)
            {
                occupiedArray = secondaryQueueSpotOccupied;
                pointsList = secondaryQueuePoints;
                targetQueue = secondaryCustomerQueue;
            }
            else
            {
                Debug.LogError($"CustomerManager: Received QueueSpotFreedEvent for unknown QueueType: {type}!");
                return;
            }

            if (occupiedArray != null && pointsList != null && targetQueue != null && spotIndex >= 0 && spotIndex < occupiedArray.Length)
            {
                occupiedArray[spotIndex] = false;
                Debug.Log($"CustomerManager: Spot {spotIndex} in {type} queue is now free.");

                // --- Logic to potentially move the next customer (Runner) up ---
                int nextCustomerSpotIndex = spotIndex + 1;

                if (nextCustomerSpotIndex < pointsList.Count)
                {
                    Game.NPC.NpcStateMachineRunner runnerToMove = null; // Look for Runner
                    foreach (var runner in targetQueue) // Iterate Runners in queue
                    {
                        // Access AssignedQueueSpotIndex from the Runner
                        if (runner != null && runner.AssignedQueueSpotIndex == nextCustomerSpotIndex)
                        {
                            runnerToMove = runner; // Found the Runner who should move up
                            break;
                        }
                    }

                    if (runnerToMove != null)
                    {
                        Debug.Log($"CustomerManager: Signalling {runnerToMove.gameObject.name} (Runner) at spot {nextCustomerSpotIndex} to move up to spot {spotIndex} in {type} queue.");

                        // Call the new MoveToQueueSpot method on the Runner
                        runnerToMove.MoveToQueueSpot(pointsList[spotIndex], spotIndex);
                    }
                    else
                    {
                        Debug.Log($"CustomerManager: No customer (Runner) found waiting for spot {nextCustomerSpotIndex} or beyond in {type} queue.");
                    }
                }
                else
                {
                    Debug.Log($"CustomerManager: Freed spot {spotIndex} is the last in the {type} queue. No customer (Runner) behind needs to move up this specific spot.");
                }
            }
            else
            {
                Debug.LogWarning($"CustomerManager: Received invalid QueueSpotFreedEvent args (index {spotIndex}, type {type}) or null array/list/queue.");
            }
        }


        /// <summary>
        /// Handles the CashRegisterFreeEvent. Signals that the register is available.
        /// </summary>
        /// <param name="eventArgs">The event arguments (currently empty).</param>
        private void HandleCashRegisterFree(CashRegisterFreeEvent eventArgs)
        {
            Debug.Log("CustomerManager: Handling CashRegisterFreeEvent.");
            customerAtRegister = null; // The register is now free

            // If there are customers (Runners) in the main queue
            if (customerQueue.Count > 0)
            {
                Game.NPC.NpcStateMachineRunner nextRunner = customerQueue.Dequeue(); // Dequeue Runner
                Debug.Log($"CustomerManager: Dequeued {nextRunner.gameObject.name} (Runner) from main queue. Signalling them to move to register.");

                // Tell them to go to register - Call public method on the Runner
                nextRunner.GoToRegisterFromQueue();

                // The first main queue spot (index 0) is now free because the person going to the register left it.
                // Call the handler manually for spot 0.
                HandleQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Main, 0));
            }
            else
            {
                Debug.Log("CustomerManager: Main queue is empty.");
            }

            // Check and trigger Secondary Queue release
            if (customerQueue.Count < mainQueueReleaseThreshold)
            {
                ReleaseNextSecondaryCustomer(); // Call the method that publishes the event
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
        public bool TryJoinQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (queuePoints == null || queuePoints.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join queue - no queue points defined!"); return false; }
            if (queueSpotOccupied == null || queueSpotOccupied.Length != queuePoints.Count) { Debug.LogError("CustomerManager: queueSpotOccupied array is not initialized correctly!"); if(queuePoints != null) queueSpotOccupied = new bool[queuePoints.Count]; return false; }

            for (int i = 0; i < queueSpotOccupied.Length; i++)
            {
                if (!queueSpotOccupied[i])
                {
                    queueSpotOccupied[i] = true;
                    customerQueue.Enqueue(customerRunner); // Enqueue the Runner
                    assignedSpot = queuePoints[i];
                    spotIndex = i;
                    Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined queue at spot {i}. Queue size: {customerQueue.Count}");
                     // Store the assigned index on the Runner itself
                    customerRunner.AssignedQueueSpotIndex = spotIndex;
                    return true;
                }
            }

            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join queue - queue is full.");
            return false;
        }
        
        /// <summary>
        /// Releases the next customer from the secondary queue to enter the store
        /// by publishing an event that the customer's AI subscribes to.
        /// </summary>
        public void ReleaseNextSecondaryCustomer()
        {
            Debug.Log("CustomerManager: Attempting to release next secondary customer (Runner).");
            if (secondaryCustomerQueue.Count > 0)
            {
                Game.NPC.NpcStateMachineRunner nextRunner = secondaryCustomerQueue.Peek();

                 if (nextRunner != null)
                 {
                      Debug.Log($"CustomerManager: Peeking {nextRunner.gameObject.name} (Runner) from secondary queue. Publishing ReleaseNpcFromSecondaryQueueEvent.");
                      // Publish the event for the specific NPC GameObject that the Runner is on
                      EventManager.Publish(new ReleaseNpcFromSecondaryQueueEvent(nextRunner.gameObject));

                      // Dequeue the Runner from the queue collection
                      secondaryCustomerQueue.Dequeue();
                      Debug.Log($"CustomerManager: Dequeued {nextRunner.gameObject.name} (Runner) from secondary queue collection.");

                      // The SecondaryQueueStateSO OnExit will publish QueueSpotFreedEvent upon state transition.
                 }
                 else
                 {
                      Debug.LogError("CustomerManager: Null customer (Runner) found at the front of the secondary queue! Dequeuing to clear.");
                      secondaryCustomerQueue.Dequeue();
                      if(secondaryCustomerQueue.Count > 0) ReleaseNextSecondaryCustomer();
                 }

            }
            else
            {
                Debug.Log("CustomerManager: Secondary queue is empty, no one to release.");
            }
        }

        /// <summary>
        /// Signals that a customer is currently moving towards or is at the register.
        /// </summary>
        /// <param name="customer">The customer AI that is now occupying the register spot.</param>
        public void SignalCustomerAtRegister(Game.NPC.NpcStateMachineRunner customerRunner)
        {
            if (customerRunner == null) { Debug.LogWarning("CustomerManager: SignalCustomerAtRegister called with null customerRunner."); return; }
            if (customerAtRegister != null && customerAtRegister != customerRunner)
            {
                Debug.LogWarning($"CustomerManager: {customerRunner.gameObject.name} (Runner) is signalling at register, but {customerAtRegister.gameObject.name} (Runner) was already there!");
            }
            customerAtRegister = customerRunner;
            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) is now registered as being at the register.");
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
        public bool TryJoinSecondaryQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (secondaryQueuePoints == null || secondaryQueuePoints.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join secondary queue - no points defined!"); return false; }
            if (secondaryQueueSpotOccupied == null || secondaryQueueSpotOccupied.Length != secondaryQueuePoints.Count) { Debug.LogError("CustomerManager: secondaryQueueSpotOccupied array is not initialized correctly!"); if(secondaryQueuePoints != null) secondaryQueueSpotOccupied = new bool[secondaryQueuePoints.Count]; return false; }

            for (int i = 0; i < secondaryQueueSpotOccupied.Length; i++)
            {
                if (!secondaryQueueSpotOccupied[i])
                {
                    secondaryQueueSpotOccupied[i] = true;
                    secondaryCustomerQueue.Enqueue(customerRunner); // Enqueue the Runner
                    assignedSpot = secondaryQueuePoints[i];
                    spotIndex = i;
                    Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined secondary queue at spot {i}. Secondary queue size: {secondaryCustomerQueue.Count}");
                     // Store the assigned index on the Runner itself
                    customerRunner.AssignedQueueSpotIndex = spotIndex;
                    return true;
                }
            }

            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join secondary queue - secondary queue is full.");
            return false;
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