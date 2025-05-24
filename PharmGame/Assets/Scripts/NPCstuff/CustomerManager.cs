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
/// This method is called *only* by the OnExit of the QueueStateSO or SecondaryQueueStateSO.
/// It marks the specific spot free and cascades the move-up commands down the line.
/// </summary>
/// <param name="eventArgs">The event arguments containing the queue type and spot index.</param>
private void HandleQueueSpotFreed(QueueSpotFreedEvent eventArgs)
{
    QueueType type = eventArgs.Type;
    int spotIndex = eventArgs.SpotIndex;

    // Basic validation
    if (spotIndex < 0)
    {
         Debug.LogWarning($"CustomerManager: Received QueueSpotFreedEvent with invalid negative spot index {spotIndex}. Ignoring.", this);
         return;
    }

    Debug.Log($"CustomerManager: Handling QueueSpotFreedEvent for spot {spotIndex} in {type} queue.");

    bool[] occupiedArray = null;
    List<Transform> pointsList = null;
    CustomerState queueStateEnum;

    if (type == QueueType.Main)
    {
        occupiedArray = queueSpotOccupied;
        pointsList = queuePoints;
        queueStateEnum = CustomerState.Queue;
    }
    else if (type == QueueType.Secondary)
    {
        occupiedArray = secondaryQueueSpotOccupied;
        pointsList = secondaryQueuePoints;
        queueStateEnum = CustomerState.SecondaryQueue;
    }
    else
    {
         Debug.LogError($"CustomerManager: Received QueueSpotFreedEvent for unknown QueueType: {type}!");
         return;
    }

    // Further validation
     if (occupiedArray == null || pointsList == null || spotIndex < 0 || spotIndex >= occupiedArray.Length)
     {
          Debug.LogWarning($"CustomerManager: Received invalid QueueSpotFreedEvent args (index {spotIndex}, type {type}) or null array/list. Ignoring.", this);
          return;
     }


    // 1. Mark the spot that explicitly published the event as free in the occupied array.
    // This spot *should* have been occupied.
    if (occupiedArray[spotIndex])
    {
        occupiedArray[spotIndex] = false; // <-- Mark the spot as free
        Debug.Log($"CustomerManager: Spot {spotIndex} in {type} queue is now marked free (triggered by State Exit).");
    }
    else
    {
        // This might happen if the spot was force-freed due to inconsistency previously
         Debug.LogWarning($"CustomerManager: Received QueueSpotFreedEvent for spot {spotIndex} in {type} queue, but it was already marked as free. Inconsistency?", this);
    }


    // 2. Initiate the cascade of "move up" commands down the line.
    // Iterate through ALL subsequent spots starting from the one *after* the freed spot.
    // We check based on ASSIGNED INDEX, not necessarily if the spot is marked occupied in the array yet.
    for (int currentSpotIndex = spotIndex + 1; currentSpotIndex < pointsList.Count; currentSpotIndex++)
    {
         // Find the Runner currently assigned to this 'currentSpotIndex' and in the correct state.
         // We must search through all active customers in the correct queue state.
         Game.NPC.NpcStateMachineRunner runnerToMove = null;
         foreach (var activeRunner in activeCustomers)
         {
             if (activeRunner != null &&
                 activeRunner.CurrentStateSO != null &&
                 activeRunner.CurrentStateSO.HandledState.Equals(queueStateEnum) && // Must be in the correct queue state
                 activeRunner.AssignedQueueSpotIndex == currentSpotIndex) // Must be assigned to this spot index
             {
                 runnerToMove = activeRunner;
                 break; // Found the Runner
             }
         }

         // If we found a runner assigned to this spot...
         if (runnerToMove != null)
         {
             // Tell the runner to move to the *previous* spot (currentSpotIndex - 1)
             int nextSpotIndex = currentSpotIndex - 1;
             Debug.Log($"CustomerManager: Signalling {runnerToMove.gameObject.name} (Runner) assigned to spot {currentSpotIndex} to move up to spot {nextSpotIndex} in {type} queue.");

             // Call the method on the Runner to initiate the move.
             // The Runner will update its internal assigned index and start moving.
             // It will *also* set its _isMovingToQueueSpot flag.
             runnerToMove.MoveToQueueSpot(pointsList[nextSpotIndex], nextSpotIndex, type); // <-- Pass queueType

             // !!! The physical spot (currentSpotIndex) is marked free in occupiedArray
             //    WHEN THE RUNNER ARRIVES at the *new* spot (nextSpotIndex),
             //    via the FreePreviousQueueSpotOnArrival call from the Runner's Update.
             //    DO NOT mark occupiedArray[currentSpotIndex] false here.
             // occupiedArray[currentSpotIndex] = false; // <-- REMOVE THIS LINE ENTIRELY
         }
         else // No runner found assigned to this spot index in the correct state
         {
             // This means there's a true gap or inconsistency in the *assigned indices* of runners in the queue.
             // Log a warning, but continue the loop. The next spot might have someone.
             // Example: Spots 0, 1 (empty assignment), 2 (occupied assignment). When spot 0 freed,
             // loop reaches 1, finds no one assigned to 1, logs gap, continues to 2, finds NPC X assigned to 2, tells them to move to 1.
             Debug.LogWarning($"CustomerManager: No Runner found assigned to spot {currentSpotIndex} in {type} queue state. This spot might be empty or inconsistency in assignments. Continuing cascade search.", this);

             // Continue the loop. Do *not* break just because one spot is empty.
         }
    } // End of cascade loop
}
    
    /// <summary>
    /// Called by an NpcStateMachineRunner when it completes a MoveToQueueSpot command.
    /// This signifies that the Runner has arrived at its *new* spot, and its *previous* spot is now free.
    /// </summary>
    /// <param name="queueType">The type of queue the move occurred within.</param>
    /// <param name="previousSpotIndex">The index of the spot the runner *just left* (which is now physically free).</param>
    /// <returns>True if the spot was successfully marked free, false otherwise.</returns>
    public bool FreePreviousQueueSpotOnArrival(QueueType queueType, int previousSpotIndex)
    {
        Debug.Log($"CustomerManager: Handling FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in {queueType} queue.");

        bool[] occupiedArray = null;
        string queueName = "";

        if (queueType == QueueType.Main)
        {
            occupiedArray = queueSpotOccupied;
            queueName = "Main";
        }
        else if (queueType == QueueType.Secondary)
        {
            occupiedArray = secondaryQueueSpotOccupied;
            queueName = "Secondary";
        }
        else
        {
            Debug.LogError($"CustomerManager: Received FreePreviousQueueSpotOnArrival for unknown QueueType: {queueType}!", this);
            return false;
        }

         // Validate the previous spot index
        if (occupiedArray == null || previousSpotIndex < 0 || previousSpotIndex >= occupiedArray.Length)
        {
            Debug.LogWarning($"CustomerManager: Received FreePreviousQueueSpotOnArrival with invalid spot index {previousSpotIndex} for {queueName} queue. Ignoring.", this);
            return false;
        }

        // Mark the previous spot as free.
        if (occupiedArray[previousSpotIndex])
        {
             occupiedArray[previousSpotIndex] = false; // <-- Mark the spot as free on arrival
             Debug.Log($"CustomerManager: Spot {previousSpotIndex} in {queueName} queue is now marked free (triggered by Runner Arrival).");
             return true;
        }
        else
        {
             Debug.LogWarning($"CustomerManager: Received FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in {queueName} queue, but it was already marked as free. Inconsistency?", this);
             // Return true even if already free, as the intent was achieved.
             return true;
        }
    }


/// <summary>
        /// Handles the CashRegisterFreeEvent. Signals that the register is available.
        /// This method attempts to send the customer at Main Queue spot 0 to the register.
        /// </summary>
        /// <param name="eventArgs">The event arguments (currently empty).</param>
        private void HandleCashRegisterFree(CashRegisterFreeEvent eventArgs)
        {
            Debug.Log("CustomerManager: Handling CashRegisterFreeEvent.");
            customerAtRegister = null; // The register is now free (this is the Manager's tracking)

            // Find the customer currently at Main Queue spot 0.
            // We must search *all* active customers to find the one
            // that is currently in the Main Queue state AND assigned to index 0.
            Game.NPC.NpcStateMachineRunner runnerAtSpot0 = null;
            foreach (var activeRunner in activeCustomers) // <-- Iterate the list of *all* active runners
            {
                if (activeRunner != null &&
                    activeRunner.CurrentStateSO != null && // Ensure CurrentStateSO is not null
                    activeRunner.CurrentStateSO.HandledState.Equals(CustomerState.Queue) && // Must be in the Main Queue state
                    activeRunner.AssignedQueueSpotIndex == 0) // Must be assigned to spot 0
                {
                    runnerAtSpot0 = activeRunner;
                    break; // Found the one who should go to the register
                }
            }

            // If we found a customer at spot 0...
            if (runnerAtSpot0 != null)
            {
                Debug.Log($"CustomerManager: Found {runnerAtSpot0.gameObject.name} (Runner) assigned to Main Queue spot 0. Signalling them to move to register.");

                // Tell the Manager that THIS runner is now claiming the register spot
                SignalCustomerAtRegister(runnerAtSpot0); // <-- Update Manager's 'customerAtRegister' tracking

                // Tell the runner to transition to the state for moving to the register.
                // This transitions the runner out of CustomerState.Queue.
                // The CustomerState.Queue.OnExit method will *then* publish QueueSpotFreedEvent(Main, 0).
                // HandleQueueSpotFreed(Main, 0) will *then* receive this event and trigger the cascade
                // to move everyone up and free their old spots.
                runnerAtSpot0.GoToRegisterFromQueue(); // <-- Tell the runner to move

                // --- REMOVE THE MANUAL CALL TO HandleQueueSpotFreed(Main, 0) ---
                // This call was causing the double freeing and cascade issue.
                // The spot freeing and cascade is now handled solely by the Runner's state exit event.
                // HandleQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Main, 0)); // <-- REMOVE THIS LINE
                // ---------------------------------------------------------------

            }
            else
            {
                // This might happen if the last customer in spot 0 became impatient and left,
                // or if there's a logic error leaving spot 0 occupied but no runner there.
                // Check the occupied array state for spot 0 to see if it *thinks* it's occupied.
                Debug.LogWarning("CustomerManager: CashRegisterFreeEvent received, but no customer found assigned to Main Queue spot 0.", this);
                if (queueSpotOccupied.Length > 0 && queueSpotOccupied[0])
                {
                    // This indicates an inconsistency. Spot 0 is marked occupied, but no runner is found there.
                    // We must force free spot 0 to prevent the queue from being permanently blocked.
                    Debug.LogError("CustomerManager: Inconsistency detected! Main Queue spot 0 marked occupied but no Runner found assigned to it and in state CustomerState.Queue. Forcing spot 0 free.", this);
                    // Force free the spot 0. This will NOT trigger the cascade automatically,
                    // but at least it allows a new customer to potentially join spot 0 later.
                    // We could potentially trigger HandleQueueSpotFreed(Main, 0) here to force a cascade,
                    // but that feels like trying to fix a symptom of a deeper inconsistency.
                    // Forcing the spot free and logging the error is safer.
                    queueSpotOccupied[0] = false; // <-- Force free spot 0
                    Debug.Log($"CustomerManager: Main Queue spot 0 ({queuePoints[0].position}) was manually forced free due to inconsistency.", this);
                }
                // Else: Spot 0 was already correctly free. Nothing to do.
            }

            // Check and trigger Secondary Queue release.
            // This check relies on the *count* of customers currently in the main queue state.
            // GetMainQueueCount() needs to iterate the activeCustomers list now.
            if (GetMainQueueCount() < mainQueueReleaseThreshold)
            {
                ReleaseNextSecondaryCustomer();
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
    /// Attempts to add a customer to the main queue.
    /// Finds the first available spot based on the queueSpotOccupied array.
    /// A spot is available if it's marked as unoccupied AND it's not the current
    /// assigned index of any other NPC currently moving up the queue.
    /// </summary>
    /// <param name="customerRunner">The customer Runner trying to join.</param>
    /// <param name="assignedSpot">Output: The Transform of the assigned queue spot, or null.</param>
    /// <param name="spotIndex">Output: The index of the assigned queue spot, or -1.</param>
    /// <returns>True if successfully joined the queue, false otherwise (e.g., queue is full).</returns>
    public bool TryJoinQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
    {
        assignedSpot = null;
        spotIndex = -1;

        if (queuePoints == null || queuePoints.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join main queue - no queue points defined!"); return false; }
        if (queueSpotOccupied == null || queueSpotOccupied.Length != queuePoints.Count)
        {
             Debug.LogError("CustomerManager: queueSpotOccupied array is not initialized correctly! Re-initializing.", this);
             if(queuePoints != null) queueSpotOccupied = new bool[queuePoints.Count];
             else return false;
        }

        // Iterate through the spots to find the first available one
        for (int i = 0; i < queueSpotOccupied.Length; i++)
        {
            // A spot is available if:
            // 1. The occupied array marks it as NOT occupied.
            // 2. AND it is NOT the *assigned* index of any other active runner
            //    that is currently in the Queue state.
            //    This second check is to prevent a new customer from claiming spot 0
            //    if someone is already moving to spot 0 from spot 1.

            // Find if any *other* active runner is currently assigned this spot index AND is in the main queue state
            bool isSpotAssignedToMovingRunner = false;
            foreach(var activeRunner in activeCustomers)
            {
                 if (activeRunner != null &&
                     activeRunner != customerRunner && // Not the runner trying to join
                     activeRunner.CurrentStateSO != null &&
                     activeRunner.CurrentStateSO.HandledState.Equals(CustomerState.Queue) && // Is in the main queue state
                     activeRunner.AssignedQueueSpotIndex == i) // Is assigned this spot index
                 {
                      // Found a runner who is currently assigned to this spot.
                      // This spot is *not* available for a *new* customer joining the end.
                      isSpotAssignedToMovingRunner = true;
                      break;
                 }
            }

            // Check if the spot is available based on the array AND not assigned to a moving runner
            if (!queueSpotOccupied[i] && !isSpotAssignedToMovingRunner)
            {
                queueSpotOccupied[i] = true; // Mark the spot as occupied by this new joiner
                assignedSpot = queuePoints[i];
                spotIndex = i;
                Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined main queue at spot {i}.");
                customerRunner.AssignedQueueSpotIndex = spotIndex; // Store the assigned index on the Runner
                return true; // Successfully joined a spot
            }
        }

        Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join main queue - queue is full or spots are temporarily claimed.");
        return false; // No free spot found
    }
        
/// <summary>
/// Releases the next customer from the secondary queue to enter the store
/// by publishing an event that the customer's AI subscribes to.
/// Finds the customer at the first occupied spot in the secondary queue.
/// </summary>
public void ReleaseNextSecondaryCustomer()
{
    Debug.Log("CustomerManager: Attempting to release next secondary customer (Runner).");

    // Find the customer currently at the first occupied Secondary Queue spot (lowest index)
    Game.NPC.NpcStateMachineRunner runnerToRelease = null;
    int firstOccupiedIndex = -1;

    // Find the index of the first occupied spot in the secondary queue array
    for(int i = 0; i < secondaryQueueSpotOccupied.Length; i++)
    {
        if (secondaryQueueSpotOccupied[i])
        {
            firstOccupiedIndex = i; // Found the index
            break; // Stop searching for the index
        }
    }

    // If we found an occupied spot index...
    if (firstOccupiedIndex != -1)
    {
        // Now, find the Runner currently assigned to this 'firstOccupiedIndex'.
        // Search *all* active customers to find the one
        // that is currently in the Secondary Queue state AND assigned to this index.
        foreach(var activeRunner in activeCustomers) // <-- Iterate the list of *all* active runners
        {
             if (activeRunner != null &&
                 activeRunner.CurrentStateSO != null && // Ensure CurrentStateSO is not null
                 activeRunner.CurrentStateSO.HandledState.Equals(CustomerState.SecondaryQueue) && // Must be in Secondary Queue state
                 activeRunner.AssignedQueueSpotIndex == firstOccupiedIndex) // Must be assigned to this spot index
             {
                 runnerToRelease = activeRunner;
                 break; // Found the one to release, exit inner loop
             }
        }

        // Check if we successfully found the runner for the first occupied spot
        if (runnerToRelease != null)
        {
             Debug.Log($"CustomerManager: Found {runnerToRelease.gameObject.name} (Runner) assigned to Secondary Queue spot {firstOccupiedIndex}. Publishing ReleaseNpcFromSecondaryQueueEvent.");

             // Publish the event for the specific NPC GameObject that the Runner is on.
             // The Runner will receive this event and transition out of SecondaryQueue state.
             // Its SecondaryQueueStateSO.OnExit method will *then* publish
             // QueueSpotFreedEvent(Secondary, firstOccupiedIndex).
             // HandleQueueSpotFreed will *then* receive this event and trigger the cascade
             // for the secondary queue.
             EventManager.Publish(new ReleaseNpcFromSecondaryQueueEvent(runnerToRelease.gameObject));

             // --- REMOVE MANUAL DEQUEUE/SPOT FREEING ---
             // The spot freeing happens when the Runner exits the state and publishes the event.
             // secondaryCustomerQueue.Dequeue(); // <-- REMOVE THIS LINE
             // Debug.Log($"CustomerManager: Dequeued ... from secondary queue collection."); // Remove or update log
             // The SecondaryQueueStateSO OnExit will publish QueueSpotFreedEvent upon state transition.
             // -------------------------------------------
        }
        else // Found an occupied spot index (firstOccupiedIndex != -1) but no runner was assigned to it
        {
            // This indicates an inconsistency. The spot is marked occupied, but no runner is found there.
            // We must force free this spot to prevent the secondary queue from being permanently blocked.
            Debug.LogError($"CustomerManager: Inconsistency detected! Secondary Queue spot {firstOccupiedIndex} marked occupied but no Runner found assigned to it and in state CustomerState.SecondaryQueue. Forcing spot {firstOccupiedIndex} free.", this);
            secondaryQueueSpotOccupied[firstOccupiedIndex] = false; // <-- Force free the spot
             Debug.Log($"CustomerManager: Secondary Queue spot {firstOccupiedIndex} ({secondaryQueuePoints[firstOccupiedIndex].position}) was manually forced free due to inconsistency.", this);

             // As this spot is now free, recursively check if there's another customer
             // at the *next* spot who is now effectively at the front of the remaining secondary queue.
             // This prevents a single inconsistency from blocking the entire secondary queue.
             Debug.Log("CustomerManager: Recursively checking for the next secondary customer to release after handling inconsistency.");
             ReleaseNextSecondaryCustomer(); // Recursive call
        }
    }
    else
    {
        // No occupied spot index was found in the first loop. The secondary queue is empty.
        Debug.Log("CustomerManager: Secondary queue appears empty (no spots marked occupied and no Runner found).");
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
    /// Finds the first available spot based on the secondaryQueueSpotOccupied array.
    /// Similar logic to main queue joining.
    /// </summary>
    /// <param name="customerRunner">The customer Runner trying to join.</param>
    /// <param name="assignedSpot">Output: The Transform of the assigned secondary queue spot, or null.</param>
    /// <param name="spotIndex">Output: The index of the assigned secondary queue spot, or -1.</param>
    /// <returns>True if successfully joined the secondary queue, false otherwise (e.g., queue is full).</returns>
    public bool TryJoinSecondaryQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
    {
        assignedSpot = null;
        spotIndex = -1;

        if (secondaryQueuePoints == null || secondaryQueuePoints.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join secondary queue - no points defined!"); return false; }
        if (secondaryQueueSpotOccupied == null || secondaryQueueSpotOccupied.Length != secondaryQueuePoints.Count)
        {
             Debug.LogError("CustomerManager: secondaryQueueSpotOccupied array is not initialized correctly! Re-initializing.", this);
             if (secondaryQueuePoints != null) secondaryQueueSpotOccupied = new bool[secondaryQueuePoints.Count];
             else return false;
        }

        for (int i = 0; i < secondaryQueueSpotOccupied.Length; i++)
        {
             // Find if any *other* active runner is currently assigned this spot index AND is in the secondary queue state
             bool isSpotAssignedToMovingRunner = false;
             foreach (var activeRunner in activeCustomers)
             {
                  if (activeRunner != null &&
                      activeRunner != customerRunner && // Not the runner trying to join
                      activeRunner.CurrentStateSO != null &&
                      activeRunner.CurrentStateSO.HandledState.Equals(CustomerState.SecondaryQueue) && // Is in the secondary queue state
                      activeRunner.AssignedQueueSpotIndex == i) // Is assigned this spot index
                  {
                       isSpotAssignedToMovingRunner = true;
                       break;
                  }
             }

             if (!secondaryQueueSpotOccupied[i] && !isSpotAssignedToMovingRunner)
             {
                 secondaryQueueSpotOccupied[i] = true;
                 assignedSpot = secondaryQueuePoints[i];
                 spotIndex = i;
                 Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined secondary queue at spot {i}.");
                 customerRunner.AssignedQueueSpotIndex = spotIndex;
                 return true;
             }
        }

        Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join secondary queue - secondary queue is full or spots are temporarily claimed.");
        return false;
    }

    // GetMainQueueCount needs to filter by state now, not rely on collection count
     public int GetMainQueueCount()
     {
         int count = 0;
         foreach(var activeRunner in activeCustomers)
         {
              if (activeRunner != null && activeRunner.CurrentStateSO != null && activeRunner.CurrentStateSO.HandledState.Equals(CustomerState.Queue))
              {
                   count++;
              }
         }
         return count;
     }

        // Add getter for checking if main queue is full
        public bool IsMainQueueFull()
     {
          if (queuePoints == null || queuePoints.Count == 0) return false; // Cannot be full if no spots

          // The queue is full if TryJoinQueue fails because no spots are available.
          // We can simulate the TryJoinQueue logic *without* actually assigning the spot.
          // Or, more simply, check if the *last* spot is both marked occupied *and*
          // potentially assigned to an NPC (including the one who just arrived there).

          // Let's use the TryJoinQueue logic internally to check for availability.
          // Find if there is *any* spot that TryJoinQueue would be able to assign.
          for (int i = 0; i < queueSpotOccupied.Length; i++)
          {
               bool isSpotAssignedToMovingRunner = false;
               // Check if any active Runner (excluding a potential 'self' if this were called from Runner)
               // is in the Queue state and assigned this spot index.
               // For IsMainQueueFull, we check against *all* active customers.
               foreach (var activeRunner in activeCustomers)
               {
                    if (activeRunner != null &&
                        activeRunner.CurrentStateSO != null &&
                        activeRunner.CurrentStateSO.HandledState.Equals(CustomerState.Queue) &&
                        activeRunner.AssignedQueueSpotIndex == i)
                    {
                         isSpotAssignedToMovingRunner = true;
                         break;
                    }
               }

               // If we find *any* spot that is not occupied AND not assigned to a current queueing runner,
               // then the queue is NOT full (there's at least one spot a new customer could potentially join).
               if (!queueSpotOccupied[i] && !isSpotAssignedToMovingRunner)
               {
                   return false; // Found an available spot, queue is NOT full
               }
          }

          // If the loop finished without finding any available spot, the queue is full.
          Debug.Log($"CustomerManager: IsMainQueueFull = true (all {queueSpotOccupied.Length} spots are occupied or temporarily claimed).");
          return true;
     }
    }
}