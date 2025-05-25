// --- START OF FILE CustomerManager.cs ---

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
        [SerializeField] private int maxCustomersInStore = 5; // This limit now applies to activeCustomers.Count
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

        // --- Phase 1, Substep 2: Keep Transform lists for Inspector setup ---
        [Tooltip("Points where customers will form a queue for the cash register, ordered from closest to furthest.")]
        [SerializeField] private List<Transform> queuePoints;

        [Tooltip("Points where customers will form a secondary queue outside the store, ordered from furthest to closest to the entrance.")]
        [SerializeField] private List<Transform> secondaryQueuePoints;
        // --- END Phase 1, Substep 2 ---


        [Tooltip("Points where customers will exit the store.")]
        [SerializeField] private List<Transform> exitPoints;


        // --- Internal State ---
        private PoolingManager poolingManager;
        // The activeCustomers list will now represent customers *inside the store*.
        private List<Game.NPC.NpcStateMachineRunner> activeCustomers = new List<Game.NPC.NpcStateMachineRunner>(); // Track customers *inside the store*

        // --- Phase 1, Substep 2: Remove bool arrays and old Queue collections ---
        // private Queue<Game.NPC.NpcStateMachineRunner> customerQueue = new Queue<Game.NPC.NpcStateMachineRunner>(); // REMOVED
        // private Game.NPC.NpcStateMachineRunner customerAtRegister = null; // REMOVED (Managed by RegisterInteractable and Runner cache)
        // private bool[] queueSpotOccupied; // REMOVED
        // private Queue<Game.NPC.NpcStateMachineRunner> secondaryCustomerQueue = new Queue<Game.NPC.NpcStateMachineRunner>(); // REMOVED
        // private bool[] secondaryQueueSpotOccupied; // REMOVED
        // --- END Phase 1, Substep 2 ---

        // --- Phase 1, Substep 2: Add new QueueSpot lists ---
        private List<QueueSpot> mainQueueSpots;
        private List<QueueSpot> secondaryQueueSpots;
        // --- END Phase 1, Substep 2 ---

        // --- REMOVED Phase 4 fields related to main queue threshold ---
        // [Tooltip("If the main register queue has this many customers or fewer, release the next secondary queue customer.")]
        // [SerializeField] private int mainQueueReleaseThreshold = 2;
        // [Tooltip("How often to check if the main queue is below the threshold to release a secondary customer.")]
        // [SerializeField] private float secondaryQueueCheckInterval = 1.0f;
        // private Coroutine secondaryQueueReleaseCoroutine;
        // --- END REMOVED Phase 4 ---


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

            // --- Phase 1, Substep 2: Initialize QueueSpot lists from Transform lists ---
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
            // We no longer need the original Transform lists for runtime logic after this point,
            // but keep them serialized for Inspector setup.
            // --- END Phase 1, Substep 2 ---


            Debug.Log("CustomerManager: Awake completed.");
        }

        private void Start()
        {
            // Begin spawning customers
            StartCoroutine(SpawnCustomerCoroutine());
            // --- REMOVED: Start the secondary queue release check coroutine ---
            // secondaryQueueReleaseCoroutine = StartCoroutine(SecondaryQueueReleaseCoroutine());
            // --- END REMOVED ---
        }

        private void OnEnable() // Subscribe to events when the GameObject is enabled
        {
            // Subscribe to events published by NPCs or other systems
            EventManager.Subscribe<NpcReturningToPoolEvent>(HandleNpcReturningToPool);
            EventManager.Subscribe<QueueSpotFreedEvent>(HandleQueueSpotFreed);
            EventManager.Subscribe<CashRegisterFreeEvent>(HandleCashRegisterFree);

            // --- NEW: Subscribe to events for managing activeCustomers count ---
            EventManager.Subscribe<NpcEnteredStoreEvent>(HandleNpcEnteredStore);
            EventManager.Subscribe<NpcExitedStoreEvent>(HandleNpcExitedStore);
            // --- END NEW ---


            // Subscribe to future interruption events if CustomerManager needs to react
            // EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);

            // --- Phase 4: Subscribe to ReleaseNpcFromSecondaryQueueEvent if Manager needs to react (currently handled by Runner) ---
            // The Runner handles this event to transition state. Manager might need it for data sync?
            // Let's stick to the plan and only have Runner handle this event for now.
            // EventManager.Subscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseNpcFromSecondaryQueue);
            // --- END Phase 4 ---

            Debug.Log("CustomerManager: Subscribed to events.");
        }

        private void OnDisable() // Unsubscribe from events when the GameObject is disabled
        {
            // Unsubscribe from events to prevent memory leaks and calls on null objects
            EventManager.Unsubscribe<NpcReturningToPoolEvent>(HandleNpcReturningToPool);
            EventManager.Unsubscribe<QueueSpotFreedEvent>(HandleQueueSpotFreed);
            EventManager.Unsubscribe<CashRegisterFreeEvent>(HandleCashRegisterFree);

            // --- NEW: Unsubscribe from events for managing activeCustomers count ---
            EventManager.Unsubscribe<NpcEnteredStoreEvent>(HandleNpcEnteredStore);
            EventManager.Unsubscribe<NpcExitedStoreEvent>(HandleNpcExitedStore);
            // --- END NEW ---


            // Unsubscribe from future interruption events
            // EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            // EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);

             // --- Phase 4: Unsubscribe from secondary release event if subscribed ---
            // EventManager.Unsubscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseNpcFromSecondaryQueue);
            // --- END Phase 4 ---

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
            }
            Debug.Log("CustomerManager: OnDestroy completed.");
        }


        /// <summary>
        /// Spawns a new customer from the pool if conditions allow.
        /// The condition now relies on whether there is *any* space in the secondary queue,
        /// as the decision to enter the store (and add to activeCustomers) is made later.
        /// </summary>
        public void SpawnCustomer()
        {
            // Check if there's capacity in the secondary queue to *spawn* the NPC.
            // They will wait here until store capacity allows them to transition to Entering.
            if (poolingManager == null || npcPrefabs == null || npcPrefabs.Count == 0 || spawnPoints == null || spawnPoints.Count == 0 || secondaryQueueSpots == null || secondaryQueueSpots.Count == 0 || !HasAvailableSecondaryQueueSpot())
            {
                 // Condition to spawn is now: PoolingManager exists, prefabs/spawns exist, secondary queue exists and has space.
                 // maxCustomersInStore limit is checked *before* transitioning from SecondaryQueue to Entering.
                 Debug.Log($"CustomerManager: Spawn conditions not met. Pool Mgr: {poolingManager!=null}, Prefabs: {npcPrefabs?.Count}, Spawns: {spawnPoints?.Count}, Secondary Queue Has Space: {HasAvailableSecondaryQueueSpot()}, Secondary Spots: {secondaryQueueSpots?.Count}");
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
                    // Warp the NPC to the spawn point - this is done by the Runner's Initialize
                    customerObject.transform.position = chosenSpawnPoint.position;
                    customerObject.transform.rotation = chosenSpawnPoint.rotation;

                    // Initialize the NpcStateMachineRunner, passing the manager and spawn position
                    customerRunner.Initialize(this, chosenSpawnPoint.position);

                    // --- REMOVED: activeCustomers.Add(customerRunner); ---
                    // activeCustomers is now managed by NpcEnteredStoreEvent.
                    // --- END REMOVED ---

                    Debug.Log($"CustomerManager: Spawned customer '{customerObject.name}' (Runner) from pool at {chosenSpawnPoint.position}.");

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


        // --- Event Handlers (replace old direct call methods) ---

        /// <summary>
        /// Handles the NpcReturningToPoolEvent. Returns a customer GameObject back to the object pool.
        /// Also removes the Runner from the active list if it somehow remained there (should be removed by NpcExitedStoreEvent).
        /// Ensures any queue spot they might have incorrectly occupied is cleared.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the NPC GameObject.</param>
        private void HandleNpcReturningToPool(NpcReturningToPoolEvent eventArgs)
        {
            GameObject customerObject = eventArgs.NpcObject;
            if (customerObject == null || poolingManager == null) return;

            Game.NPC.NpcStateMachineRunner customerRunner = customerObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (customerRunner != null)
            {
                 // --- REMOVED: activeCustomers.Remove(customerRunner); ---
                 // activeCustomers is now managed by NpcExitedStoreEvent.
                 // However, defensively remove it here just in case the state transition was missed or bugged.
                 if (activeCustomers.Contains(customerRunner))
                 {
                     Debug.LogWarning($"CustomerManager: NpcReturningToPoolEvent received for '{customerObject.name}' (Runner), but it was still in activeCustomers list! Removing defensively.", this);
                     activeCustomers.Remove(customerRunner);
                 }
                 Debug.Log($"CustomerManager: Handling NpcReturningToPoolEvent for '{customerObject.name}' (Runner). Total active (inside store): {activeCustomers.Count}");

                 // --- Phase 5: Add cleanup for potentially occupied queue spots ---
                 // If the Runner was assigned to a queue spot when it was pooled,
                 // ensure that spot's currentOccupant reference is cleared.
                 if (customerRunner.AssignedQueueSpotIndex != -1)
                 {
                     Debug.LogWarning($"CustomerManager: Runner '{customerObject.name}' was pooled but still assigned to queue spot index {customerRunner.AssignedQueueSpotIndex} in {customerRunner._currentQueueMoveType} queue. Forcing spot free.", this);
                      List<QueueSpot> targetQueue = (customerRunner._currentQueueMoveType == QueueType.Main) ? mainQueueSpots : secondaryQueueSpots;
                      if (targetQueue != null && customerRunner.AssignedQueueSpotIndex >= 0 && customerRunner.AssignedQueueSpotIndex < targetQueue.Count)
                      {
                           QueueSpot spot = targetQueue[customerRunner.AssignedQueueSpotIndex];
                           if (spot.currentOccupant == customerRunner) // Double check it's this specific runner
                           {
                                spot.currentOccupant = null; // Force free the spot
                                Debug.Log($"CustomerManager: Queue spot {customerRunner.AssignedQueueSpotIndex} in {customerRunner._currentQueueMoveType} queue manually freed during pooling cleanup.", this);
                           } else if (spot.currentOccupant != null)
                           {
                               Debug.LogWarning($"CustomerManager: Queue spot {customerRunner.AssignedQueueSpotIndex} in {customerRunner._currentQueueMoveType} was occupied by a different NPC when pooled cleanup ran for '{customerObject.name}'. Data inconsistency!", this);
                           }
                      }
                 }
                 // --- END Phase 5 ---

                 poolingManager.ReturnPooledObject(customerObject);
            }
             else
             {
                  Debug.LogWarning($"CustomerManager: Received NpcReturningToPoolEvent for GameObject '{customerObject.name}' without NpcStateMachineRunner component. Attempting direct return.", customerObject);
                  // Defensive check if it was in the activeCustomers list, though it shouldn't be
                   Game.NPC.NpcStateMachineRunner runnerCandidate = customerObject.GetComponent<Game.NPC.NpcStateMachineRunner>(); // Re-get runner
                   if (activeCustomers.Contains(runnerCandidate)) // Null check inside Contains logic
                   {
                        Debug.LogWarning($"CustomerManager: NpcReturningToPoolEvent received for '{customerObject.name}' (No Runner found), but null was in activeCustomers list! Removing defensively.", this);
                        activeCustomers.Remove(runnerCandidate); // Removes null if present
                   }


                  if(customerObject.GetComponent<PooledObjectInfo>() != null) poolingManager.ReturnPooledObject(customerObject);
                  else Destroy(customerObject); // Fallback destroy if not pooled
             }
        }

        /// <summary>
        /// Handles the NpcEnteredStoreEvent. Adds the NPC to the list of customers currently inside the store.
        /// This event is published by the NpcStateMachineRunner when transitioning to the Entering state.
        /// </summary>
        private void HandleNpcEnteredStore(NpcEnteredStoreEvent eventArgs)
        {
            Game.NPC.NpcStateMachineRunner customerRunner = eventArgs.NpcObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (customerRunner != null && !activeCustomers.Contains(customerRunner))
            {
                activeCustomers.Add(customerRunner);
                Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) entered the store (received NpcEnteredStoreEvent). Total active (inside store): {activeCustomers.Count}");

                // Now that a customer has successfully entered the store (decrementing external queue pressure),
                // check if we can release someone from the secondary queue if capacity allows.
                CheckStoreCapacityAndReleaseSecondaryCustomer(); // Check on entry into store
            }
             else if (customerRunner == null)
             {
                 Debug.LogWarning($"CustomerManager: Received NpcEnteredStoreEvent for GameObject '{eventArgs.NpcObject.name}' without an NpcStateMachineRunner component.", eventArgs.NpcObject);
             }
             else // Contains(customerRunner) was true
             {
                  Debug.LogWarning($"CustomerManager: Received NpcEnteredStoreEvent for '{customerRunner.gameObject.name}' but it was already in the activeCustomers list. Duplicate event?", eventArgs.NpcObject);
             }
        }

        /// <summary>
        /// Handles the NpcExitedStoreEvent. Removes the NPC from the list of customers currently inside the store.
        /// This event is published by the NpcStateMachineRunner when transitioning to the Exiting state.
        /// </summary>
        private void HandleNpcExitedStore(NpcExitedStoreEvent eventArgs)
        {
            Game.NPC.NpcStateMachineRunner customerRunner = eventArgs.NpcObject.GetComponent<Game.NPC.NpcStateMachineRunner>();
            if (customerRunner != null && activeCustomers.Contains(customerRunner))
            {
                activeCustomers.Remove(customerRunner);
                Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) exited the store (received NpcExitedStoreEvent). Total active (inside store): {activeCustomers.Count}");

                // Now that a customer has successfully exited the store (increasing external queue pressure),
                // check if we can release someone from the secondary queue if capacity allows.
                CheckStoreCapacityAndReleaseSecondaryCustomer(); // Check on exit from store
            }
            else if (customerRunner == null)
            {
                Debug.LogWarning($"CustomerManager: Received NpcExitedStoreEvent for GameObject '{eventArgs.NpcObject.name}' without an NpcStateMachineRunner component.", eventArgs.NpcObject);
            }
            else // !Contains(customerRunner)
            {
                 Debug.LogWarning($"CustomerManager: Received NpcExitedStoreEvent for '{eventArgs.NpcObject.name}' but it was not in the activeCustomers list. State inconsistency?", eventArgs.NpcObject);
            }
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

    // Basic validation
    if (spotIndex < 0)
    {
         Debug.LogWarning($"CustomerManager: Received QueueSpotFreedEvent with invalid negative spot index {spotIndex}. Ignoring.", this);
         return;
    }

    Debug.Log($"CustomerManager: Handling QueueSpotFreedEvent for spot {spotIndex} in {type} queue (triggered by State Exit). Initiating cascade from spot {spotIndex + 1}.");

    List<QueueSpot> targetQueue = (type == QueueType.Main) ? mainQueueSpots : secondaryQueueSpots;

    // Further validation
     if (targetQueue == null || spotIndex >= targetQueue.Count)
     {
          Debug.LogWarning($"CustomerManager: Received invalid QueueSpotFreedEvent args (index {spotIndex}, type {type}) or null target queue. Ignoring.", this);
          return;
     }

    // --- Phase 2, Substep 4 (REVISED): DO NOT clear the spot here. ---
    // This event means the NPC previously assigned to this spot has LEFT the queue state.
    // The spot's occupant should *already* be null if they left via Register/Exiting/Impatience (cleared in HandleCashRegisterFree/CheckStoreCapacityAndReleaseSecondaryCustomer)
    // OR when the moving NPC arrived at the *next* spot via FreePreviousQueueSpotOnArrival.
    // Rely on those points for clearing the occupant reference.
    // The purpose of *this* handler is solely to start the cascade.
    // The freeing of spotIndex itself should have happened just before this event fired (for spot 0 exiting to Register/Exit)
    // or happens when the moving NPC arrives at the *next* spot (for spots > 0 leaving their spot).
    QueueSpot spotThatPublished = targetQueue[spotIndex]; // Get the spot data corresponding to the event source
    if (spotThatPublished.IsOccupied)
    {
        // This is an inconsistency! The spot that published the "I'm leaving" event is STILL marked occupied.
        // This means the clearing step in HandleCashRegisterFree, CheckStoreCapacityAndReleaseSecondaryCustomer, or FreePreviousQueueSpotOnArrival FAILED for this spot.
        Debug.LogError($"CustomerManager: Inconsistency detected! QueueSpotFreedEvent received for spot {spotIndex} in {type} queue, but the spot is still marked occupied by {spotThatPublished.currentOccupant.gameObject.name} (Runner). Forcing spot free.", this);
        spotThatPublished.currentOccupant = null; // Force clear the occupant reference to fix the data
    }
    else
    {
        // This is the expected case for spot 0 leaving or a spot being freed by arrival: it's already null.
        Debug.Log($"CustomerManager: QueueSpotFreedEvent received for spot {spotIndex} in {type} queue, spot is correctly marked free.");
    }
    // --- END Phase 2, Substep 4 (REVISED) ---


    // --- Phase 2, Substep 4: Initiate the cascade of "move up" commands ---
    // Iterate through ALL subsequent spots starting from the one *after* the freed spot.
    for (int currentSpotIndex = spotIndex + 1; currentSpotIndex < targetQueue.Count; currentSpotIndex++)
    {
         QueueSpot currentSpotData = targetQueue[currentSpotIndex];

         // If this spot is occupied...
         if (currentSpotData.IsOccupied)
         {
             Game.NPC.NpcStateMachineRunner runnerToMove = currentSpotData.currentOccupant;

             // --- Phase 5: Robustness check for valid Runner reference ---
             if (runnerToMove == null || !runnerToMove.gameObject.activeInHierarchy || runnerToMove.GetCurrentState() == null || !(runnerToMove.GetCurrentState().HandledState.Equals(CustomerState.Queue) || runnerToMove.GetCurrentState().HandledState.Equals(CustomerState.SecondaryQueue)) ) // Check if in either queue state
             {
                 // Found an occupied spot data but the Runner is invalid, inactive, or in wrong state.
                 Debug.LogError($"CustomerManager: Inconsistency detected! Spot {currentSpotIndex} in {type} queue is marked occupied by a Runner ('{runnerToMove?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or in wrong state ('{runnerToMove?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot {currentSpotIndex} free and continuing cascade search.", this);
                 currentSpotData.currentOccupant = null; // Force free this inconsistent spot
                 // Continue the loop - the next spot might have a valid NPC.
                 continue; // Skip the MoveToQueueSpot call for this invalid spot
             }
             // --- END Phase 5 ---


             // Tell the runner to move to the *previous* spot (currentSpotIndex - 1)
             int nextSpotIndex = currentSpotIndex - 1;
             QueueSpot nextSpotData = targetQueue[nextSpotIndex]; // Get the data for the spot they are moving *to*

             Debug.Log($"CustomerManager: Signalling {runnerToMove.gameObject.name} assigned to spot {currentSpotIndex} to move up to spot {nextSpotIndex} in {type} queue.");

             // --- FIX: Phase 2, Substep 4 (REVISED): Set the destination spot's occupant BEFORE calling MoveToQueueSpot ---
             // This marks the destination spot as occupied by the Runner that is now *moving* to it.
             // The source spot (currentSpotIndex) is marked free by the Runner's MoveToQueueSpot *immediately*.
             nextSpotData.currentOccupant = runnerToMove;
             // --- END FIX ---

             // Call the method on the Runner to initiate the move.
             // The Runner will update its internal assigned index and start moving.
             // It will *also* set its _isMovingToQueueSpot flag and store the *previous* index.
             // The source spot (currentSpotIndex) will be marked free in the data
             // WHEN THE MoveToQueueSpot is called on the Runner.
             runnerToMove.MoveToQueueSpot(nextSpotData.spotTransform, nextSpotIndex, type); // <-- Pass the new spot's transform and index
         }
         else // No occupant found for this spot index
         {
             // This indicates a true gap in the queue. Log a warning.
             Debug.LogWarning($"CustomerManager: No Runner found occupying spot {currentSpotIndex} in {type} queue. This spot is a gap. Continuing cascade search.", this);
             // Continue the loop. Do *not* break just because one spot is empty.
         }
    } // End of cascade loop
}

/// <summary>
/// Called by an NpcStateMachineRunner when it completes a MoveToQueueSpot command.
/// This signifies that the Runner has arrived at its *new* spot, and its *previous* spot is now free.
/// NOTE: With the update in Runner.MoveToQueueSpot, this method is now called *immediately* when the Runner *starts* the move, not on arrival. The name is slightly misleading now but kept for reference.
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

    // --- Phase 2, Substep 5: Mark the previous spot as free in the QueueSpot data ---
    QueueSpot spotToFree = targetQueue[previousSpotIndex];

    if (spotToFree.IsOccupied) // Check if it's occupied before freeing (defensive)
    {
         // Optional: Check if spotToFree.currentOccupant is the Runner that called this method for robustness.
         // For now, assume the call is valid and free the spot.
         // The Runner calling this method *should* have just been marked as the occupant of the *next* spot.
         spotToFree.currentOccupant = null; // <-- Mark the spot as free when the Runner starts moving away
         Debug.Log($"CustomerManager: Spot {previousSpotIndex} in {queueName} queue is now marked free (clearing occupant reference on Runner starting move).");
         return true;
    }
    else
    {
         Debug.LogWarning($"CustomerManager: Received FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in {queueName} queue, but it was already marked as free. Inconsistency?", this);
         // Return true even if already free, as the intent was achieved.
         return true;
    }
    // --- END Phase 2, Substep 5 ---
}


/// <summary>
        /// Handles the CashRegisterFreeEvent. Signals that the register is available.
        /// This method attempts to send the customer at Main Queue spot 0 to the register.
        /// Also checks if a secondary queue customer can be released based on *store capacity*.
        /// </summary>
        /// <param name="eventArgs">The event arguments (currently empty).</param>
        private void HandleCashRegisterFree(CashRegisterFreeEvent eventArgs)
        {
            Debug.Log("CustomerManager: Handling CashRegisterFreeEvent.");

            // --- Phase 2, Substep 6: Check if Main Queue spot 0 is occupied ---
            if (mainQueueSpots == null || mainQueueSpots.Count == 0)
            {
                 Debug.LogWarning("CustomerManager: HandleCashRegisterFree called but mainQueueSpots list is null or empty.", this);
                 // Still check secondary queue release? Yes, register being free *might* free up store capacity if the person leaving
                 // was counted towards it (they are, via Exiting state).
                 CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call the check method
                 return;
            }

            QueueSpot spotZero = mainQueueSpots[0];

            // If spot 0 is occupied...
            if (spotZero.IsOccupied)
            {
                Game.NPC.NpcStateMachineRunner runnerAtSpot0 = spotZero.currentOccupant;

                // --- Phase 5: Robustness check for valid Runner reference ---
                 if (runnerAtSpot0 == null || !runnerAtSpot0.gameObject.activeInHierarchy || runnerAtSpot0.GetCurrentState() == null || !runnerAtSpot0.GetCurrentState().HandledState.Equals(CustomerState.Queue))
                 {
                      // Found an occupied spot data but the Runner is invalid or in the wrong state.
                      Debug.LogError($"CustomerManager: Inconsistency detected! Main Queue spot 0 is marked occupied by a Runner ('{runnerAtSpot0?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or not in the Queue state ('{runnerAtSpot0?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot 0 free.", this);
                      spotZero.currentOccupant = null; // Force free this inconsistent spot
                      // Trigger cascade manually from spot 0 as if it left naturally.
                      HandleQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Main, 0)); // This will see spot 0 is null and start cascade from 1
                 }
                 else
                 {
                     // --- Phase 2, Substep 6 (REVISED): Clear spot 0's occupant reference immediately ---
                     // The NPC is leaving this spot *now* to go to the register. Update the data model.
                     spotZero.currentOccupant = null; // <-- Clear spot 0's occupant

                     // --- Phase 2, Substep 6: Signal the Runner to go to the register ---
                     Debug.Log($"CustomerManager: Found {runnerAtSpot0.gameObject.name} occupying Main Queue spot 0. Clearing spot 0 and Signalling them to move to register.");

                     // The Runner transitions state. Its QueueStateSO.OnExit will *still* publish QueueSpotFreedEvent(Main, 0),
                     // which HandleQueueSpotFreed will receive, find spot 0 already null (expected), and start the cascade from spot 1.
                     runnerAtSpot0.GoToRegisterFromQueue(); // Tell the runner to move
                     // --- END Phase 2, Substep 6 (REVISED) ---
                 }
            }
            else
            {
                // This might happen if the last customer in spot 0 became impatient and left,
                // or if there's a logic error leaving spot 0 occupied but no runner there.
                // The spot is already marked free in the QueueSpot data.
                Debug.Log("CustomerManager: CashRegisterFreeEvent received, but Main Queue spot 0 is not occupied.", this);
                // If spot 0 is unexpectedly null, it means the NPC left *without* the event firing or the event handler failed.
                // To recover, we should ensure the cascade is triggered just in case, but only if the list isn't empty.
                if (mainQueueSpots.Count > 0)
                {
                     Debug.LogWarning($"CustomerManager: Main Queue spot 0 is unexpectedly free. Manually triggering cascade from spot 1 just in case.", this);
                     HandleQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Main, 0)); // Trigger cascade from spot 1
                }
            }

            // --- Phase 4: Check and trigger Secondary Queue release based on threshold ---
            // This check relies on the *count* of customers currently in the main queue state, using the updated GetMainQueueCount().
            CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call the check method
            // --- END Phase 4 ---
        }

        // --- REMOVED Phase 4: New periodic check coroutine for secondary queue release ---
        // private IEnumerator SecondaryQueueReleaseCoroutine() { ... REMOVED ... }
        // --- END REMOVED Phase 4 ---


        // --- Updated Method Name and Logic ---
         /// <summary>
         /// Checks if the *store capacity* allows releasing the next customer from the secondary queue
         /// and releases them if so and if the secondary queue is not empty.
         /// This replaces the main queue threshold check.
         /// </summary>
         private void CheckStoreCapacityAndReleaseSecondaryCustomer()
         {
             // Release condition: Total active customers inside the store must be less than maxCustomersInStore.
             if (activeCustomers.Count >= maxCustomersInStore)
             {
                  // Store is at or above capacity, cannot release from secondary queue.
                  Debug.Log($"CustomerManager: Cannot release from secondary queue. Store capacity ({activeCustomers.Count}/{maxCustomersInStore}) is full.");
                  return;
             }

             Debug.Log($"CustomerManager: Store capacity ({activeCustomers.Count}/{maxCustomersInStore}) allows release from secondary queue. Attempting to release next secondary customer (Runner).");

             // Find the customer currently at the first occupied Secondary Queue spot (lowest index)
             // Phase 2, Substep 7: Use the secondaryQueueSpots list
             QueueSpot firstOccupiedSpot = null;
             if (secondaryQueueSpots != null) // Add null check for the list itself
             {
                 foreach(var spotData in secondaryQueueSpots) // Iterate through spots to find the first occupied one
                 {
                     if (spotData.IsOccupied)
                     {
                         firstOccupiedSpot = spotData; // Found the first occupied spot
                         break; // Stop searching
                     }
                 }
             }

             // If we found an occupied spot (should be index 0 if queue is managed correctly)...
             if (firstOccupiedSpot != null)
             {
                 Game.NPC.NpcStateMachineRunner runnerToRelease = firstOccupiedSpot.currentOccupant;

                 // --- Phase 5: Robustness check for valid Runner reference ---
                 if (runnerToRelease == null || !runnerToRelease.gameObject.activeInHierarchy || runnerToRelease.GetCurrentState() == null || !runnerToRelease.GetCurrentState().HandledState.Equals(CustomerState.SecondaryQueue))
                 {
                      // Found an occupied spot data but the Runner is invalid or in the wrong state.
                      Debug.LogError($"CustomerManager: Inconsistency detected! Secondary Queue spot {firstOccupiedSpot.spotIndex} is marked occupied by a Runner ('{runnerToRelease?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or not in Secondary Queue state ('{runnerToRelease?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot {firstOccupiedSpot.spotIndex} free and trying the next spot.", this);
                      firstOccupiedSpot.currentOccupant = null; // Force free this inconsistent spot
                      // Recursive call to try and find the *next* valid customer in the secondary queue.
                      CheckStoreCapacityAndReleaseSecondaryCustomer(); // Call self
                 }
                 else
                 {
                     // --- Phase 2, Substep 7 (REVISED): Clear the spot's occupant reference immediately ---
                     // The NPC is leaving this spot *now* to go into the store. Update the data model.
                      firstOccupiedSpot.currentOccupant = null; // <-- Clear spot's occupant

                     // --- Phase 2, Substep 7: Publish the event for the specific NPC GameObject ---
                     Debug.Log($"CustomerManager: Found {runnerToRelease.gameObject.name} occupying Secondary Queue spot {firstOccupiedSpot.spotIndex}. Clearing spot and Publishing ReleaseNpcFromSecondaryQueueEvent.", runnerToRelease.gameObject);

                     // Publish the event for the specific NPC GameObject that the Runner is on.
                     // The Runner will receive this event and transition out of SecondaryQueue state.
                     // Its SecondaryQueueStateSO.OnExit method will *still* publish
                     // QueueSpotFreedEvent(Secondary, firstOccupiedSpot.spotIndex).
                     // HandleQueueSpotFreed will *then* receive this event, find the spot already null (expected), and start the cascade from the next spot.
                     EventManager.Publish(new ReleaseNpcFromSecondaryQueueEvent(runnerToRelease.gameObject));
                     // --- END Phase 2, Substep 7 (REVISED) ---
                 }
             }
             else
             {
                 // No occupied spot was found. The secondary queue is empty.
                 Debug.Log("CustomerManager: Secondary queue appears empty (no spots marked occupied).");
             }
         }
        // --- END Updated Method ---


        /// <summary>
        /// Coroutine to handle timed customer spawning.
        /// Spawning now depends on whether there's *any* room in the secondary queue.
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
                    SpawnCustomer(); // Attempt to spawn a customer
                }
                else
                {
                    // If secondary queue is full, wait a short time before checking again.
                    // This prevents a tight loop when max capacity is reached.
                    // The actual check against maxCustomersInStore happens before they
                    // transition from SecondaryQueue to Entering.
                    // Wait for slightly less than the minimum spawn interval to be responsive
                    // when a secondary spot frees up.
                    yield return new WaitForSeconds(minSpawnInterval / 2f);
                     Debug.Log($"CustomerManager: Spawn paused, secondary queue is full. Waiting for space... ({secondaryQueueSpots.Count} spots)");
                }
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

         // --- Phase 2, Substep 2: Update GetSecondaryQueuePoint to use QueueSpot list ---
         public Transform GetSecondaryQueuePoint(int index)
         {
            if (secondaryQueueSpots != null && index >= 0 && index < secondaryQueueSpots.Count)
            {
                return secondaryQueueSpots[index].spotTransform; // Return the transform from the QueueSpot
            }
            Debug.LogWarning($"CustomerManager: Requested secondary queue point index {index} is out of bounds or secondaryQueueSpots list is null!");
            return null;
         }
         // --- END Phase 2, Substep 2 ---


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

        // --- Phase 2, Substep 2: Use mainQueueSpots list ---
        if (mainQueueSpots == null || mainQueueSpots.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join main queue - mainQueueSpots list is null or empty!"); return false; }

        // Iterate through the spots to find the first available one
        foreach (var spotData in mainQueueSpots) // Iterate QueueSpot objects directly
        {
            // A spot is available if its currentOccupant is null
            // Phase 2, Substep 4: Simplified check - removed isSpotAssignedToMovingRunner
            if (!spotData.IsOccupied) // Check if spotData.currentOccupant == null
            {
                // Phase 2, Substep 4: Assign the occupant
                spotData.currentOccupant = customerRunner; // <-- Assign the Runner to the spot
                assignedSpot = spotData.spotTransform;
                spotIndex = spotData.spotIndex;
                Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined main queue at spot {spotIndex}.");
                customerRunner.AssignedQueueSpotIndex = spotIndex; // Store the assigned index on the Runner
                // Phase 2, Substep 4: Store the queue type on the runner for FreePreviousQueueSpotOnArrival
                customerRunner._currentQueueMoveType = QueueType.Main; // Set the queue type on the runner
                return true; // Successfully joined a spot
            }
        }

        Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join main queue - main queue is full.");
        return false; // No free spot found
        // --- END Phase 2, Substep 2/4 ---
    }

        // --- REMOVED public ReleaseNextSecondaryCustomer ---
        // ReleaseNextSecondaryCustomer is now handled internally by CheckStoreCapacityAndReleaseSecondaryCustomer.
        // public void ReleaseNextSecondaryCustomer() { ... REMOVED ... }
        // --- END REMOVED ---


        /// <summary>
        /// Signals that a customer is currently moving towards or is at the register.
        /// This is handled by caching the register reference on the Runner itself now.
        /// This method might become redundant if the Runner's state handles caching directly.
        /// Keeping it for now, but its purpose is reduced.
        /// </summary>
        /// <param name="customer">The customer Runner that is now occupying the register spot.</param>
        public void SignalCustomerAtRegister(Game.NPC.NpcStateMachineRunner customerRunner)
        {
             // Phase 2, Substep 6: Removed Manager's customerAtRegister tracking.
             // Register caching is now done directly on the Runner (CachedCashRegister).
             // This method might be removed later if states fully manage the cache.
             if (customerRunner == null) { Debug.LogWarning("CustomerManager: SignalCustomerAtRegister called with null customerRunner."); return; }
             Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) is being signalled as being at the register (Manager tracking removed).");
        }

        /// <summary>
        /// Gets the Transform for a specific main queue point.
        /// </summary>
        /// <param name="index">The index of the desired queue point.</param>
        /// <returns>The Transform of the queue point, or null if index is out of bounds.</returns>
        // --- Phase 2, Substep 2: Update GetQueuePoint to use QueueSpot list ---
        public Transform GetQueuePoint(int index)
        {
            if (mainQueueSpots != null && index >= 0 && index < mainQueueSpots.Count)
            {
                return mainQueueSpots[index].spotTransform; // Return the transform from the QueueSpot
            }
            Debug.LogWarning($"CustomerManager: Requested main queue point index {index} is out of bounds or mainQueueSpots list is null!");
            return null;
        }
        // --- END Phase 2, Substep 2 ---


        /// <summary>
        /// Checks if the register is currently occupied by a customer.
        /// This check relies on iterating through active customers and checking their state.
        /// </summary>
        public bool IsRegisterOccupied()
        {
            // Check active customers (those inside the store) for their state.
            // This is more reliable than a single Manager field, as the state machine controls the NPC's status.
            foreach(var activeRunner in activeCustomers) // Check only customers currently 'inside' the store
            {
                if (activeRunner != null && activeRunner.GetCurrentState() != null)
                {
                    System.Enum state = activeRunner.GetCurrentState().HandledState;
                    if (state.Equals(CustomerState.WaitingAtRegister) || state.Equals(CustomerState.TransactionActive) || state.Equals(CustomerState.MovingToRegister))
                    {
                        // Found a runner that is involved with the register. Consider it occupied.
                        return true;
                    }
                }
            }
             // ALSO check secondary queue? No, register occupied refers to the *register itself*, not the waiting lines.
             // Secondary queue is waiting to enter the *store*, not the register.
             // Main queue is waiting *for* the register, but the register spot itself is only occupied by one person at a time.
             // The definition of "RegisterOccupied" should match what the decision points care about: Can I walk straight to the register *now*?
             // This is true only if no one is actively AT the register or moving to claim it.
            return false; // No active runner found to be at the register
        }

/// <summary>
    /// Attempts to add a customer to the secondary queue.
    /// Finds the first available spot based on the QueueSpotData list.
    /// </summary>
    /// <param name="customerRunner">The customer Runner trying to join.</param>
    /// <param name="assignedSpot">Output: The Transform of the assigned secondary queue spot, or null.</param>
    /// <param name="spotIndex">Output: The index of the assigned secondary queue spot, or -1.</param>
    /// <returns>True if successfully joined the secondary queue, false otherwise (e.g., queue is full).</returns>
    // --- Phase 2, Substep 2: Update TryJoinSecondaryQueue to use QueueSpot list ---
    public bool TryJoinSecondaryQueue(Game.NPC.NpcStateMachineRunner customerRunner, out Transform assignedSpot, out int spotIndex)
    {
        assignedSpot = null;
        spotIndex = -1;

        if (secondaryQueueSpots == null || secondaryQueueSpots.Count == 0) { Debug.LogWarning("CustomerManager: Cannot join secondary queue - secondaryQueueSpots list is null or empty!"); return false; }

        // Iterate through the spots to find the first available one
        foreach (var spotData in secondaryQueueSpots) // Iterate QueueSpot objects directly
        {
            // A spot is available if its currentOccupant is null
            // Phase 2, Substep 4: Simplified check - removed isSpotAssignedToMovingRunner
            if (!spotData.IsOccupied) // Check if spotData.currentOccupant == null
            {
                // Phase 2, Substep 4: Assign the occupant
                spotData.currentOccupant = customerRunner; // <-- Assign the Runner to the spot
                assignedSpot = spotData.spotTransform;
                spotIndex = spotData.spotIndex;
                Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) successfully joined secondary queue at spot {spotIndex}.");
                customerRunner.AssignedQueueSpotIndex = spotIndex; // Store the assigned index on the Runner
                // Phase 2, Substep 4: Store the queue type on the runner for FreePreviousQueueSpotOnArrival
                 customerRunner._currentQueueMoveType = QueueType.Secondary; // Set the queue type on the runner
                return true; // Successfully joined a spot
            }
        }

        Debug.Log($"CustomerManager: {customerRunner.gameObject.name} (Runner) could not join secondary queue - secondary queue is full.");
        return false; // No free spot found
    }
    // --- END Phase 2, Substep 2/4 ---


    // GetMainQueueCount implementation now uses the QueueSpot list (from Substep 2.3)
     public int GetMainQueueCount()
     {
         if (mainQueueSpots == null) return 0;
         int count = 0;
         foreach(var spotData in mainQueueSpots)
         {
              if (spotData.IsOccupied) // Count occupied spots
              {
                   count++;
              }
         }
         return count;
     }

    // IsMainQueueFull implementation now uses the QueueSpot list (from Substep 2.3)
     public bool IsMainQueueFull()
     {
          if (mainQueueSpots == null || mainQueueSpots.Count == 0) return false; // Cannot be full if no spots

          // Phase 2, Substep 3: Queue is full if the last spot is occupied
          return mainQueueSpots[mainQueueSpots.Count - 1].IsOccupied;
     }

     // --- Phase 2, Substep 6: Remove Manager's direct customerAtRegister field and related methods ---
     // The Register's state and the Runner's cached reference are now the sources of truth.
     // public void SignalCustomerAtRegister(Game.NPC.NpcStateMachineRunner customerRunner) { ... removed ... }
     // public bool IsRegisterOccupied() { ... logic moved above ... }
     // --- END Phase 2, Substep 6 ---
    }
}