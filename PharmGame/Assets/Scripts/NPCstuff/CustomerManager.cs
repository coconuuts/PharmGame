// --- START OF FILE CustomerManager.cs ---

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

        [Tooltip("Points where customers will form a secondary queue outside the store, ordered from furthest to closest to the entrance.")]
        [SerializeField] private List<Transform> secondaryQueuePoints;


        [Tooltip("Points where customers will exit the store.")]
        [SerializeField] private List<Transform> exitPoints;


        // --- Internal State ---
        private PoolingManager poolingManager;
        // The activeCustomers list will now represent *Transient* customers inside the store.
        private List<Game.NPC.NpcStateMachineRunner> activeCustomers = new List<Game.NPC.NpcStateMachineRunner>(); // Track Transient customers *inside the store*

        // --- Track TI NPCs inside the store by their persistent data ---
        private HashSet<TiNpcData> tiNpcsInsideStore; // Track TI customers *inside the store* by data

        private List<QueueSpot> mainQueueSpots;
        private List<QueueSpot> secondaryQueueSpots;

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

            secondaryQueueSpots = new List<QueueSpot>();
            if (secondaryQueuePoints == null || secondaryQueuePoints.Count == 0)
            {
                Debug.LogWarning("CustomerManager: No secondary queue points assigned! Secondary queue system will not function.", this);
            }
            else
            {
                for (int i = 0; i < secondaryQueuePoints.Count; i++)
                {
                    if (secondaryQueuePoints[i] != null)
                    {
                        secondaryQueueSpots.Add(new QueueSpot(secondaryQueuePoints[i], i, QueueType.Secondary));
                    }
                    else
                    {
                        Debug.LogWarning($"CustomerManager: Secondary queue point at index {i} is null!", this);
                    }
                }
                Debug.Log($"CustomerManager: Initialized secondary queue with {secondaryQueueSpots.Count} spots.");
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
            // --- END Find ---


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

            // Unsubscribe from events for managing activeCustomers count
            EventManager.Unsubscribe<NpcEnteredStoreEvent>(HandleNpcEnteredStore);
            EventManager.Unsubscribe<NpcExitedStoreEvent>(HandleNpcExitedStore);

            // Unsubscribe from future interruption events
            // EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);

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
        /// Spawns a new customer from the pool if conditions allow.
        /// The condition now relies on whether there is *any* space in the secondary queue,
        /// as the decision to enter the store (and add to activeCustomers) is made later.
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
            // --- END ---

            // Determine which spawn points to use
            List<Transform> currentSpawnPoints = isBusSpawn ? busSpawnPoints : spawnPoints;

            // Check if there's capacity in the secondary queue to *spawn* the NPC.
            // They will wait here until store capacity allows them to transition to Entering.
            // NOTE: This check is now also done *before* calling SpawnCustomer in the coroutines,
            // but keeping it here as a defensive check and for clarity.
            if (poolingManager == null || npcPrefabs == null || npcPrefabs.Count == 0 || currentSpawnPoints == null || currentSpawnPoints.Count == 0 || secondaryQueueSpots == null || secondaryQueueSpots.Count == 0 || !HasAvailableSecondaryQueueSpot())
            {
                // This log is useful for debugging why a spawn *attempt* failed due to capacity
                // It's commented out because the calling coroutines now log this more specifically.
                // Debug.Log($"CustomerManager: Spawn conditions not met. Pool Mgr: {poolingManager != null}, Prefabs: {npcPrefabs?.Count}, Spawns: {currentSpawnPoints?.Count}, Secondary Queue Has Space: {HasAvailableSecondaryQueueSpot()}, Secondary Spots: {secondaryQueueSpots?.Count}");
                return; // Do not attempt to spawn if secondary queue is full or no valid spawn points
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
                    // --- End Check ---

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

                    // Once spawned, they immediately try to join the secondary queue (via Initializing -> LookToShop logic)
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
        /// Checks if there is at least one unoccupied spot in the secondary queue.
        /// </summary>
        private bool HasAvailableSecondaryQueueSpot()
        {
            if (secondaryQueueSpots == null || secondaryQueueSpots.Count == 0) return false;

            // A spot is available if its currentOccupant is null.
            // We only need *one* spot available to allow spawning a customer to wait there.
            // TryJoinSecondaryQueue handles finding the *first* available spot.
            foreach (var spotData in secondaryQueueSpots)
            {
                if (!spotData.IsOccupied)
                {
                    return true;
                }
            }
            return false; // No free spot found
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
                Debug.LogWarning($"CustomerManager: Received NpcReturningToPoolEvent for GameObject '{npcObject.name}' without NpcStateMachineRunner component. Attempting direct return.", npcObject);
                // Defensive check if it might still be in the active list (shouldn't be)
                if (activeCustomers.Contains(runner))
                {
                    Debug.LogWarning($"CustomerManager: Null runner in activeCustomers list! Removing defensively.", this);
                    activeCustomers.Remove(runner);
                }
                if (npcObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(npcObject);
                else Destroy(npcObject); // Fallback
                return; // Exit handling
            }

            // --- Differentiate between TI and Transient NPCs ---
            if (runner.IsTrueIdentityNpc)
            {
                Debug.Log($"CustomerManager: Handling NpcReturningToPoolEvent for TI NPC '{runner.TiData?.Id ?? "Unknown TI NPC"}'. Handing off to TiNpcManager.", npcObject);

                // This is a TI NPC. It should be handled by the TiNpcManager for deactivation and pooling.
                TiNpcManager tiManager = TiNpcManager.Instance;
                if (tiManager != null)
                {
                    // The Runner's Deactivate() should have already been called by TransitionToState(ReturningToPool)
                    // Here, we just need to tell the TiNpcManager that this GameObject is ready for pooling.
                    // We need a method on TiNpcManager to receive the GameObject.
                    tiManager.HandleTiNpcReturnToPool(npcObject);
                }
                else
                {
                    Debug.LogError($"CustomerManager: Received NpcReturningToPoolEvent for TI NPC '{runner.TiData?.Id ?? "Unknown TI NPC"}' but TiNpcManager.Instance is null! Cannot properly handle deactivation and pooling. Destroying object.", npcObject);
                    // Fallback: If TiNpcManager is missing, we can't save data or pool correctly. Destroy.
                    Destroy(npcObject);
                }

                // Regardless of TiNpcManager outcome, we are done with this event in CustomerManager
                // for this specific NPC type. Do NOT proceed with the transient pooling logic below.
                // Also ensure it's removed from activeCustomers list if it somehow remained (should be removed by Exited state).
                // NOTE: TI NPCs are NOT added to activeCustomers in the new logic,
                // so this defensive check for activeCustomers.Contains(runner) for TI NPCs is no longer strictly necessary
                // but doesn't hurt. The check for tiNpcsInsideStore is also not needed here, as removal from that
                // happens via NpcExitedStoreEvent.
                if (activeCustomers.Contains(runner))
                {
                    Debug.LogWarning($"CustomerManager: TI NPC '{runner.TiData?.Id ?? "Unknown TI NPC"}' was still in activeCustomers list! Removing defensively.", this);
                    activeCustomers.Remove(runner);
                }

                // --- Add cleanup for potentially occupied queue spots for TI NPCs ---
                // If the Runner was assigned to a queue spot when it was pooled,
                // ensure that spot's currentOccupant reference is cleared.
                // This is defensive; Runner.Deactivate() should clear its AssignedQueueSpotIndex
                // and states should publish QueueSpotFreedEvent on exit.
                if (runner.QueueHandler != null && runner.QueueHandler.AssignedQueueSpotIndex != -1)
                {
                    Debug.LogWarning($"CustomerManager: TI Runner '{npcObject.name}' was pooled but still assigned to queue spot index {runner.QueueHandler.AssignedQueueSpotIndex} in {runner.QueueHandler._currentQueueMoveType} queue. Forcing spot free.", this);
                    List<QueueSpot> targetQueue = (runner.QueueHandler._currentQueueMoveType == QueueType.Main) ? mainQueueSpots : secondaryQueueSpots;
                    if (targetQueue != null && runner.QueueHandler.AssignedQueueSpotIndex >= 0 && runner.QueueHandler.AssignedQueueSpotIndex < targetQueue.Count)
                    {
                        QueueSpot spot = targetQueue[runner.QueueHandler.AssignedQueueSpotIndex];
                        if (spot.currentOccupant == runner) // Double check it's this specific runner
                        {
                            spot.currentOccupant = null; // Force free the spot
                            Debug.Log($"CustomerManager: Queue spot {runner.QueueHandler.AssignedQueueSpotIndex} in {runner.QueueHandler._currentQueueMoveType} queue manually freed during TI pooling cleanup.", this);
                        }
                        else if (spot.currentOccupant != null)
                        {
                            Debug.LogWarning($"CustomerManager: Queue spot {runner.QueueHandler.AssignedQueueSpotIndex} in {runner.QueueHandler._currentQueueMoveType} was occupied by a different NPC when TI pooled cleanup ran for '{npcObject.name}'. Data inconsistency!", this);
                        }
                    }
                    runner.QueueHandler.AssignedQueueSpotIndex = -1; // Clear index on runner defensively
                }
                return; // Exit the method, let TiNpcManager handle pooling for TI NPCs
            }

            // --- Existing Transient NPC Pooling Logic ---
            // This else block contains the original logic for transient customers
            Debug.Log($"CustomerManager: Handling NpcReturningToPoolEvent for Transient NPC '{npcObject.name}'. Returning to pool.", npcObject);

            // Remove from active list (should be removed by NpcExitedStoreEvent, but defensive)
            // NOTE: This list ONLY contains Transient NPCs
            if (activeCustomers.Contains(runner))
            {
                Debug.LogWarning($"CustomerManager: Transient NPC '{npcObject.name}' was still in activeCustomers list! Removing defensively.", this);
                activeCustomers.Remove(runner);
            }

            // Add cleanup for potentially occupied queue spots for Transient NPCs
            // If the Runner was assigned to a queue spot when it was pooled,
            // ensure that spot's currentOccupant reference is cleared.
            // This is defensive; states should publish QueueSpotFreedEvent on exit.
            if (runner.QueueHandler.AssignedQueueSpotIndex != -1)
            {
                Debug.LogWarning($"CustomerManager: Transient Runner '{npcObject.name}' was pooled but still assigned to queue spot index {runner.QueueHandler.AssignedQueueSpotIndex} in {runner.QueueHandler._currentQueueMoveType} queue. Forcing spot free.", this);
                List<QueueSpot> targetQueue = (runner.QueueHandler._currentQueueMoveType == QueueType.Main) ? mainQueueSpots : secondaryQueueSpots;
                if (targetQueue != null && runner.QueueHandler.AssignedQueueSpotIndex >= 0 && runner.QueueHandler.AssignedQueueSpotIndex < targetQueue.Count)
                {
                    QueueSpot spot = targetQueue[runner.QueueHandler.AssignedQueueSpotIndex];
                    if (spot.currentOccupant == runner) // Double check it's this specific runner
                    {
                        spot.currentOccupant = null; // Force free the spot
                        Debug.Log($"CustomerManager: Queue spot {runner.QueueHandler.AssignedQueueSpotIndex} in {runner.QueueHandler._currentQueueMoveType} queue manually freed during transient pooling cleanup.", this);
                    }
                    else if (spot.currentOccupant != null)
                    {
                        Debug.LogWarning($"CustomerManager: Queue spot {runner.QueueHandler.AssignedQueueSpotIndex} in {runner.QueueHandler._currentQueueMoveType} was occupied by a different NPC when transient pooled cleanup ran for '{npcObject.name}'. Data inconsistency!", this);
                    }
                }
                runner.QueueHandler.AssignedQueueSpotIndex = -1; // Clear index on runner defensively
            }
            // Return the transient NPC object to the pool
            if (poolingManager != null)
            {
                poolingManager.ReturnPooledObject(npcObject);
                Debug.Log($"CustomerManager: Returned transient NPC '{npcObject.name}' to pool.", npcObject);
            }
            else
            {
                // Fallback if poolingManager is somehow null
                Debug.LogError($"CustomerManager: PoolingManager is null! Cannot return transient NPC '{npcObject.name}' to pool. Destroying object.", this);
                Destroy(npcObject);
            }
            // --- End Existing Transient NPC Pooling Logic ---
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

            // Now that a customer has successfully entered the store (decrementing external queue pressure),
            // check if we can release someone from the secondary queue if capacity allows.
            CheckStoreCapacityAndReleaseSecondaryCustomer(); // Check on entry into store
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

            // Now that a customer has successfully exited the store (increasing external queue pressure),
            // check if we can release someone from the secondary queue if capacity allows.
            CheckStoreCapacityAndReleaseSecondaryCustomer(); // Check on exit from store
        }


        /// <summary>
        /// Handles the QueueSpotFreedEvent. Signals that an NPC is leaving a specific queue spot.
        /// This method is called by the OnExit of the QueueStateSO or SecondaryQueueStateSO.
        /// It starts the cascade of move-up commands *from* the spot that was freed.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the queue type and spot index that published the event.</param>
        private void HandleQueueSpotFreed(QueueSpotFreedEvent eventArgs)
        {
            QueueType type = eventArgs.Type;
            int spotIndex = eventArgs.SpotIndex; // The index that was just vacated

            if (type == QueueType.Prescription)
            {
                // Debug.Log($"CustomerManager: Ignoring QueueSpotFreedEvent for Prescription queue spot {spotIndex}.", this); // Optional debug
                return; // Exit handler immediately for Prescription queue events
            }

            if (spotIndex < 0)
            {
                Debug.LogWarning($"CustomerManager: Received QueueSpotFreedEvent with invalid negative spot index {spotIndex}. Ignoring.", this);
                return;
            }

            Debug.Log($"CustomerManager: Handling QueueSpotFreedEvent for spot {spotIndex} in {type} queue (triggered by State Exit). Initiating cascade from spot {spotIndex + 1}.");

            List<QueueSpot> targetQueue = (type == QueueType.Main) ? mainQueueSpots : secondaryQueueSpots;

            if (targetQueue == null || spotIndex >= targetQueue.Count)
            {
                Debug.LogWarning($"CustomerManager: Received invalid QueueSpotFreedEvent args (index {spotIndex}, type {type}) or null target queue. Ignoring.", this);
                return;
            }

            // The freeing of spotIndex itself should have happened just before this event fired (for spot 0 exiting to Register/Exit)
            // or happens when the moving NPC arrived at the *next* spot via FreePreviousQueueSpotOnArrival.
            QueueSpot spotThatPublished = targetQueue[spotIndex]; // Get the spot data corresponding to the event source
            if (spotThatPublished.IsOccupied)
            {
                // This is an inconsistency! The spot that published the "I'm leaving" event is STILL marked occupied.
                Debug.LogError($"CustomerManager: Inconsistency detected! QueueSpotFreedEvent received for spot {spotIndex} in {type} queue, but the spot is still marked occupied by {spotThatPublished.currentOccupant.gameObject.name} (Runner). Forcing spot free.", this);
                spotThatPublished.currentOccupant = null; // Force clear the occupant reference to fix the data
            }
            else
            {
                Debug.Log($"CustomerManager: QueueSpotFreedEvent received for spot {spotIndex} in {type} queue, spot is correctly marked free.");
            }


            // Initiate the cascade of "move up" commands
            for (int currentSpotIndex = spotIndex + 1; currentSpotIndex < targetQueue.Count; currentSpotIndex++)
            {
                QueueSpot currentSpotData = targetQueue[currentSpotIndex];

                if (currentSpotData.IsOccupied)
                {
                    Game.NPC.NpcStateMachineRunner runnerToMove = currentSpotData.currentOccupant;

                    // Robustness check for valid Runner reference
                    if (runnerToMove == null || !runnerToMove.gameObject.activeInHierarchy || runnerToMove.GetCurrentState() == null || !(runnerToMove.GetCurrentState().HandledState.Equals(CustomerState.Queue) || runnerToMove.GetCurrentState().HandledState.Equals(CustomerState.SecondaryQueue)))
                    {
                        Debug.LogError($"CustomerManager: Inconsistency detected! Spot {currentSpotIndex} in {type} queue is marked occupied by a Runner ('{runnerToMove?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or not in wrong state ('{runnerToMove?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot {currentSpotIndex} free and continuing cascade search.", this);
                        currentSpotData.currentOccupant = null;
                        continue;
                    }

                    int nextSpotIndex = currentSpotIndex - 1;
                    QueueSpot nextSpotData = targetQueue[nextSpotIndex];

                    Debug.Log($"CustomerManager: Signalling {runnerToMove.gameObject.name} assigned to spot {currentSpotIndex} to move up to spot {nextSpotIndex} in {type} queue.");

                    // Set the destination spot's occupant BEFORE calling MoveToQueueSpot
                    nextSpotData.currentOccupant = runnerToMove;

                    // Call the method on the Runner to initiate the move.
                    if (runnerToMove.QueueHandler != null)
                    {
                        runnerToMove.QueueHandler.MoveToQueueSpot(nextSpotData.spotTransform, nextSpotIndex, type);
                    }
                    else
                    {
                        Debug.LogError($"CustomerManager: Runner '{runnerToMove.gameObject.name}' is missing its NpcQueueHandler component! Cannot signal move up.", runnerToMove.gameObject);
                        // This NPC is likely stuck.
                    }
                }
                else // No occupant found for this spot index
                {
                    Debug.LogWarning($"CustomerManager: No Runner found occupying spot {currentSpotIndex} in {type} queue. This spot is a gap. Continuing cascade search.", this);
                }
            } // End of cascade loop
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
            Debug.Log($"CustomerManager: Handling FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in {queueType} queue (triggered by Runner Starting Move).");

            List<QueueSpot> targetQueue = (queueType == QueueType.Main) ? mainQueueSpots : secondaryQueueSpots;
            string queueName = (queueType == QueueType.Main) ? "Main" : "Secondary";

            // Validate the previous spot index
            if (targetQueue == null || previousSpotIndex < 0 || previousSpotIndex >= targetQueue.Count)
            {
                Debug.LogWarning($"CustomerManager: Received FreePreviousQueueSpotOnArrival with invalid spot index {previousSpotIndex} for {queueName} queue. Ignoring.", this);
                return false;
            }

            // Mark the previous spot as free in the QueueSpot data
            QueueSpot spotToFree = targetQueue[previousSpotIndex];

            if (spotToFree.IsOccupied) // Check if it's occupied before freeing (defensive)
            {
                spotToFree.currentOccupant = null; // <-- Mark the spot as free when the Runner starts moving away
                Debug.Log($"CustomerManager: Spot {previousSpotIndex} in {queueName} queue is now marked free (clearing occupant reference on Runner starting move).");
                return true;
            }
            else
            {
                Debug.LogWarning($"CustomerManager: Received FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in {queueName} queue, but it was already marked as free. Inconsistency?", this);
                return true; // Return true even if already free, as the intent was achieved.
            }
        }


        /// <summary>
        /// Handles the CashRegisterFreeEvent. Signals that the register is available for the *next customer in the queue*.
        /// This method attempts to send the customer at Main Queue spot 0 to the register *only if no Cashier is present*.
        /// Also checks if a secondary queue customer can be released based on *store capacity*.
        /// </summary>
        /// <param name="eventArgs">The event arguments (currently empty).</param>
        private void HandleCashRegisterFree(CashRegisterFreeEvent eventArgs)
        {
            Debug.Log("CustomerManager: Handling CashRegisterFreeEvent.");

            // --- NEW: Check if the register is staffed by a Cashier ---
            if (cashRegister != null && cashRegister.IsStaffedByCashier)
            {
                Debug.Log("CustomerManager: CashRegisterFreeEvent received, but the register is staffed by a Cashier. Not sending the next customer from the queue.", this);
                // If a Cashier is present, the CashRegisterFreeEvent means a customer finished checkout *with the Cashier*.
                // The CashierWaitingForCustomer state handles receiving the next customer via CustomerReadyForCashierEvent.
                // We still need to check store capacity and potentially release a secondary queue customer.
                CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call the check method
                return; // Exit the handler, the Cashier manages the flow now
            }
            // --- END NEW ---


            // --- Existing Logic (only runs if no Cashier is staffed) ---
            if (mainQueueSpots == null || mainQueueSpots.Count == 0)
            {
                Debug.LogWarning("CustomerManager: HandleCashRegisterFree called but mainQueueSpots list is null or empty.", this);
                CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call the check method
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
            // --- END Existing Logic ---

            CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call the check method
        }


        /// <summary>
        /// Checks if the *store capacity* allows releasing the next customer from the secondary queue
        /// and releases them if so and if the secondary queue is not empty.
        /// This replaces the main queue threshold check.
        /// </summary>
        private void CheckStoreCapacityAndReleaseSecondaryCustomer()
        {
            // --- Use the combined count for store capacity ---
            int currentCustomersInside = activeCustomers.Count + (tiNpcsInsideStore?.Count ?? 0);

            // Release condition: Total active customers inside the store must be less than maxCustomersInStore.
            if (currentCustomersInside >= maxCustomersInStore)
            {
                Debug.Log($"CustomerManager: Cannot release from secondary queue. Store capacity ({currentCustomersInside}/{maxCustomersInStore}) is full.");
                return;
            }

            Debug.Log($"CustomerManager: Store capacity ({currentCustomersInside}/{maxCustomersInStore}) allows release from secondary queue. Attempting to release next secondary customer (Runner).");

            // Find the customer currently at the first occupied Secondary Queue spot (lowest index)
            QueueSpot firstOccupiedSpot = null;
            if (secondaryQueueSpots != null)
            {
                foreach (var spotData in secondaryQueueSpots) // Iterate through spots to find the first occupied one
                {
                    if (spotData.IsOccupied)
                    {
                        firstOccupiedSpot = spotData; // Found the first occupied spot
                        break; // Stop searching
                    }
                }
            }

            if (firstOccupiedSpot != null)
            {
                Game.NPC.NpcStateMachineRunner runnerToRelease = firstOccupiedSpot.currentOccupant;

                // Robustness check for valid Runner reference
                if (runnerToRelease == null || !runnerToRelease.gameObject.activeInHierarchy || runnerToRelease.GetCurrentState() == null || !runnerToRelease.GetCurrentState().HandledState.Equals(CustomerState.SecondaryQueue))
                {
                    Debug.LogError($"CustomerManager: Inconsistency detected! Secondary Queue spot {firstOccupiedSpot.spotIndex} is marked occupied by a Runner ('{runnerToRelease?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or not in Secondary Queue state ('{runnerToRelease?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot {firstOccupiedSpot.spotIndex} free and trying the next spot.", this);
                    firstOccupiedSpot.currentOccupant = null; // Force free this inconsistent spot
                    CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call self
                }
                else
                {
                    // Clear the spot's occupant reference immediately
                    firstOccupiedSpot.currentOccupant = null; // <-- Clear spot's occupant

                    // Publish the event for the specific NPC GameObject
                    Debug.Log($"CustomerManager: Found {runnerToRelease.gameObject.name} occupying Secondary Queue spot {firstOccupiedSpot.spotIndex}. Clearing spot and Publishing ReleaseNpcFromSecondaryQueueEvent.", runnerToRelease.gameObject);

                    EventManager.Publish(new ReleaseNpcFromSecondaryQueueEvent(runnerToRelease.gameObject));
                }
            }
            else
            {
                Debug.Log("CustomerManager: Secondary queue appears empty (no spots marked occupied).");
            }
        }


        /// <summary>
        /// Coroutine to handle timed customer spawning (trickle).
        /// Spawning now depends on whether there is *any* room in the secondary queue.
        /// </summary>
        private IEnumerator SpawnCustomerCoroutine()
        {
            while (true) // Loop indefinitely
            {
                // Only attempt to spawn if there is space in the secondary queue.
                if (HasAvailableSecondaryQueueSpot())
                {
                    float spawnDelay = Random.Range(minSpawnInterval, maxSpawnInterval);
                    yield return new WaitForSeconds(spawnDelay);
                    // SpawnCustomer() already checks HasAvailableSecondaryQueueSpot internally,
                    // but checking here prevents waiting the full interval if the queue is full.
                    if (HasAvailableSecondaryQueueSpot()) // Re-check just before spawning
                    {
                        Debug.Log($"CustomerManager: Attempting trickle spawn after {spawnDelay}s delay.");
                        SpawnCustomer(false); // Call SpawnCustomer with isBusSpawn = false // MODIFIED CALL
                    }
                    else
                    {
                        // This case is unlikely due to the outer if, but defensive
                        Debug.Log($"CustomerManager: Trickle spawn attempt skipped, secondary queue became full during wait.");
                    }
                }
                else
                {
                    // If secondary queue is full, wait a short time before checking again.
                    // This prevents the coroutine from checking every frame when full.
                    // Added a log here to indicate the pause.
                    Debug.Log($"CustomerManager: Trickle spawn paused, secondary queue is full ({GetSecondaryQueueCount()}/{secondaryQueueSpots.Count} spots occupied). Waiting for space...");
                    yield return new WaitForSeconds(minSpawnInterval / 2f);
                }
            }
        }

        /// <summary>
        /// Coroutine to handle periodic bus arrivals and burst spawning.
        /// Attempts to spawn npcsPerBus customers if secondary queue capacity allows.
        /// </summary>
        private IEnumerator BusArrivalCoroutine()
        {
            // Optional initial delay before the first bus arrives
            if (initialBusDelay > 0)
            {
                Debug.Log($"CustomerManager: Waiting for initial bus arrival delay ({initialBusDelay}s)...");
                yield return new WaitForSeconds(initialBusDelay);
            }

            while (true) // Loop indefinitely for subsequent bus arrivals
            {
                Debug.Log($"CustomerManager: Bus arrived! Attempting to spawn {npcsPerBus} customers.");
                int spawnedCount = 0; // Track successful spawns in this burst

                // Attempt to spawn npcsPerBus customers
                for (int i = 0; i < npcsPerBus; i++)
                {
                    // Check capacity *before* each spawn attempt within the burst
                    if (HasAvailableSecondaryQueueSpot())
                    {
                        // Check if bus spawn points are available
                        if (busSpawnPoints == null || busSpawnPoints.Count == 0)
                        {
                            Debug.LogWarning($"CustomerManager: Bus spawn points list is null or empty! Cannot perform bus spawn. Aborting burst.", this);
                            break; // Abort the burst if no bus spawn points
                        }

                        SpawnCustomer(true); // Call SpawnCustomer with isBusSpawn = true // MODIFIED CALL
                        spawnedCount++;

                        // Add the delay between spawns within the burst // NEW
                        // Only yield if we successfully spawned and there are more attempts planned
                        if (delayBetweenBusSpawns > 0 && i < npcsPerBus - 1 && HasAvailableSecondaryQueueSpot())
                        {
                            yield return new WaitForSeconds(delayBetweenBusSpawns);
                        }
                    }
                    else
                    {
                        // Secondary queue is full, stop attempting to spawn more from this burst
                        Debug.LogWarning($"CustomerManager: Secondary queue became full during bus burst spawn. Stopped spawning after {spawnedCount} customers.");
                        break; // Exit the for loop
                    }
                }

                Debug.Log($"CustomerManager: Bus burst completed. Successfully spawned {spawnedCount} out of {npcsPerBus} attempted customers.");

                // Wait for the next bus arrival interval
                yield return new WaitForSeconds(BusArrivalInterval);
            }
        }


        // --- Public methods for CustomerAI to request navigation/system info ---

        /// <summary>
        /// Gets a random Browse location (point and associated inventory).
        /// </summary>
        public BrowseLocation? GetRandomBrowseLocation()
        {
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
        /// Gets the Transform for a specific secondary queue point.
        /// </summary>
        public Transform GetSecondaryQueuePoint(int index)
        {
            if (secondaryQueueSpots != null && index >= 0 && index < secondaryQueueSpots.Count)
            {
                return secondaryQueueSpots[index].spotTransform;
            }
            Debug.LogWarning($"CustomerManager: Requested secondary queue point index {index} is out of bounds or secondaryQueueSpots list is null!");
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
        /// This is handled by caching the register reference on the Runner itself now.
        /// This method might become redundant if the Runner's state handles caching directly.
        /// Keeping it for now, but its purpose is reduced.
        /// </summary>
        /// <param name="customer">The customer Runner that is now occupying the register spot.</param>
        public void SignalCustomerAtRegister(Game.NPC.NpcStateMachineRunner customerRunner)
        {
            if (customerRunner == null) { Debug.LogWarning("CustomerManager: SignalCustomerAtRegister called with null customerRunner."); return; }
            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) is being signalled as being at the register (Manager tracking removed).");
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
            // Check if any active customer is in a register-related state
            foreach (var activeRunner in activeCustomers) // Check only customers currently 'inside' the store AND have an active GameObject
            {
                if (activeRunner != null && activeRunner.GetCurrentState() != null)
                {
                    System.Enum state = activeRunner.GetCurrentState().HandledState;
                    if (state.Equals(CustomerState.WaitingAtRegister) || state.Equals(CustomerState.TransactionActive) || state.Equals(CustomerState.MovingToRegister))
                    {
                        return true;
                    }
                }
            }
            // --- END Existing Logic ---

            return false; // Not occupied by a Cashier or a Customer
        }

        /// <summary>
        /// Attempts to add a customer to the secondary queue.
        /// Finds the first available spot based on the QueueSpotData list.
        /// </summary>
        /// <param name="customerRunner">The customer Runner trying to join.</param>
        /// <param name="assignedSpot">Output: The Transform of the assigned secondary queue spot, or null.</param>
        /// <param name="spotIndex">Output: The index of the assigned secondary queue spot, or -1.</param>
        /// <returns>True if successfully joined the secondary queue, false otherwise (e.g., queue is full).</returns>
        public bool TryJoinSecondaryQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (secondaryQueueSpots == null || secondaryQueueSpots.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join secondary queue - secondaryQueueSpots list is null or empty!"); return false; }

            foreach (var spotData in secondaryQueueSpots) // Iterate QueueSpot objects directly
            {
                if (!spotData.IsOccupied) // Check if spotData.currentOccupant == null
                {
                    spotData.currentOccupant = customerRunner; // <-- Assign the Runner to the spot in Manager's data
                    assignedSpot = spotData.spotTransform;
                    spotIndex = spotData.spotIndex;
                    Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined secondary queue at spot {spotIndex}.");

                    // Call the public method on the QueueHandler to receive the assignment
                    if (customerRunner.QueueHandler != null)
                    {
                        customerRunner.QueueHandler.ReceiveQueueAssignment(spotIndex, QueueType.Secondary);
                    }
                    else
                    {
                        Debug.LogError($"CustomerManager: Runner '{customerRunner.gameObject.name}' is missing its NpcQueueHandler component! Cannot assign secondary queue spot.", customerRunner.gameObject);
                        // Revert the spot assignment in manager's data if we can't tell the handler
                        spotData.currentOccupant = null;
                        return false; // Signal failure
                    }

                    return true; // Success
                }
            }

            Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join secondary queue - secondary queue is full.");
            return false;
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

        public int GetSecondaryQueueCount()
        {
            if (secondaryQueueSpots == null) return 0;
            int count = 0;
            foreach (var spotData in secondaryQueueSpots)
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

        public bool IsSecondaryQueueFull()
        {
            if (secondaryQueueSpots == null || secondaryQueueSpots.Count == 0) return false;

            // The secondary queue is considered "full" if the very last spot has an occupant.
            return secondaryQueueSpots[secondaryQueueSpots.Count - 1].IsOccupied;
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