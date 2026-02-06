// --- START OF FILE CustomerManager.cs ---
using UnityEngine;
using System.Collections.Generic;
using Utils.Pooling; // Required for PoolingManager
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState enum
using System.Collections; // Required for Coroutines
using Systems.Inventory; // Required for Inventory reference
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using Game.Events;
using Game.NPC.States;
using Game.NPC.TI; // Needed for TiNpcManager (to pass the TI NPC to it), TiNpcData

namespace CustomerManagement
{
    // NEW ENUM to identify the source of a pause request
    public enum StorePauseSource
    {
        Proximity,
        CashierSimulation
    }

    /// <summary>
    /// Manages the spawning, pooling, and overall flow of customer NPCs in the store.
    /// Now also collaborates with TiNpcManager for pooling TI NPCs.
    /// MODIFIED: Upgraded pause logic to handle multiple sources (Proximity, Cashier Simulation).
    /// </summary>
    public class CustomerManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static CustomerManager Instance { get; private set; }

        [Header("NPC Setup")]
        [Tooltip("List of NPC prefabs that this manager can spawn.")]
        [SerializeField] private List<GameObject> npcPrefabs;
        [Tooltip("Maximum number of customers allowed in the store at any given time.")]
        [SerializeField] private int maxCustomersInStore = 5; // This limit now applies to activeCustomers.Count + tiNpcsInsideStore.Count
        [Tooltip("Minimum time between customer spawns.")]
        [SerializeField] private float minSpawnInterval = 5f;
        [Tooltip("Maximum time between customer spawns.")]
        [SerializeField] private float maxSpawnInterval = 15f;

        // --- Bus Spawning Configuration ---
        [Header("Bus Spawning")]
        // Changed from private field to private backing field for public property
        [Tooltip("The time interval between bus arrivals.")]
        [SerializeField] private float _busArrivalInterval = 75f;

        // Public property to access and modify the bus arrival interval
        public float BusArrivalInterval
        {
            get { return _busArrivalInterval; }
            set
            {
                _busArrivalInterval = value;
                Debug.Log($"CustomerManager: Bus Arrival Interval updated to {_busArrivalInterval}s.");
            }
        }
        [Tooltip("The number of transient NPCs that attempt to spawn when a bus arrives.")]
        [SerializeField] private int npcsPerBus = 3; // Example: 3 NPCs per bus
        [Tooltip("Optional: Delay before the very first bus arrives.")]
        [SerializeField] private float initialBusDelay = 10f; // Example: First bus arrives after 10 seconds
        [Tooltip("The delay between spawning each NPC during a bus burst.")] // NEW
        [SerializeField] private float delayBetweenBusSpawns = 0.5f; // Example: 0.5 seconds between each NPC in a burst // NEW
        [Tooltip("Points specifically where bus-spawned customers will enter the store.")] // NEW
        [SerializeField] private List<Transform> busSpawnPoints; // NEW
        // --- END NEW ---


        [Header("Navigation Points")]
        [Tooltip("Points where customers will enter the store.")]
        [SerializeField] private List<Transform> spawnPoints;

        [Tooltip("List of Browse locations, pairing a point with its associated inventory.")]
        [SerializeField] private List<BrowseLocation> BrowseLocations;

        [Tooltip("Point where customers will wait at the cash register.")]
        [SerializeField] private Transform registerPoint;

        [Tooltip("Points where customers will form a queue for the cash register, ordered from closest to furthest.")]
        [SerializeField] private List<Transform> queuePoints;

        [Tooltip("Points where customers will exit the store.")]
        [SerializeField] private List<Transform> exitPoints;


        // --- Internal State ---
        private PoolingManager poolingManager;
        // The activeCustomers list will now represent *Transient* customers inside the store.
        private List<Game.NPC.NpcStateMachineRunner> activeCustomers = new List<Game.NPC.NpcStateMachineRunner>(); // Track Transient customers *inside the store*

        // --- Track TI NPCs inside the store by their persistent data ---
        private HashSet<TiNpcData> tiNpcsInsideStore; // Track TI customers *inside the store* by data

        private List<QueueSpot> mainQueueSpots;

        // --- NEW: Reference to the CashRegisterInteractable ---
        private CashRegisterInteractable cashRegister;
        // --- END NEW ---

        // --- MODIFIED: Store Simulation Active Flag ---
        /// <summary>
        /// Indicates if active NPC spawning should be paused.
        /// Returns true if any system (e.g., Proximity, CashierSimulation) has requested a pause.
        /// </summary>
        public bool IsStoreSimulationActive => pauseRequesters.Count > 0;
        private HashSet<StorePauseSource> pauseRequesters; // Tracks all systems that have requested a pause.
        // --- END MODIFIED ---


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("CustomerManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // --- Initialize the new collections ---
            pauseRequesters = new HashSet<StorePauseSource>();
            tiNpcsInsideStore = new HashSet<TiNpcData>();
            // --- END ---

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
            if (spawnPoints == null || spawnPoints.Count == 0) Debug.LogWarning("CustomerManager: No general spawn points assigned! Trickle spawning may not work."); // Updated log
            if (busSpawnPoints == null || busSpawnPoints.Count == 0) Debug.LogWarning("CustomerManager: No bus spawn points assigned! Bus spawning may not work."); // NEW log
            if (BrowseLocations == null || BrowseLocations.Count == 0) Debug.LogError("CustomerManager: No Browse locations assigned!");
            else
            {
                foreach (var location in BrowseLocations)
                {
                    if (location.browsePoint == null) Debug.LogWarning("CustomerManager: A Browse location has a null browse point!");
                    if (location.inventory == null) Debug.LogWarning($"CustomerManager: Browse location '{location.browsePoint?.name}' has a null inventory reference!");
                }
            }
            if (registerPoint == null) Debug.LogWarning("CustomerManager: Register point not assigned!");
            if (exitPoints == null || exitPoints.Count == 0) Debug.LogWarning("CustomerManager: No exit points assigned!");

            // Initialize QueueSpot lists from Transform lists
            mainQueueSpots = new List<QueueSpot>();
            if (queuePoints == null || queuePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No main queue points assigned! Main queue system will not function.", this);
            }
            else
            {
                for (int i = 0; i < queuePoints.Count; i++)
                {
                    if (queuePoints[i] != null)
                    {
                        mainQueueSpots.Add(new QueueSpot(queuePoints[i], i, QueueType.Main));
                    }
                    else
                    {
                        Debug.LogWarning($"CustomerManager: Main queue point at index {i} is null!", this);
                    }
                }
                Debug.Log($"CustomerManager: Initialized main queue with {mainQueueSpots.Count} spots.");
            }

            Debug.Log("CustomerManager: Awake completed.");
        }

        private void Start()
        {
            // --- Find the CashRegisterInteractable ---
            GameObject registerGO = GameObject.FindGameObjectWithTag("CashRegister"); // Assumes your register has this tag
            if (registerGO != null)
            {
                cashRegister = registerGO.GetComponent<CashRegisterInteractable>();
                if (cashRegister == null)
                {
                    Debug.LogError($"CustomerManager ({gameObject.name}): Found GameObject with tag 'CashRegister' but it's missing the CashRegisterInteractable component! Register logic will not function.", this);
                }
            }
            else
            {
                Debug.LogError($"CustomerManager ({gameObject.name}): Could not find GameObject with tag 'CashRegister'! Register logic will not function.", this);
            }

            // Begin spawning customers (both trickle and bus)
            StartCoroutine(SpawnCustomerCoroutine()); // Existing trickle spawn
            StartCoroutine(BusArrivalCoroutine()); // NEW bus spawn
        }

        private void OnEnable() // Subscribe to events when the GameObject is enabled
        {
            // Subscribe to events published by NPCs or other systems
            EventManager.Subscribe<NpcReturningToPoolEvent>(HandleNpcReturningToPool);
            EventManager.Subscribe<QueueSpotFreedEvent>(HandleQueueSpotFreed);
            EventManager.Subscribe<CashRegisterFreeEvent>(HandleCashRegisterFree);

            // Subscribe to events for managing activeCustomers count
            EventManager.Subscribe<NpcEnteredStoreEvent>(HandleNpcEnteredStore);
            EventManager.Subscribe<NpcExitedStoreEvent>(HandleNpcExitedStore);

            Debug.Log("CustomerManager: Subscribed to events.");
        }

        private void OnDisable() // Unsubscribe from events when the GameObject is disabled
        {
            // Unsubscribe from events to prevent memory leaks and calls on null objects
            EventManager.Unsubscribe<NpcReturningToPoolEvent>(HandleNpcReturningToPool);
            EventManager.Unsubscribe<QueueSpotFreedEvent>(HandleQueueSpotFreed);
            EventManager.Unsubscribe<CashRegisterFreeEvent>(HandleCashRegisterFree);

            // Unsubscribe from events for managing activeCustomers count
            EventManager.Unsubscribe<NpcEnteredStoreEvent>(HandleNpcEnteredStore);
            EventManager.Unsubscribe<NpcExitedStoreEvent>(HandleNpcExitedStore);

            Debug.Log("CustomerManager: Unsubscribed from events.");
            StopAllCoroutines(); // Stop spawning and check coroutines
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // Clean up any remaining active customers if necessary
                StopAllCoroutines(); // Stop spawning and check coroutines
                // Clear the new collection as well
                tiNpcsInsideStore?.Clear();
            }
            Debug.Log("CustomerManager: OnDestroy completed.");
        }

        /// <summary>
        /// Checks if the store is at maximum capacity.
        /// Replaces the old logic involving the secondary queue availability.
        /// </summary>
        public bool IsStoreFull()
        {
            int currentCustomers = activeCustomers.Count + (tiNpcsInsideStore?.Count ?? 0);
            return currentCustomers >= maxCustomersInStore;
        }

        /// <summary>
        /// Spawns a new customer from the pool if conditions allow.
        /// </summary>
        /// <param name="isBusSpawn">True if this spawn is part of a bus burst, false for trickle spawn.</param> // NEW PARAM
        public void SpawnCustomer(bool isBusSpawn) // MODIFIED SIGNATURE
        {
            // --- Check if store spawning is paused ---
            if (IsStoreSimulationActive)
            {
                Debug.Log("CustomerManager: SpawnCustomer skipped. Store activity is paused, preventing active NPC spawning.", this);
                return; // Do not attempt to spawn if store simulation is active
            }

            // Determine which spawn points to use
            List<Transform> currentSpawnPoints = isBusSpawn ? busSpawnPoints : spawnPoints;

            if (poolingManager == null || npcPrefabs == null || npcPrefabs.Count == 0 || currentSpawnPoints == null || currentSpawnPoints.Count == 0)
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
                    // --- Ensure this is NOT treated as a TI NPC ---
                    if (customerRunner.IsTrueIdentityNpc)
                    {
                        Debug.LogError($"CustomerManager: Attempted to spawn a pooled object ({customerObject.name}) that is still flagged as a TI NPC! This indicates a pooling or deactivation issue. Returning it immediately.", customerObject);
                        // Attempt to return it to the pool directly, don't try to initialize it as transient
                        if (customerObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(customerObject);
                        else Destroy(customerObject); // Fallback
                        return; // Abort spawn process for this object
                    }

                    Transform chosenSpawnPoint = currentSpawnPoints[Random.Range(0, currentSpawnPoints.Count)]; // Use the determined spawn points list
                    // Warp the NPC to the spawn point - this is done by the Runner's Initialize
                    customerObject.transform.position = chosenSpawnPoint.position;
                    customerObject.transform.rotation = chosenSpawnPoint.rotation;

                    // Initialize the NpcStateMachineRunner, passing the manager and spawn position
                    // This path is for TRANSIENT customers.
                    customerRunner.Initialize(this, chosenSpawnPoint.position);


                    // This log confirms a successful spawn *from the pool* and initialization.
                    // The calling coroutine logs will indicate if it was a trickle or bus spawn attempt.
                    Debug.Log($"CustomerManager: Initialized transient customer '{customerObject.name}' (Runner) from pool at {chosenSpawnPoint.position}.");
                }
                else
                {
                    Debug.LogError($"CustomerManager: Pooled object '{customerObject.name}' does not have an NpcStateMachineRunner component! Returning to pool.", customerObject);
                    poolingManager.ReturnPooledObject(customerObject); // Return if not a valid NPC object
                }
            }
            else
            {
                Debug.LogWarning($"CustomerManager: Failed to get pooled object for prefab '{npcPrefabToSpawn.name}'. Pool might be exhausted and cannot grow.");
            }
        }

        /// <summary>
        /// Manually registers an NPC as being "inside" the store without triggering entry events.
        /// vital for Save/Load systems where an NPC loads directly into a Browse/Queue state,
        /// bypassing the Entering state that normally handles registration.
        /// </summary>
        /// <param name="runner">The NPC runner to register.</param>
        public void RegisterLoadedCustomer(Game.NPC.NpcStateMachineRunner runner)
        {
            if (runner == null) return;

            // 1. Handle True Identity NPCs
            if (runner.IsTrueIdentityNpc)
            {
                if (runner.TiData != null)
                {
                    if (tiNpcsInsideStore == null) tiNpcsInsideStore = new HashSet<TiNpcData>();

                    if (!tiNpcsInsideStore.Contains(runner.TiData))
                    {
                        tiNpcsInsideStore.Add(runner.TiData);
                        Debug.Log($"CustomerManager: Manually registered loaded TI NPC '{runner.TiData.Id}' as inside store. Total active: {activeCustomers.Count + tiNpcsInsideStore.Count}");
                    }
                }
            }
            // 2. Handle Transient NPCs
            else
            {
                if (!activeCustomers.Contains(runner))
                {
                    activeCustomers.Add(runner);
                    Debug.Log($"CustomerManager: Manually registered loaded Transient NPC '{runner.gameObject.name}' as inside store. Total active: {activeCustomers.Count + tiNpcsInsideStore.Count}");
                }
            }
        }

        // --- Event Handlers ---

        /// <summary>
        /// Handles the NpcReturningToPoolEvent. Returns a customer GameObject back to the object pool.
        /// Differentiates between transient and TI NPCs.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the NPC GameObject.</param>
        private void HandleNpcReturningToPool(NpcReturningToPoolEvent eventArgs)
        {
            GameObject npcObject = eventArgs.NpcObject;
            if (npcObject == null) return;

            Game.NPC.NpcStateMachineRunner runner = npcObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (runner == null)
            {
                if (activeCustomers.Contains(runner)) activeCustomers.Remove(runner);
                if (npcObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(npcObject);
                else Destroy(npcObject); 
                return; 
            }

            if (runner.IsTrueIdentityNpc)
            {
                TiNpcManager tiManager = TiNpcManager.Instance;
                if (tiManager != null) tiManager.HandleTiNpcReturnToPool(npcObject);
                else Destroy(npcObject);

                if (activeCustomers.Contains(runner)) activeCustomers.Remove(runner);
                
                // Main Queue Cleanup
                if (runner.QueueHandler != null && runner.QueueHandler.AssignedQueueSpotIndex != -1)
                {
                    CleanupQueueOnPooling(runner, QueueType.Main);
                }
                return; 
            }

            // Transient Logic
            if (activeCustomers.Contains(runner)) activeCustomers.Remove(runner);

            if (runner.QueueHandler.AssignedQueueSpotIndex != -1)
            {
                CleanupQueueOnPooling(runner, QueueType.Main);
            }
            
            if (poolingManager != null) poolingManager.ReturnPooledObject(npcObject);
            else Destroy(npcObject);
        }

        private void CleanupQueueOnPooling(Game.NPC.NpcStateMachineRunner runner, QueueType type)
        {
             // Secondary support removed, defaults to Main or logic ignored
             if (type == QueueType.Main && mainQueueSpots != null)
             {
                 if(runner.QueueHandler.AssignedQueueSpotIndex < mainQueueSpots.Count)
                 {
                    QueueSpot spot = mainQueueSpots[runner.QueueHandler.AssignedQueueSpotIndex];
                    if (spot.currentOccupant == runner) spot.currentOccupant = null;
                 }
             }
             runner.QueueHandler.AssignedQueueSpotIndex = -1;
        }


        /// <summary>
        /// Handles the NpcEnteredStoreEvent. Adds the NPC to the list of customers currently inside the store.
        /// This event is published by the NpcStateMachineRunner when transitioning to the Entering state.
        /// Applies to both Transient and TI NPCs temporarily acting as customers.
        /// </summary>
        private void HandleNpcEnteredStore(NpcEnteredStoreEvent eventArgs)
        {
            Game.NPC.NpcStateMachineRunner customerRunner = eventArgs.NpcObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (customerRunner == null)
            {
                Debug.LogWarning($"CustomerManager: Received NpcEnteredStoreEvent for GameObject '{eventArgs.NpcObject.name}' without an NpcStateMachineRunner component.", eventArgs.NpcObject);
                return;
            }

            // --- Differentiate tracking based on NPC type ---
            if (customerRunner.IsTrueIdentityNpc)
            {
                // Ensure TiData is available for TI NPCs
                if (customerRunner.TiData == null)
                {
                    Debug.LogError($"CustomerManager: Received NpcEnteredStoreEvent for TI NPC '{customerRunner.gameObject.name}' but TiData is null! Cannot track.", eventArgs.NpcObject);
                    return;
                }

                if (tiNpcsInsideStore != null && !tiNpcsInsideStore.Contains(customerRunner.TiData))
                {
                    tiNpcsInsideStore.Add(customerRunner.TiData);
                    Debug.Log($"CustomerManager: TI NPC '{customerRunner.TiData.Id}' ({customerRunner.gameObject.name}) entered the store (received NpcEnteredStoreEvent). Total active (inside store): {activeCustomers.Count + tiNpcsInsideStore.Count}");
                }
                else if (tiNpcsInsideStore == null)
                {
                    Debug.LogError($"CustomerManager: tiNpcsInsideStore collection is null! Cannot track TI NPC '{customerRunner.TiData.Id}'.", this);
                }
                else // Contains(customerRunner.TiData) was true
                {
                    Debug.LogWarning($"CustomerManager: Received NpcEnteredStoreEvent for TI NPC '{customerRunner.TiData.Id}' ({customerRunner.gameObject.name}) but it was already in the tiNpcsInsideStore list. Duplicate event?", eventArgs.NpcObject);
                }
            }
            else // Transient NPC
            {
                if (!activeCustomers.Contains(customerRunner))
                {
                    activeCustomers.Add(customerRunner);
                    Debug.Log($"CustomerManager: Transient NPC ({customerRunner.gameObject.name}) entered the store (received NpcEnteredStoreEvent). Total active (inside store): {activeCustomers.Count + tiNpcsInsideStore.Count}");
                }
                else // Contains(customerRunner) was true
                {
                    Debug.LogWarning($"CustomerManager: Received NpcEnteredStoreEvent for Transient NPC '{customerRunner.gameObject.name}' but it was already in the activeCustomers list. Duplicate event?", eventArgs.NpcObject);
                }
            }
        }

        /// <summary>
        /// Handles the NpcExitedStoreEvent. Removes the NPC from the list of customers currently inside the store.
        /// This event is published by the NpcStateMachineRunner when transitioning to the Exiting state.
        /// Applies to both Transient and TI NPCs finishing their customer loop.
        /// </summary>
        private void HandleNpcExitedStore(NpcExitedStoreEvent eventArgs)
        {
            Game.NPC.NpcStateMachineRunner customerRunner = eventArgs.NpcObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (customerRunner == null)
            {
                Debug.LogWarning($"CustomerManager: Received NpcExitedStoreEvent for GameObject '{eventArgs.NpcObject.name}' without an NpcStateMachineRunner component.", eventArgs.NpcObject);
                return;
            }

            // --- Differentiate tracking based on NPC type ---
            if (customerRunner.IsTrueIdentityNpc)
            {
                // Ensure TiData is available for TI NPCs
                if (customerRunner.TiData == null)
                {
                    Debug.LogError($"CustomerManager: Received NpcExitedStoreEvent for TI NPC '{customerRunner.gameObject.name}' but TiData is null! Cannot track.", eventArgs.NpcObject);
                    return;
                }

                if (tiNpcsInsideStore != null && tiNpcsInsideStore.Contains(customerRunner.TiData))
                {
                    tiNpcsInsideStore.Remove(customerRunner.TiData);
                    Debug.Log($"CustomerManager: TI NPC '{customerRunner.TiData.Id}' ({customerRunner.gameObject.name}) exited the store (received NpcExitedStoreEvent). Total active (inside store): {activeCustomers.Count + tiNpcsInsideStore.Count}");
                }
                else if (tiNpcsInsideStore == null)
                {
                    Debug.LogError($"CustomerManager: tiNpcsInsideStore collection is null! Cannot track TI NPC '{customerRunner.TiData.Id}' exiting.", this);
                }
                else // !Contains(customerRunner.TiData)
                {
                    Debug.LogWarning($"CustomerManager: Received NpcExitedStoreEvent for TI NPC '{customerRunner.TiData.Id}' ({customerRunner.gameObject.name}) but it was not in the tiNpcsInsideStore list. State inconsistency?", eventArgs.NpcObject);
                }
            }
            else // Transient NPC
            {
                if (activeCustomers.Contains(customerRunner))
                {
                    activeCustomers.Remove(customerRunner);
                    Debug.Log($"CustomerManager: Transient NPC ({customerRunner.gameObject.name}) exited the store (received NpcExitedStoreEvent). Total active (inside store): {activeCustomers.Count + tiNpcsInsideStore.Count}");
                }
                else // !Contains(customerRunner)
                {
                    Debug.LogWarning($"CustomerManager: Received NpcExitedStoreEvent for Transient NPC '{eventArgs.NpcObject.name}' but it was not in the activeCustomers list. State inconsistency?", eventArgs.NpcObject);
                }
            }
        }


        /// <summary>
        /// Handles the QueueSpotFreedEvent. Signals that an NPC is leaving a specific queue spot.
        /// This method is called by the OnExit of the QueueStateSO.
        /// It starts the cascade of move-up commands *from* the spot that was freed.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the queue type and spot index that published the event.</param>
        private void HandleQueueSpotFreed(QueueSpotFreedEvent eventArgs)
        {
            QueueType type = eventArgs.Type;
            int spotIndex = eventArgs.SpotIndex; 

            // Only handling Main queue now
            if (type != QueueType.Main) return;
            if (spotIndex < 0 || mainQueueSpots == null || spotIndex >= mainQueueSpots.Count) return;

            QueueSpot spotThatPublished = mainQueueSpots[spotIndex]; 
            if (spotThatPublished.IsOccupied) spotThatPublished.currentOccupant = null; 

            // Cascade Main Queue
            for (int currentSpotIndex = spotIndex + 1; currentSpotIndex < mainQueueSpots.Count; currentSpotIndex++)
            {
                QueueSpot currentSpotData = mainQueueSpots[currentSpotIndex];

                if (currentSpotData.IsOccupied)
                {
                    Game.NPC.NpcStateMachineRunner runnerToMove = currentSpotData.currentOccupant;
                    if (runnerToMove == null || !runnerToMove.gameObject.activeInHierarchy)
                    {
                        currentSpotData.currentOccupant = null;
                        continue;
                    }

                    int nextSpotIndex = currentSpotIndex - 1;
                    QueueSpot nextSpotData = mainQueueSpots[nextSpotIndex];
                    nextSpotData.currentOccupant = runnerToMove;

                    if (runnerToMove.QueueHandler != null)
                    {
                        runnerToMove.QueueHandler.MoveToQueueSpot(nextSpotData.spotTransform, nextSpotIndex, QueueType.Main);
                    }
                }
            } 
        }

        /// <summary>
        /// Called by an NpcStateMachineRunner when it completes a MoveToQueueSpot command.
        /// This signifies that the Runner has arrived at its *new* spot, and its *previous* spot is now free.
        /// NOTE: This is called *immediately* when the Runner *starts* the move, not on arrival.
        /// </summary>
        /// <param name="queueType">The type of queue the move occurred within.</param>
        /// <param name="previousSpotIndex">The index of the spot the runner *just left* (which is now physically free).</param>
        /// <returns>True if the spot was successfully marked free, false otherwise.</returns>
        public bool FreePreviousQueueSpotOnArrival(QueueType queueType, int previousSpotIndex)
        {
            // Only Main Queue supported
            if (queueType != QueueType.Main) return false;

            if (mainQueueSpots == null || previousSpotIndex < 0 || previousSpotIndex >= mainQueueSpots.Count) return false;

            QueueSpot spotToFree = mainQueueSpots[previousSpotIndex];
            if (spotToFree.IsOccupied) 
            {
                spotToFree.currentOccupant = null; 
                return true;
            }
            return true; 
        }


        /// <summary>
        /// Handles the CashRegisterFreeEvent. Signals that the register is available for the *next customer in the queue*.
        /// This method attempts to send the customer at Main Queue spot 0 to the register *only if no Cashier is present*.
        /// </summary>
        /// <param name="eventArgs">The event arguments (currently empty).</param>
        private void HandleCashRegisterFree(CashRegisterFreeEvent eventArgs)
        {
            Debug.Log("CustomerManager: Handling CashRegisterFreeEvent.");

            // ---= Check if the register is staffed by a Cashier ---
            if (cashRegister != null && cashRegister.IsStaffedByCashier)
            {
                Debug.Log("CustomerManager: CashRegisterFreeEvent received, but the register is staffed by a Cashier. Not sending the next customer from the queue.", this);
                return; // Exit the handler, the Cashier manages the flow now
            }


            // --- Existing Logic ---
            if (mainQueueSpots == null || mainQueueSpots.Count == 0)
            {
                Debug.LogWarning("CustomerManager: HandleCashRegisterFree called but mainQueueSpots list is null or empty.", this);
                return;
            }

            QueueSpot spotZero = mainQueueSpots[0];

            if (spotZero.IsOccupied)
            {
                Game.NPC.NpcStateMachineRunner runnerAtSpot0 = spotZero.currentOccupant;

                // Robustness check for valid Runner reference
                if (runnerAtSpot0 == null || !runnerAtSpot0.gameObject.activeInHierarchy || runnerAtSpot0.GetCurrentState() == null || !runnerAtSpot0.GetCurrentState().HandledState.Equals(CustomerState.Queue))
                {
                    Debug.LogError($"CustomerManager: Inconsistency detected! Main Queue spot 0 is marked occupied by a Runner ('{runnerAtSpot0?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or not in the Queue state ('{runnerAtSpot0?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot 0 free.", this);
                    spotZero.currentOccupant = null; // Force free this inconsistent spot
                    HandleQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Main, 0)); // Trigger cascade manually from spot 0
                }
                else
                {
                    // Clear spot 0's occupant reference immediately
                    spotZero.currentOccupant = null; // <-- Clear spot 0's occupant

                    // Signal the Runner to go to the register
                    Debug.Log($"CustomerManager: Found {runnerAtSpot0.gameObject.name} occupying Main Queue spot 0. Clearing spot 0 and Signalling them to move to register.");
                    if (runnerAtSpot0.QueueHandler != null)
                    {
                        runnerAtSpot0.QueueHandler.GoToRegisterFromQueue(); // Tell the runner to move
                    }
                    else
                    {
                        Debug.LogError($"CustomerManager: Runner '{runnerAtSpot0.gameObject.name}' is missing its NpcQueueHandler component! Cannot signal move to register.", runnerAtSpot0.gameObject);
                        // This NPC is likely stuck.
                    }
                }
            }
            else
            {
                Debug.Log("CustomerManager: CashRegisterFreeEvent received, but Main Queue spot 0 is not occupied.", this);
                if (mainQueueSpots.Count > 0)
                {
                    Debug.LogWarning($"CustomerManager: Main Queue spot 0 is unexpectedly free. Manually triggering cascade from spot 1 just in case.", this);
                    HandleQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Main, 0)); // Trigger cascade from spot 1
                }
            }
        }

        /// <summary>
        /// Coroutine to handle timed customer spawning (trickle).
        /// Spawning now depends on whether there is *any* room in the store.
        /// </summary>
        private IEnumerator SpawnCustomerCoroutine()
        {
            while (true) 
            {
                if (!IsStoreFull())
                {
                    float spawnDelay = Random.Range(minSpawnInterval, maxSpawnInterval);
                    yield return new WaitForSeconds(spawnDelay);
                    
                    if (!IsStoreFull()) 
                    {
                        SpawnCustomer(false); 
                    }
                }
                else
                {
                    yield return new WaitForSeconds(minSpawnInterval / 2f);
                }
            }
        }

        /// <summary>
        /// Coroutine to handle periodic bus arrivals and burst spawning.
        /// Attempts to spawn npcsPerBus customers if store capacity allows.
        /// </summary>
        private IEnumerator BusArrivalCoroutine()
        {
            if (initialBusDelay > 0) yield return new WaitForSeconds(initialBusDelay);

            while (true) 
            {
                for (int i = 0; i < npcsPerBus; i++)
                {
                    if (!IsStoreFull())
                    {
                        if (busSpawnPoints == null || busSpawnPoints.Count == 0) break; 

                        SpawnCustomer(true); 

                        if (delayBetweenBusSpawns > 0 && i < npcsPerBus - 1 && !IsStoreFull())
                        {
                            yield return new WaitForSeconds(delayBetweenBusSpawns);
                        }
                    }
                    else
                    {
                        break; 
                    }
                }
                yield return new WaitForSeconds(BusArrivalInterval);
            }
        }


        // --- Public methods for CustomerAI to request navigation/system info ---

        /// <summary>
        /// Gets an available Browse location that is NOT currently targeted by another NPC.
        /// Returns null if all locations are taken.
        /// </summary>
        /// <param name="requestingRunner">The NPC asking for a spot (so they don't block themselves).</param>
        public BrowseLocation? GetAvailableBrowseLocation(Game.NPC.NpcStateMachineRunner requestingRunner)
        {
            if (BrowseLocations == null || BrowseLocations.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No Browse locations assigned!");
                return null;
            }

            // 1. Identify occupied points
            // We use a HashSet for fast lookup of occupied Transforms
            HashSet<Transform> occupiedPoints = new HashSet<Transform>();

            // Helper to check a runner and add their target to the occupied list
            void MarkRunnerTargetAsOccupied(Game.NPC.NpcStateMachineRunner runner)
            {
                // We only care if:
                // - The runner exists
                // - It is NOT the runner currently asking (we can stay at our own spot or re-pick it)
                // - It has a valid target browse point
                if (runner != null && runner != requestingRunner && 
                    runner.CurrentTargetLocation.HasValue && 
                    runner.CurrentTargetLocation.Value.browsePoint != null)
                {
                    occupiedPoints.Add(runner.CurrentTargetLocation.Value.browsePoint);
                }
            }

            // Check Active Transient Customers
            if (activeCustomers != null)
            {
                foreach (var runner in activeCustomers)
                {
                    MarkRunnerTargetAsOccupied(runner);
                }
            }

            // Check Active True Identity (TI) NPCs inside the store
            if (tiNpcsInsideStore != null)
            {
                foreach (var tiData in tiNpcsInsideStore)
                {
                    if (tiData != null && tiData.NpcGameObject != null)
                    {
                        var runner = tiData.NpcGameObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
                        MarkRunnerTargetAsOccupied(runner);
                    }
                }
            }

            // 2. Create a list of available locations
            List<BrowseLocation> availableLocations = new List<BrowseLocation>();
            for (int i = 0; i < BrowseLocations.Count; i++)
            {
                // Only add if the browsePoint is NOT in the occupied set
                if (!occupiedPoints.Contains(BrowseLocations[i].browsePoint))
                {
                    availableLocations.Add(BrowseLocations[i]);
                }
            }

            // 3. Return a random one from the available list, or null if empty
            if (availableLocations.Count == 0)
            {
                // Debug.Log($"CustomerManager: No available browse locations for {requestingRunner?.name}. All {BrowseLocations.Count} are occupied.");
                return null; 
            }

            return availableLocations[Random.Range(0, availableLocations.Count)];
        }

        /// <summary>
        /// Redirects to GetAvailableBrowseLocation(null) to prevent stacking 
        /// even for legacy calls that don't provide a runner.
        /// </summary>
        public BrowseLocation? GetRandomBrowseLocation()
        {
            return GetAvailableBrowseLocation(null);
        }

        /// <summary>
        /// Retrieves a specific BrowseLocation by its index in the list.
        /// Used for restoring state from save files.
        /// </summary>
        public BrowseLocation? GetBrowseLocation(int index)
        {
            if (BrowseLocations != null && index >= 0 && index < BrowseLocations.Count)
            {
                return BrowseLocations[index];
            }
            return null;
        }

        /// <summary>
        /// Finds the index of a specific BrowseLocation in the list.
        /// Used for saving state to files.
        /// </summary>
        public int GetBrowseLocationIndex(BrowseLocation location)
        {
            if (BrowseLocations == null) return -1;

            for (int i = 0; i < BrowseLocations.Count; i++)
            {
                // Compare the unique Transform reference to identify the location
                if (BrowseLocations[i].browsePoint == location.browsePoint)
                {
                    return i;
                }
            }
            return -1;
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

        /// <summary>
        /// Manually restores an NPC to a specific queue spot. 
        /// Used by the Save/Load system to rebuild the queue state.
        /// </summary>
        public void RestoreQueueOccupant(Game.NPC.NpcStateMachineRunner runner, QueueType type, int index)
        {
            List<QueueSpot> targetQueue = null;
            if (type == QueueType.Main) targetQueue = mainQueueSpots;

            if (targetQueue != null && index >= 0 && index < targetQueue.Count)
            {
                QueueSpot spot = targetQueue[index];
                
                // Force assignment
                spot.currentOccupant = runner;
                
                Debug.Log($"CustomerManager: Restored '{runner.name}' to {type} Queue Spot {index}.");
            }
            else
            {
                Debug.LogWarning($"CustomerManager: Failed to restore '{runner.name}' to {type} Queue Spot {index}. Spot invalid or queue list null.");
            }
        }

        /// <summary>
        /// Attempts to add a customer to the main queue.
        /// Finds the first available spot based on the QueueSpotData list.
        /// </summary>
        /// <param name="customerRunner">The customer Runner trying to join.</param>
        /// <param name="assignedSpot">Output: The Transform of the assigned queue spot, or null.</param>
        /// <param name="spotIndex">Output: The index of the assigned queue spot, or -1.</param>
        /// <returns>True if successfully joined the queue, false otherwise (e.g., queue is full).</returns>
        public bool TryJoinQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (mainQueueSpots == null || mainQueueSpots.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join main queue - mainQueueSpots list is null or empty!"); return false; }

            foreach (var spotData in mainQueueSpots)
            {
                if (!spotData.IsOccupied)
                {
                    spotData.currentOccupant = customerRunner; // <-- Assign the Runner to the spot in Manager's data
                    assignedSpot = spotData.spotTransform;
                    spotIndex = spotData.spotIndex;
                    Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined main queue at spot {spotIndex}.");

                    // Call the public method on the QueueHandler to receive the assignment
                    if (customerRunner.QueueHandler != null)
                    {
                        customerRunner.QueueHandler.ReceiveQueueAssignment(spotIndex, QueueType.Main);
                    }
                    else
                    {
                        Debug.LogError($"CustomerManager: Runner '{customerRunner.gameObject.name}' is missing its NpcQueueHandler component! Cannot assign queue spot.", customerRunner.gameObject);
                        // Revert the spot assignment in manager's data if we can't tell the handler
                        spotData.currentOccupant = null;
                        return false; // Signal failure
                    }

                    return true; // Success
                }
            }

            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join main queue - main queue is full.");
            return false;
        }


        /// <summary>
        /// Signals that a customer is currently moving towards or is at the register.
        /// Now ensures the customer is correctly tracked in the manager's active lists (Critical for Save/Load).
        /// </summary>
        /// <param name="customerRunner">The customer Runner that is now occupying the register spot.</param>
        public void SignalCustomerAtRegister(Game.NPC.NpcStateMachineRunner customerRunner)
        {
            if (customerRunner == null) { Debug.LogWarning("CustomerManager: SignalCustomerAtRegister called with null customerRunner."); return; }

            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) is being signalled as being at the register.");
        }

        /// <summary>
        /// Gets the Transform for a specific main queue point.
        /// </summary>
        /// <param name="index">The index of the desired queue point.</param>
        /// <returns>The Transform of the queue point, or null if index is out of bounds.</returns>
        public Transform GetQueuePoint(int index)
        {
            if (mainQueueSpots != null && index >= 0 && index < mainQueueSpots.Count)
            {
                return mainQueueSpots[index].spotTransform;
            }
            Debug.LogWarning($"CustomerManager: Requested main queue point index {index} is out of bounds or mainQueueSpots list is null!");
            return null;
        }


        /// <summary>
        /// Checks if the register is currently occupied by a customer OR staffed by a Cashier.
        /// </summary>
        public bool IsRegisterOccupied()
        {
            // 1. Check Active Transient Customers
            if (activeCustomers != null)
            {
                foreach (var activeRunner in activeCustomers) 
                {
                    if (activeRunner != null && activeRunner.GetCurrentState() != null)
                    {
                        System.Enum state = activeRunner.GetCurrentState().HandledState;
                        if (state.Equals(CustomerState.WaitingAtRegister) || 
                            state.Equals(CustomerState.TransactionActive) || 
                            state.Equals(CustomerState.MovingToRegister))
                        {
                            return true;
                        }
                    }
                }
            }

            // 2. Check Active TI Customers (FIX: Was previously missing)
            if (tiNpcsInsideStore != null)
            {
                foreach (var tiData in tiNpcsInsideStore)
                {
                    // Check if the TI NPC is physically instantiated and active
                    if (tiData != null && tiData.NpcGameObject != null)
                    {
                        var runner = tiData.NpcGameObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
                        if (runner != null && runner.GetCurrentState() != null)
                        {
                            System.Enum state = runner.GetCurrentState().HandledState;
                            if (state.Equals(CustomerState.WaitingAtRegister) || 
                                state.Equals(CustomerState.TransactionActive) || 
                                state.Equals(CustomerState.MovingToRegister))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false; // Not occupied
        }

        /// <summary>
        /// Sets the active state of the store simulation in the CustomerManager.
        /// This is called by the StoreSimulationManager or other systems like a proximity monitor.
        /// </summary>
        /// <param name="pause">True to request a pause, false to release the pause request.</param>
        /// <param name="source">The system making the request.</param>
        public void SetStoreSimulationActive(bool pause, StorePauseSource source)
        {
            bool wasPaused = IsStoreSimulationActive; // Check state before the change

            if (pause)
            {
                pauseRequesters.Add(source);
            }
            else
            {
                pauseRequesters.Remove(source);
            }

            bool isPaused = IsStoreSimulationActive; // Check state after the change

            // Only log if the overall paused state has changed
            if (wasPaused != isPaused)
            {
                Debug.Log($"CustomerManager: Overall store activity state changed to PAUSED: {isPaused}. Active NPC spawning will be {(isPaused ? "paused" : "resumed")}.", this);
            }
            Debug.Log($"CustomerManager: Pause request from '{source}' set to '{pause}'. Total requesters: {pauseRequesters.Count}.", this);
        }


        public int GetMainQueueCount()
        {
            if (mainQueueSpots == null) return 0;
            int count = 0;
            foreach (var spotData in mainQueueSpots)
            {
                if (spotData.IsOccupied) // Count occupied spots
                {
                    count++;
                }
            }
            return count;
        }

        public bool IsMainQueueFull()
        {
            if (mainQueueSpots == null || mainQueueSpots.Count == 0) return false;

            return mainQueueSpots[mainQueueSpots.Count - 1].IsOccupied;
        }

        public bool IsTiNpcInsideStore(TiNpcData tiData)
        {
            if (tiData == null) return false;
            // Use the null-conditional operator for safety if tiNpcsInsideStore is null
            return tiNpcsInsideStore?.Contains(tiData) ?? false;
        }
        
        /// <summary>
        /// Gets the list of currently active transient NpcStateMachineRunners.
        /// This is intended for other managers to read the state of active customers.
        /// </summary>
        /// <returns>A list of active transient NpcStateMachineRunners.</returns>
        public List<Game.NPC.NpcStateMachineRunner> GetActiveTransientRunners()
        {
            return activeCustomers;
        }
    }
}
// --- END OF FILE CustomerManager.cs ---