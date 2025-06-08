// --- START OF FILE PrescriptionManager.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List and Dictionary
using System; // Needed for System.Serializable and Enum
using Game.Prescriptions; // Needed for PrescriptionOrder
using Game.NPC.TI; // Needed for TiNpcManager, TiNpcData
using CustomerManagement; // Needed for CustomerManager, QueueType
using Utils.Pooling; // Needed for PoolingManager
using Game.Utilities; // Needed for TimeRange (assuming this exists for schedule checks)
using Game.Events; // Needed for EventManager and new events
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState, GeneralState, PathState
using Game.Navigation; // Needed for WaypointManager, PathSO, PathTransitionDetails
using Game.NPC.Handlers; // Needed for NpcQueueHandler
using Game.NPC.States; // Needed for NpcStateSO
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Collections; // Needed for Coroutines
using System.Linq; // Needed for LINQ operations like FirstOrDefault
using Game.NPC.BasicStates;

namespace Game.Prescriptions // Place the Prescription Manager in its own namespace
{
    /// <summary>
    /// Manages the generation, assignment, and tracking of prescription orders.
    /// Also manages the Prescription Queue.
    /// </summary>
    public class PrescriptionManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static PrescriptionManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Reference to the TimeManager instance in the scene.")]
        [SerializeField] private TimeManager timeManager; // Will get via Instance if not assigned
        [Tooltip("Reference to the TiNpcManager instance in the scene.")]
        [SerializeField] private TiNpcManager tiNpcManager; // Will get via Instance if not assigned
        [Tooltip("Reference to the CustomerManager instance in the scene.")]
        [SerializeField] private CustomerManager customerManager; // Will get via Instance if not assigned
        [Tooltip("Reference to the PoolingManager instance in the scene.")]
        [SerializeField] private PoolingManager poolingManager; // Will get via Instance if not assigned
        [Tooltip("Reference to the WaypointManager instance in the scene.")]
        [SerializeField] private WaypointManager waypointManager; // Will get via Instance if not assigned


        [Header("Prescription Order Settings")]
        [Tooltip("The time range during the day when prescription orders are generated.")]
        [SerializeField] private TimeRange orderGenerationTime = new TimeRange(8, 0, 8, 5); // Example: Generate orders between 8:00 and 8:05 AM
        [Tooltip("The number of prescription orders to generate each day.")]
        [SerializeField] private int ordersToGeneratePerDay = 10;
        [Tooltip("Minimum time (real-time seconds) between generating individual orders.")]
        [SerializeField] private float minOrderGenerationInterval = 0.5f; // Added field
        [Tooltip("Maximum time (real-time seconds) between generating individual orders.")]
        [SerializeField] private float maxOrderGenerationInterval = 2f; // Added field
        [Tooltip("The maximum number of transient NPCs that can be assigned a prescription order at any given time.")]
        [SerializeField] private int maxAssignedTransientOrders = 4;

        [Header("TI Assignment Settings")]
        [Tooltip("The time range during the day when TI NPCs can be assigned pendingPrescription flags.")]
        [SerializeField] private TimeRange tiAssignmentTime = new TimeRange(9, 0, 17, 0); // Example: Assign TI orders between 9 AM and 5 PM
        [Tooltip("Minimum time between assigning pendingPrescription flags to TI NPCs.")]
        [SerializeField] private float minTiAssignmentInterval = 30f; // In seconds (real-time)
        [Tooltip("Maximum time between assigning pendingPrescription flags to TI NPCs.")]
        [SerializeField] private float maxTiAssignmentInterval = 120f; // In seconds (real-time)

        [Header("Transient Assignment Settings")]
        [Tooltip("The chance (0-1) for a transient NPC in Initializing to be assigned a prescription order if available and limits allow.")]
        [Range(0f, 1f)]
        [SerializeField] private float transientAssignmentChance = 0.25f; // 25% chance

        [Header("Navigation Points")]
        [Tooltip("Point where prescription customers will claim their prescription.")]
        [SerializeField] private Transform prescriptionClaimPoint;
        [Tooltip("Points where prescription customers will form a queue, ordered from closest to furthest.")]
        [SerializeField] private List<Transform> prescriptionQueuePoints;


        // --- Internal Data ---
        private List<PrescriptionOrder> allOrdersGeneratedToday = new List<PrescriptionOrder>();
        private List<PrescriptionOrder> unassignedOrders = new List<PrescriptionOrder>();
        // We need to track assigned orders, potentially linking them back to the NPC (TI Data or Runner)
        // A Dictionary might be better here: NPC -> Order
        private Dictionary<string, PrescriptionOrder> assignedTiOrders = new Dictionary<string, PrescriptionOrder>(); // TI ID -> Order
        // For transient, we might track them by Runner instance or GameObject
        private Dictionary<GameObject, PrescriptionOrder> assignedTransientOrders = new Dictionary<GameObject, PrescriptionOrder>(); // Transient GO -> Order

        // --- Prescription Queue Data ---
        private List<QueueSpot> prescriptionQueueSpots; // List of QueueSpot objects for the prescription queue

        // --- Timers and Flags ---
        private bool ordersGeneratedToday = false;
        private float tiAssignmentTimer = 0f; // Timer for timed TI assignments

        // Coroutine references
        private Coroutine tiAssignmentCoroutine;
        private Coroutine orderGenerationCoroutine; // Added field

        // --- Runtime tracking for active prescription claim spot --- // <-- NEW TRACKING
        private GameObject currentClaimSpotOccupant = null; // Track the GameObject currently at the claim spot
        // --- END NEW ---


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
                Debug.LogWarning("PrescriptionManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Initialize Prescription QueueSpot list from Transform list
            prescriptionQueueSpots = new List<QueueSpot>();
            if (prescriptionQueuePoints == null || prescriptionQueuePoints.Count == 0)
            {
                Debug.LogWarning("PrescriptionManager: No prescription queue points assigned! Prescription queue system will not function.", this);
            }
            else
            {
                for (int i = 0; i < prescriptionQueuePoints.Count; i++)
                {
                    if (prescriptionQueuePoints[i] != null)
                    {
                        // Using QueueType.Prescription now that it's added
                        prescriptionQueueSpots.Add(new QueueSpot(prescriptionQueuePoints[i], i, QueueType.Prescription)); // <-- Using QueueType.Prescription
                    }
                    else
                    {
                        Debug.LogWarning($"PrescriptionManager: Prescription queue point at index {i} is null!", this);
                    }
                }
                Debug.Log($"PrescriptionManager: Initialized prescription queue with {prescriptionQueueSpots.Count} spots.");
            }


            Debug.Log("PrescriptionManager: Awake completed.");
        }

        private void Start()
        {
            // Get references to other singletons
            timeManager = TimeManager.Instance;
            if (timeManager == null) Debug.LogError("PrescriptionManager: TimeManager instance not found!");

            tiNpcManager = TiNpcManager.Instance;
            if (tiNpcManager == null) Debug.LogError("PrescriptionManager: TiNpcManager instance not found!");

            customerManager = CustomerManager.Instance;
            if (customerManager == null) Debug.LogError("PrescriptionManager: CustomerManager instance not found!");

            poolingManager = PoolingManager.Instance;
            if (poolingManager == null) Debug.LogError("PrescriptionManager: PoolingManager instance not found!");

            waypointManager = WaypointManager.Instance;
            if (waypointManager == null) Debug.LogError("PrescriptionManager: WaypointManager instance not found!");


            // Subscribe to TimeManager events if available
            if (timeManager != null)
            {
                // Assuming TimeManager has an OnSunset event to signal day end
                timeManager.OnSunset += HandleSunset; // <-- SUBSCRIBED TO ON SUNSET
                // Assuming TimeManager has an OnSunrise event to signal day start
                timeManager.OnSunrise += HandleSunrise; // <-- SUBSCRIBED TO ON SUNRISE
            }

            // Subscribe to new prescription events
            EventManager.Subscribe<ClaimPrescriptionSpotEvent>(HandleClaimPrescriptionSpot); // <-- NEW Subscription
            EventManager.Subscribe<FreePrescriptionClaimSpotEvent>(HandleFreePrescriptionClaimSpot); // <-- NEW Subscription
            EventManager.Subscribe<QueueSpotFreedEvent>(HandlePrescriptionQueueSpotFreed); // <-- NEW Subscription (for prescription queue)


            // Start the coroutine for timed TI assignments - REMOVED FROM START()
            // tiAssignmentCoroutine = StartCoroutine(TimedTIAssignmentRoutine());

            Debug.Log("PrescriptionManager: Start completed. Manager references acquired.");
        }

        private void Update()
        {
            // Check if orders need to be generated based on time
            if (!ordersGeneratedToday && timeManager != null && timeManager.CurrentGameTime != DateTime.MinValue)
            {
                DateTime currentTime = timeManager.CurrentGameTime;
                // Check if the current time is within the generation range AND we haven't generated today
                if (orderGenerationTime.IsWithinRange(currentTime)) // Assuming TimeRange has IsWithinRange
                {
                    // Time is within the generation window and we haven't generated yet today
                    StartOrderGenerationRoutine(); // Start the timed generation coroutine
                    ordersGeneratedToday = true; // Set flag immediately to prevent starting multiple coroutines
                }
            }
        }

        private void OnEnable()
        {
             // Subscribe to events if needed (e.g., NPC Initializing event if not checking in Update)
             // EventManager.Subscribe<NpcInitializingEvent>(HandleNpcInitializing); // Placeholder
             if (timeManager != null)
             {
                 timeManager.OnSunset += HandleSunset; // <-- SUBSCRIBED TO ON SUNSET
                 timeManager.OnSunrise += HandleSunrise; // <-- SUBSCRIBED TO ON SUNRISE
             }
             // Subscribe to new prescription events
             EventManager.Subscribe<ClaimPrescriptionSpotEvent>(HandleClaimPrescriptionSpot); // <-- NEW Subscription
             EventManager.Subscribe<FreePrescriptionClaimSpotEvent>(HandleFreePrescriptionClaimSpot); // <-- NEW Subscription
             EventManager.Subscribe<QueueSpotFreedEvent>(HandlePrescriptionQueueSpotFreed); // <-- NEW Subscription (for prescription queue)

             // Restart TI assignment coroutine ONLY IF it was running and stopped by OnDisable
             // It's normally started by HandleSunrise now.
             // if (tiAssignmentCoroutine == null)
             // {
             //     tiAssignmentCoroutine = StartCoroutine(TimedTIAssignmentRoutine());
             // }
             if (orderGenerationCoroutine == null && ordersToGeneratePerDay > 0 && !ordersGeneratedToday && timeManager != null && timeManager.CurrentGameTime != DateTime.MinValue && orderGenerationTime.IsWithinRange(timeManager.CurrentGameTime))
             {
                 // Only restart generation if it was supposed to be running based on time/flags
                 StartOrderGenerationRoutine();
             }
        }

        private void OnDisable()
        {
             // Unsubscribe from events
             // EventManager.Unsubscribe<NpcInitializingEvent>(HandleNpcInitializing); // Placeholder
             if (timeManager != null)
             {
                 timeManager.OnSunset -= HandleSunset; // <-- UNSUBSCRIBED FROM ON SUNSET
                 timeManager.OnSunrise -= HandleSunrise; // <-- UNSUBSCRIBED FROM ON SUNRISE
             }
             // Unsubscribe from new prescription events
             EventManager.Unsubscribe<ClaimPrescriptionSpotEvent>(HandleClaimPrescriptionSpot); // <-- NEW Unsubscription
             EventManager.Unsubscribe<FreePrescriptionClaimSpotEvent>(HandleFreePrescriptionClaimSpot); // <-- NEW Unsubscription
             EventManager.Unsubscribe<QueueSpotFreedEvent>(HandlePrescriptionQueueSpotFreed); // <-- NEW Unsubscription (for prescription queue)

             if (tiAssignmentCoroutine != null)
             {
                  StopCoroutine(tiAssignmentCoroutine);
                  tiAssignmentCoroutine = null;
             }
             if (orderGenerationCoroutine != null) // Stop generation coroutine on disable
             {
                  StopCoroutine(orderGenerationCoroutine);
                  orderGenerationCoroutine = null;
             }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // Clean up lists
                allOrdersGeneratedToday.Clear();
                unassignedOrders.Clear();
                assignedTiOrders.Clear();
                assignedTransientOrders.Clear();
                prescriptionQueueSpots.Clear();
                currentClaimSpotOccupant = null; // Clear occupant reference
            }
            Debug.Log("PrescriptionManager: OnDestroy completed.");
        }

        // --- Order Generation Logic (Substep 1.3 Implementation) ---

        /// <summary>
        /// Handles the OnSunset event from TimeManager. Resets daily order data.
        /// </summary>
        private void HandleSunset() // <-- RENAMED HANDLER
        {
            Debug.Log("PrescriptionManager: Received OnSunset event. Resetting daily order data.");
            allOrdersGeneratedToday.Clear();
            unassignedOrders.Clear();
            assignedTiOrders.Clear(); // <-- Clear assigned orders at end of day
            assignedTransientOrders.Clear(); // <-- Clear assigned orders at end of day
            // Note: NPCs currently holding orders will have their flags/references cleared
            // when they exit the prescription flow or are deactivated/pooled.
            // Clearing the manager's dictionaries here ensures they aren't tracked across days.

            ordersGeneratedToday = false; // Allow generation again on the *next* day

            // Optional: Clear prescription queue occupants at end of day?
            // This might be better handled by the NPCs themselves exiting the queue state.
            // For now, let's let the standard queue freeing logic handle it.
        }

        /// <summary>
        /// Handles the OnSunrise event from TimeManager. Starts the TI assignment routine.
        /// </summary>
        private void HandleSunrise() // <-- NEW HANDLER FOR STARTING ASSIGNMENT
        {
            Debug.Log("PrescriptionManager: Received OnSunrise event. Starting TI assignment routine.");
            // Start the coroutine for timed TI assignments
             if (tiAssignmentCoroutine != null)
             {
                  StopCoroutine(tiAssignmentCoroutine); // Stop if somehow already running
             }
            tiAssignmentCoroutine = StartCoroutine(TimedTIAssignmentRoutine());
        }


        /// <summary>
        /// Starts the coroutine for staggered order generation. Stops any existing generation routine.
        /// </summary>
        private void StartOrderGenerationRoutine()
        {
            if (orderGenerationCoroutine != null)
            {
                StopCoroutine(orderGenerationCoroutine);
            }
            orderGenerationCoroutine = StartCoroutine(GenerateOrdersRoutine());
            Debug.Log("PrescriptionManager: Starting order generation routine.");
        }

        /// <summary>
        /// Coroutine that generates prescription orders one by one with a delay.
        /// </summary>
        private IEnumerator GenerateOrdersRoutine()
        {
            Debug.Log($"PrescriptionManager: GenerateOrdersRoutine started. Will generate {ordersToGeneratePerDay} orders.");
            // Lists are cleared on Sunset. Clear them here too for robustness if generation runs multiple times unexpectedly.
            allOrdersGeneratedToday.Clear();
            unassignedOrders.Clear();
            // Assigned orders are NOT cleared here, they persist across generation runs within the same day.

            for (int i = 0; i < ordersToGeneratePerDay; i++)
            {
                // --- Placeholder: Generate a single dummy order ---
                // In a real scenario, this would involve more complex logic,
                // potentially pulling from a list of possible drugs, generating
                // realistic doses/lengths, and linking to actual TI NPC IDs.
                string patientName = $"Patient_{i + 1}"; // Simple dummy name
                string prescribedDrug = $"Drug_{Random.Range(1, 10)}"; // Simple dummy drug
                int dose = Random.Range(1, 4); // 1-3 times a day
                int length = Random.Range(3, 30); // Increased length range for more persistence
                bool illegal = Random.value < 0.1f; // 10% chance of being illegal

                PrescriptionOrder newOrder = new PrescriptionOrder(patientName, prescribedDrug, dose, length, illegal);
                // --- End Placeholder ---

                allOrdersGeneratedToday.Add(newOrder);
                unassignedOrders.Add(newOrder);
                Debug.Log($"PrescriptionManager: Generated order {i + 1}/{ordersToGeneratePerDay}: {newOrder}");

                // Wait before generating the next order
                yield return new WaitForSeconds(Random.Range(minOrderGenerationInterval, maxOrderGenerationInterval));
            }

            Debug.Log($"PrescriptionManager: Order generation routine finished. {allOrdersGeneratedToday.Count} orders generated.");
            orderGenerationCoroutine = null; // Clear coroutine reference
        }

        // --- END Order Generation Logic ---


        // --- TI Assignment Logic (Substep 1.5 Implementation) ---

        /// <summary>
        /// Coroutine for timed assignment of pendingPrescription flags to TI NPCs.
        /// Attempts assignment immediately on start, then waits for intervals.
        /// </summary>
        private IEnumerator TimedTIAssignmentRoutine() // <-- MODIFIED LOGIC
        {
             Debug.Log("PrescriptionManager: Timed TI Assignment Routine started.");

             // --- Attempt assignment immediately on start ---
             AttemptTIAssignment(); // Call the helper method

             // --- Set the timer for the *next* assignment ---
             tiAssignmentTimer = Random.Range(minTiAssignmentInterval, maxTiAssignmentInterval);
             Debug.Log($"PrescriptionManager: Next TI assignment attempt in {tiAssignmentTimer:F2}s.");

             // --- Loop for Subsequent Assignments ---
             while(true)
             {
                  // Wait for the timer to elapse
                  while (tiAssignmentTimer > 0)
                  {
                      // Only decrement timer if within the assignment time window
                      if (timeManager != null && timeManager.CurrentGameTime != DateTime.MinValue && tiAssignmentTime.IsWithinRange(timeManager.CurrentGameTime))
                      {
                          tiAssignmentTimer -= Time.deltaTime;
                      }
                      yield return null; // Wait a frame
                  }

                  // Timer has elapsed, attempt another assignment
                  Debug.Log("PrescriptionManager: TI Assignment timer elapsed. Attempting to assign another order.");
                  AttemptTIAssignment(); // Call the helper method again

                  // Set the timer for the *next* assignment
                  tiAssignmentTimer = Random.Range(minTiAssignmentInterval, maxTiAssignmentInterval);
                  Debug.Log($"PrescriptionManager: Next TI assignment attempt in {tiAssignmentTimer:F2}s.");
             }
        }
        
        /// <summary>
        /// Helper method to attempt assigning a TI prescription order.
        /// </summary>
        private void AttemptTIAssignment() // <-- NEW HELPER METHOD
        {
            if (timeManager == null || tiNpcManager == null)
            {
                Debug.LogWarning("PrescriptionManager: Cannot attempt TI assignment, TimeManager or TiNpcManager is null.");
                return;
            }

            DateTime currentTime = timeManager.CurrentGameTime;

            // Only attempt assignment if within the assignment window
            if (!tiAssignmentTime.IsWithinRange(currentTime))
            {
                // Debug.Log("PrescriptionManager: Outside TI Assignment window. Skipping assignment attempt."); // Too noisy
                return;
            }

            // Find the first unassigned order that matches a TI NPC
            // Use ToList() to avoid modifying the collection while iterating
            PrescriptionOrder? orderToAssign = null;
            TiNpcData targetTi = null;

            foreach (var order in unassignedOrders.ToList())
            {
                TiNpcData tiData = tiNpcManager.GetTiNpcData(order.patientName);
                if (tiData != null)
                {
                    // Found a TI NPC matching the patient name
                    if (!tiData.pendingPrescription) // Check if they don't already have a pending prescription
                    {
                        orderToAssign = order;
                        targetTi = tiData;
                        break; // Found a suitable order/NPC, stop searching
                    }
                }
            }

            if (orderToAssign.HasValue && targetTi != null)
            {
                // Assign the order
                targetTi.pendingPrescription = true;
                targetTi.assignedOrder = orderToAssign.Value; // Assign the struct value

                // Move the order from unassigned to assignedTiOrders
                unassignedOrders.Remove(orderToAssign.Value); // Remove from unassigned
                assignedTiOrders[targetTi.Id] = orderToAssign.Value; // Add to assigned TI

                Debug.Log($"PrescriptionManager: Assigned prescription order for '{orderToAssign.Value.patientName}' to TI NPC '{targetTi.Id}'. {unassignedOrders.Count} unassigned orders remaining.");
            }
            else
            {
                // No suitable order/NPC found in this attempt
                // Debug.Log("PrescriptionManager: No unassigned orders matching a TI NPC without a pending prescription found for assignment."); // Too noisy
            }
        }

        // --- END TI Assignment Logic ---


        // --- Transient Assignment Logic (Substep 1.6 Implementation) ---

        /// <summary>
        /// Attempts to assign a non-TI prescription order to a transient NPC.
        /// Called by NpcStateMachineRunner during Initialization.
        /// </summary>
        /// <param name="runner">The runner of the transient NPC.</param>
        /// <returns>True if an order was assigned, false otherwise.</returns>
        public bool TryAssignTransientPrescription(NpcStateMachineRunner runner)
        {
            if (runner == null || tiNpcManager == null)
            {
                Debug.LogError("PrescriptionManager: TryAssignTransientPrescription called with null runner or TiNpcManager!");
                return false;
            }

            // Check if the transient NPC already has a pending prescription (shouldn't happen, but defensive)
            if (runner.hasPendingPrescriptionTransient)
            {
                Debug.LogWarning($"PrescriptionManager: Transient NPC '{runner.gameObject.name}' already has a pending prescription. Skipping assignment attempt.");
                return false;
            }

            // Check if we have unassigned orders that are NOT for a TI NPC
            // And check if we are below the maximum assigned transient orders limit
            PrescriptionOrder? orderToAssign = null;
            if (assignedTransientOrders.Count < maxAssignedTransientOrders)
            {
                // Find the first unassigned order that does NOT match a TI NPC
                // Use ToList() to avoid modifying the collection while iterating
                orderToAssign = unassignedOrders.ToList().FirstOrDefault(order => tiNpcManager.GetTiNpcData(order.patientName) == null);
            }


            if (orderToAssign.HasValue)
            {
                // Found a suitable order, now check the random chance
                if (Random.value < transientAssignmentChance)
                {
                    // Check if the prescription queue is full. If so, we cannot assign.
                    // This check is based on the vision: "if the prescription queue is currently full, then the prescriptionmanager will not flag any new transient npcs."
                    if (IsPrescriptionQueueFull()) // Implemented in this substep
                    {
                        Debug.Log($"PrescriptionManager: Prescription queue is full. Cannot assign transient order to '{runner.gameObject.name}'.");
                        return false; // Cannot assign if queue is full
                    }

                    // All checks passed, assign the order
                    runner.hasPendingPrescriptionTransient = true;
                    runner.assignedOrderTransient = orderToAssign.Value; // Assign the struct value

                    // Move the order from unassigned to assignedTransientOrders
                    unassignedOrders.Remove(orderToAssign.Value); // Remove from unassigned
                    assignedTransientOrders[runner.gameObject] = orderToAssign.Value; // Add to assigned transient

                    Debug.Log($"PrescriptionManager: Assigned transient prescription order for '{orderToAssign.Value.patientName}' to NPC '{runner.gameObject.name}'. {unassignedOrders.Count} unassigned orders remaining. {assignedTransientOrders.Count}/{maxAssignedTransientOrders} transient orders assigned.");

                    return true; // Successfully assigned
                }
                else
                {
                    // Random chance failed
                    // Debug.Log($"PrescriptionManager: Random chance failed for transient assignment to '{runner.gameObject.name}'."); // Too noisy
                    return false;
                }
            }
            else
            {
                // No suitable unassigned order found or limit reached
                // Debug.Log($"PrescriptionManager: No suitable unassigned non-TI orders found or transient assignment limit ({maxAssignedTransientOrders}) reached ({assignedTransientOrders.Count})."); // Too noisy
                return false;
            }
        }

        // --- END Transient Assignment Logic ---


        // --- Prescription Queue & Spot Management (Phase 4 Implementation) --- // <-- NEW HEADER

        /// <summary>
        /// Handles the ClaimPrescriptionSpotEvent. Marks the claim spot as occupied.
        /// </summary>
        private void HandleClaimPrescriptionSpot(ClaimPrescriptionSpotEvent eventArgs)
        {
             if (eventArgs.NpcObject == null) return;

             if (currentClaimSpotOccupant != null && currentClaimSpotOccupant != eventArgs.NpcObject)
             {
                  Debug.LogWarning($"PrescriptionManager: ClaimPrescriptionSpotEvent received for '{eventArgs.NpcObject.name}', but spot is already occupied by '{currentClaimSpotOccupant.name}'! Forcing occupant change.", eventArgs.NpcObject);
                  // Decide how to handle this conflict - for now, just overwrite
             }
             currentClaimSpotOccupant = eventArgs.NpcObject;
             Debug.Log($"PrescriptionManager: Prescription claim spot is now occupied by '{currentClaimSpotOccupant.name}'.", currentClaimSpotOccupant);

             // Check if anyone is waiting in the queue and can move up? No, that happens when the spot is FREED.
        }

        /// <summary>
        /// Handles the FreePrescriptionClaimSpotEvent. Marks the claim spot as free and signals the queue.
        /// </summary>
        private void HandleFreePrescriptionClaimSpot(FreePrescriptionClaimSpotEvent eventArgs)
        {
             if (eventArgs.NpcObject == null) return;

             if (currentClaimSpotOccupant == eventArgs.NpcObject)
             {
                  Debug.Log($"PrescriptionManager: FreePrescriptionClaimSpotEvent received for '{eventArgs.NpcObject.name}'. Spot is now free.", eventArgs.NpcObject);
                  currentClaimSpotOccupant = null;

                  // Now that the claim spot is free, check if anyone is waiting in the prescription queue (spot 0)
                  // and signal them to move to the claim spot.
                  SignalNextPrescriptionCustomerToClaimSpot(); // Need to implement this
             }
             else if (currentClaimSpotOccupant != null)
             {
                  Debug.LogWarning($"PrescriptionManager: FreePrescriptionClaimSpotEvent received for '{eventArgs.NpcObject.name}', but spot is occupied by '{currentClaimSpotOccupant.name}'! Inconsistency.", eventArgs.NpcObject);
                  // Inconsistency - the wrong NPC is trying to free the spot.
                  // Decide error handling - maybe force free if the event NPC is valid?
                  // For now, just log warning.
             }
             else
             {
                  Debug.LogWarning($"PrescriptionManager: FreePrescriptionClaimSpotEvent received for '{eventArgs.NpcObject.name}', but spot was already free. Duplicate event?", eventArgs.NpcObject);
             }
        }

        /// <summary>
        /// Signals the NPC at Prescription Queue spot 0 to move to the claim spot.
        /// Called when the claim spot becomes free.
        /// </summary>
        private void SignalNextPrescriptionCustomerToClaimSpot()
        {
             if (prescriptionQueueSpots == null || prescriptionQueueSpots.Count == 0)
             {
                  Debug.Log("PrescriptionManager: No prescription queue spots available to signal next customer.");
                  return;
             }

             QueueSpot spotZero = prescriptionQueueSpots[0];

             if (spotZero.IsOccupied)
             {
                  NpcStateMachineRunner runnerAtSpot0 = spotZero.currentOccupant;

                  // Robustness check for valid Runner reference
                  if (runnerAtSpot0 == null || !runnerAtSpot0.gameObject.activeInHierarchy || runnerAtSpot0.GetCurrentState() == null || !runnerAtSpot0.GetCurrentState().HandledState.Equals(CustomerState.PrescriptionQueue))
                  {
                       Debug.LogError($"PrescriptionManager: Inconsistency detected! Prescription Queue spot 0 is marked occupied by a Runner ('{runnerAtSpot0?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or not in the PrescriptionQueue state ('{runnerAtSpot0?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot 0 free.", this);
                       spotZero.currentOccupant = null; // Force free this inconsistent spot
                       HandlePrescriptionQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Prescription, 0)); // Trigger cascade manually from spot 0
                  }
                  else
                  {
                       // Clear spot 0's occupant reference immediately
                       spotZero.currentOccupant = null; // <-- Clear spot 0's occupant

                       // Signal the Runner to go to the claim spot
                       Debug.Log($"PrescriptionManager: Found {runnerAtSpot0.gameObject.name} occupying Prescription Queue spot 0. Clearing spot 0 and Signalling them to move to claim spot.");
                       if (runnerAtSpot0.QueueHandler != null)
                       {
                            // Need a method on QueueHandler to signal move to claim spot
                            // Let's add GoToPrescriptionClaimSpotFromQueue() to NpcQueueHandler
                            runnerAtSpot0.QueueHandler.GoToPrescriptionClaimSpotFromQueue(); // Implemented in Substep 4.3
                       }
                       else
                       {
                            Debug.LogError($"PrescriptionManager: Runner '{runnerAtSpot0.gameObject.name}' is missing its NpcQueueHandler component! Cannot signal move to claim spot.", runnerAtSpot0.gameObject);
                            // This NPC is likely stuck.
                       }
                  }
             }
             else
             {
                  Debug.Log("PrescriptionManager: FreePrescriptionClaimSpotEvent received, but Prescription Queue spot 0 is not occupied.");
                  if (prescriptionQueueSpots.Count > 0)
                  {
                       Debug.LogWarning($"PrescriptionManager: Prescription Queue spot 0 is unexpectedly free. Manually triggering cascade from spot 1 just in case.", this);
                       HandlePrescriptionQueueSpotFreed(new QueueSpotFreedEvent(QueueType.Prescription, 0)); // Trigger cascade from spot 1
                  }
             }
        }


        /// <summary>
        /// Handles the QueueSpotFreedEvent specifically for the Prescription Queue.
        /// Signals that an NPC is leaving a specific prescription queue spot.
        /// This method is called by the OnExit of the PrescriptionQueueStateSO.
        /// It starts the cascade of move-up commands *from* the spot that was freed.
        /// </summary>
        /// <param name="eventArgs">The event arguments containing the queue type and spot index that published the event.</param>
        private void HandlePrescriptionQueueSpotFreed(QueueSpotFreedEvent eventArgs)
        {
             // Only process events for the Prescription Queue
             if (eventArgs.Type != QueueType.Prescription) return;

             int spotIndex = eventArgs.SpotIndex; // The index that was just vacated

             if (spotIndex < 0)
             {
                  Debug.LogWarning($"PrescriptionManager: Received QueueSpotFreedEvent with invalid negative spot index {spotIndex} for Prescription queue. Ignoring.", this);
                  return;
             }

             Debug.Log($"PrescriptionManager: Handling QueueSpotFreedEvent for spot {spotIndex} in Prescription queue (triggered by State Exit). Initiating cascade from spot {spotIndex + 1}.");

             List<QueueSpot> targetQueue = prescriptionQueueSpots; // Target is always the prescription queue

             if (targetQueue == null || spotIndex >= targetQueue.Count)
             {
                  Debug.LogWarning($"PrescriptionManager: Received invalid QueueSpotFreedEvent args (index {spotIndex}, type {eventArgs.Type}) or null target queue. Ignoring.", this);
                  return;
             }

             // The freeing of spotIndex itself should have happened just before this event fired (for spot 0 exiting to Claim Spot)
             // or happens when the moving NPC arrived at the *next* spot via FreePreviousPrescriptionQueueSpotOnArrival.
             QueueSpot spotThatPublished = targetQueue[spotIndex]; // Get the spot data corresponding to the event source
             if (spotThatPublished.IsOccupied)
             {
                  // This is an inconsistency! The spot that published the "I'm leaving" event is STILL marked occupied.
                  Debug.LogError($"PrescriptionManager: Inconsistency detected! QueueSpotFreedEvent received for spot {spotIndex} in Prescription queue, but the spot is still marked occupied by {spotThatPublished.currentOccupant.gameObject.name} (Runner). Forcing spot free.", this);
                  spotThatPublished.currentOccupant = null; // Force clear the occupant reference to fix the data
             }
             else
             {
                  Debug.Log($"PrescriptionManager: QueueSpotFreedEvent received for spot {spotIndex} in Prescription queue, spot is correctly marked free.");
             }


             // Initiate the cascade of "move up" commands
             for (int currentSpotIndex = spotIndex + 1; currentSpotIndex < targetQueue.Count; currentSpotIndex++)
             {
                  QueueSpot currentSpotData = targetQueue[currentSpotIndex];

                  if (currentSpotData.IsOccupied)
                  {
                       Game.NPC.NpcStateMachineRunner runnerToMove = currentSpotData.currentOccupant;

                       // Robustness check for valid Runner reference
                       if (runnerToMove == null || !runnerToMove.gameObject.activeInHierarchy || runnerToMove.GetCurrentState() == null || !runnerToMove.GetCurrentState().HandledState.Equals(CustomerState.PrescriptionQueue))
                       {
                            Debug.LogError($"PrescriptionManager: Inconsistency detected! Spot {currentSpotIndex} in Prescription queue is marked occupied by a Runner ('{runnerToMove?.gameObject.name ?? "NULL Runner"}') that is invalid, inactive, or in wrong state ('{runnerToMove?.GetCurrentState()?.name ?? "NULL State"}'). Forcing spot {currentSpotIndex} free and continuing cascade search.", this);
                            currentSpotData.currentOccupant = null;
                            continue;
                       }

                       int nextSpotIndex = currentSpotIndex - 1;
                       QueueSpot nextSpotData = targetQueue[nextSpotIndex];

                       Debug.Log($"PrescriptionManager: Signalling {runnerToMove.gameObject.name} assigned to spot {currentSpotIndex} to move up to spot {nextSpotIndex} in Prescription queue.");

                       // Set the destination spot's occupant BEFORE calling MoveToQueueSpot
                       nextSpotData.currentOccupant = runnerToMove;

                       // Call the method on the Runner's QueueHandler to initiate the move.
                       if (runnerToMove.QueueHandler != null)
                       {
                            // Need to modify NpcQueueHandler.MoveToQueueSpot to accept QueueType.Prescription
                            runnerToMove.QueueHandler.MoveToQueueSpot(nextSpotData.spotTransform, nextSpotIndex, QueueType.Prescription); // Use QueueType.Prescription
                       }
                       else
                       {
                            Debug.LogError($"PrescriptionManager: Runner '{runnerToMove.gameObject.name}' is missing its NpcQueueHandler component! Cannot signal move up.", runnerToMove.gameObject);
                            // This spot is now incorrectly marked as occupied by runnerToMove, and the previous spot might not be freed.
                            // It's a significant inconsistency. Forcing the spot to free to unblock the queue,
                            // but the NPC is likely stuck.
                            nextSpotData.currentOccupant = null; // Unmark the destination spot
                       }
                  }
                  else // No occupant found for this spot index
                  {
                       Debug.LogWarning($"PrescriptionManager: No Runner found occupying spot {currentSpotIndex} in Prescription queue. This spot is a gap. Continuing cascade search.", this);
                  }
             } // End of cascade loop
        }


        /// <summary>
        /// Attempts to add a customer to the prescription queue.
        /// Finds the first available spot based on the QueueSpotData list.
        /// </summary>
        /// <param name="runner">The customer Runner trying to join.</param>
        /// <param name="assignedSpot">Output: The Transform of the assigned queue spot, or null.</param>
        /// <param name="spotIndex">Output: The index of the assigned queue spot, or -1.</param>
        /// <returns>True if successfully joined the queue, false otherwise (e.g., queue is full).</returns>
        public bool TryJoinPrescriptionQueue(NpcStateMachineRunner runner, out Transform assignedSpot, out int spotIndex)
        {
            assignedSpot = null;
            spotIndex = -1;

            if (runner == null) { Debug.LogError("PrescriptionManager: TryJoinPrescriptionQueue called with null runner!"); return false; }
            if (prescriptionQueueSpots == null || prescriptionQueueSpots.Count == 0) { Debug.LogWarning("PrescriptionManager: Cannot join prescription queue - prescriptionQueueSpots list is null or empty!"); return false; }

            foreach (var spotData in prescriptionQueueSpots) // Iterate QueueSpot objects directly
            {
                if (!spotData.IsOccupied) // Check if spotData.currentOccupant == null
                {
                    spotData.currentOccupant = runner; // <-- Assign the Runner to the spot in Manager's data
                    assignedSpot = spotData.spotTransform;
                    spotIndex = spotData.spotIndex;
                    Debug.Log($"PrescriptionManager: {runner.gameObject.name} (Runner) successfully joined prescription queue at spot {spotIndex}.");
                    Debug.Log($"[DEBUG {runner.gameObject.name}] PrescriptionManager.TryJoinPQ: Attempting to call runner.QueueHandler.ReceiveQueueAssignment. runner.QueueHandler is null: {runner.QueueHandler == null}", runner.gameObject);

                    // Call the public method on the QueueHandler to receive the assignment
                    if (runner.QueueHandler != null)
                    {
                        runner.QueueHandler.ReceiveQueueAssignment(spotIndex, QueueType.Prescription); // Use QueueType.Prescription
                    }
                    else
                    {
                        Debug.LogError($"PrescriptionManager: Runner '{runner.gameObject.name}' is missing its NpcQueueHandler component! Cannot assign prescription queue spot.", runner.gameObject);
                        // Revert the spot assignment in manager's data if we can't tell the handler
                        spotData.currentOccupant = null;
                        return false; // Signal failure
                    }

                    return true; // Success
                }
            }

            Debug.Log($"PrescriptionManager: {runner.gameObject.name} (Runner) could not join prescription queue - prescription queue is full.");
            return false;
        }

        /// <summary>
        /// Checks if the prescription queue is currently full.
        /// </summary>
        public bool IsPrescriptionQueueFull()
        {
             if (prescriptionQueueSpots == null || prescriptionQueueSpots.Count == 0) return false;

             // The prescription queue is considered "full" if the very last spot has an occupant.
             return prescriptionQueueSpots[prescriptionQueueSpots.Count - 1].IsOccupied;
        }

         /// <summary>
         /// Checks if the prescription claim spot is occupied.
         /// </summary>
         public bool IsPrescriptionClaimSpotOccupied()
         {
              // Check the runtime tracking field
              return currentClaimSpotOccupant != null;
         }

         /// <summary>
         /// Called by an NpcQueueHandler when an NPC starts moving away from a prescription queue spot.
         /// </summary>
         public bool FreePreviousPrescriptionQueueSpotOnArrival(QueueType queueType, int previousSpotIndex)
         {
              // Only process events for the Prescription Queue
             if (queueType != QueueType.Prescription)
             {
                  Debug.LogWarning($"PrescriptionManager: Received FreePreviousQueueSpotOnArrival for incorrect queue type {queueType}. Ignoring.", this);
                  return false;
             }

              Debug.Log($"PrescriptionManager: Handling FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in Prescription queue (triggered by Runner Starting Move).");

              List<QueueSpot> targetQueue = prescriptionQueueSpots; // Target is always the prescription queue
              string queueName = "Prescription";

              // Validate the previous spot index
              if (targetQueue == null || previousSpotIndex < 0 || previousSpotIndex >= targetQueue.Count)
              {
                  Debug.LogWarning($"PrescriptionManager: Received FreePreviousQueueSpotOnArrival with invalid spot index {previousSpotIndex} for {queueName} queue. Ignoring.", this);
                  return false;
              }

              // Mark the previous spot as free in the QueueSpot data
              QueueSpot spotToFree = targetQueue[previousSpotIndex];

              if (spotToFree.IsOccupied) // Check if it's occupied before freeing (defensive)
              {
                  spotToFree.currentOccupant = null; // <-- Mark the spot as free when the Runner starts moving away
                  Debug.Log($"PrescriptionManager: Spot {previousSpotIndex} in {queueName} queue is now marked free (clearing occupant reference on Runner starting move).");
                  return true;
              }
              else
              {
                  Debug.LogWarning($"PrescriptionManager: Received FreePreviousQueueSpotOnArrival for spot {previousSpotIndex} in {queueName} queue, but it was already marked as free. Inconsistency?", this);
                  return true; // Return true even if already free, as the intent was achieved.
              }
         }

         /// <summary>
         /// Gets the Transform for a specific prescription queue point.
         /// </summary>
         /// <param name="index">The index of the desired queue point.</param>
         /// <returns>The Transform of the queue point, or null if index is out of bounds.</returns>
         public Transform GetPrescriptionQueuePoint(int index)
         {
              if (prescriptionQueueSpots != null && index >= 0 && index < prescriptionQueueSpots.Count)
              {
                   return prescriptionQueueSpots[index].spotTransform;
              }
              Debug.LogWarning($"PrescriptionManager: Requested prescription queue point index {index} is out of bounds or prescriptionQueueSpots list is null!");
              return null;
         }


         /// <summary>
         /// Called by TiNpcManager to start the pendingPrescription suppression coroutine.
         /// </summary>
         public void StartPrescriptionSuppressionCoroutine(TiNpcData data, float duration)
         {
              if (data == null) return;
              Debug.Log($"PrescriptionManager: Starting prescription suppression for {data.Id} for {duration}s.", data.NpcGameObject);
              // Need to manage this coroutine. Store a dictionary of active suppression coroutines?
              // For now, let's just start it directly. If multiple suppressions happen, they might overwrite.
              // A more robust system would track suppression per NPC.
              StartCoroutine(PrescriptionSuppressionRoutine(data, duration));
         }

         /// <summary>
         /// Coroutine to temporarily suppress a TI NPC's pendingPrescription flag.
         /// </summary>
         private IEnumerator PrescriptionSuppressionRoutine(TiNpcData data, float duration)
         {
              // The flag is set to false in PrescriptionEnteringSO.OnEnter
              yield return new WaitForSeconds(duration);

              if (data != null)
              {
                   Debug.Log($"PrescriptionManager: Prescription suppression ended for {data.Id}. Re-activating pendingPrescription flag.", data.NpcGameObject);
                   data.pendingPrescription = true;
                   // Note: The assignedOrder is NOT cleared here, they still have the order, just couldn't get it right now.
              }
         }

         /// <summary>
         /// Removes a TI NPC's assigned order from the manager's tracking dictionary.
         /// Called when the TI NPC successfully completes the prescription flow.
         /// </summary>
         public void RemoveAssignedTiOrder(string tiId) // <-- NEW METHOD Implementation
         {
              if (assignedTiOrders.ContainsKey(tiId))
              {
                   Debug.Log($"PrescriptionManager: Removing assigned TI order for '{tiId}' from tracking.");
                   assignedTiOrders.Remove(tiId);
              } else {
                   Debug.LogWarning($"PrescriptionManager: Attempted to remove assigned TI order for '{tiId}' but it was not found in the tracking dictionary.");
              }
         }

         /// <summary>
         /// Removes a Transient NPC's assigned order from the manager's tracking dictionary.
         /// Called when the Transient NPC successfully completes the prescription flow.
         /// </summary>
         public void RemoveAssignedTransientOrder(GameObject npcObject) // <-- NEW METHOD Implementation
         {
              if (assignedTransientOrders.ContainsKey(npcObject))
              {
                   Debug.Log($"PrescriptionManager: Removing assigned Transient order for '{npcObject.name}' from tracking.");
                   assignedTransientOrders.Remove(npcObject);
              } else {
                   Debug.LogWarning($"PrescriptionManager: Attempted to remove assigned Transient order for '{npcObject.name}' but it was not found in the tracking dictionary.");
              }
         }


        // --- Simulation Status Methods (Needed by Basic States) --- // <-- NEW METHODS

        /// <summary>
        /// Simulates whether the prescription claim spot is occupied for inactive NPCs.
        /// </summary>
        public bool SimulateIsPrescriptionClaimSpotOccupied()
        {
             // For simulation, we can check if the *active* claim spot is occupied.
             // If the active spot is occupied, the simulated NPC would also perceive it as occupied.
             // This is a simplification; a more complex simulation might track simulated occupants.
             return currentClaimSpotOccupant != null;
        }

        /// <summary>
        /// Simulates whether the prescription queue is full for inactive NPCs.
        /// </summary>
        public bool SimulateIsPrescriptionQueueFull()
        {
             // For simulation, we need to count both active NPCs in the queue states
             // AND inactive TI NPCs whose simulation state is BasicWaitForPrescription.

             if (tiNpcManager == null)
             {
                  Debug.LogError("PrescriptionManager: TiNpcManager is null! Cannot simulate prescription queue fullness.");
                  return true; // Assume full as a safe fallback
             }

             // Count active NPCs in PrescriptionQueue state
             int activeQueueCount = 0;
             // Need access to active Runners. CustomerManager tracks active Transient.
             // TiNpcManager tracks active TI.
             // Let's assume CustomerManager has a public list of *all* active Runners (Transient + TI).
             // Or, iterate through TiNpcManager.GetActiveTiNpcs() and check their state.
             // Let's iterate active TI and assume CustomerManager.activeCustomers is public for Transient.

             // Count active TI NPCs in PrescriptionQueue state
             int activeTiQueueCount = 0;
             foreach (var tiData in tiNpcManager.GetActiveTiNpcs())
             {
                  if (tiData.IsActiveGameObject && tiData.NpcGameObject != null)
                  {
                       NpcStateMachineRunner runner = tiData.NpcGameObject.GetComponent<NpcStateMachineRunner>();
                       if (runner != null && runner.GetCurrentState() != null && runner.GetCurrentState().HandledState.Equals(CustomerState.PrescriptionQueue))
                       {
                            activeTiQueueCount++;
                       }
                  }
             }

             // Count active Transient NPCs in PrescriptionQueue state
             int activeTransientQueueCount = 0;
             // Assuming CustomerManager.activeCustomers is accessible (it's private in the provided code)
             // If not accessible, CustomerManager would need a public method like GetActiveTransientRunners()
             // For now, let's simplify and just count active TI in queue + inactive in basic wait.
             // A full active transient count would require modifying CustomerManager.

             activeQueueCount = activeTiQueueCount; // Simplified count for now


             // Count inactive TI NPCs in BasicWaitForPrescription state
             int inactiveSimulatedQueueCount = 0;
             foreach (var tiData in tiNpcManager.allTiNpcs.Values) // Iterate ALL TI data
             {
                  // Only count if inactive AND in the specific simulation state
                  if (!tiData.IsActiveGameObject && tiData.CurrentStateEnum != null && tiData.CurrentStateEnum.Equals(BasicState.BasicWaitForPrescription))
                  {
                       inactiveSimulatedQueueCount++;
                  }
             }

             int totalSimulatedQueueCount = activeQueueCount + inactiveSimulatedQueueCount;

             // Compare total simulated count to the number of available queue spots
             bool isFull = totalSimulatedQueueCount >= (prescriptionQueueSpots?.Count ?? 0);

             // Debug.Log($"SIM PrescriptionManager: Simulated Queue Status: Active in Queue: {activeQueueCount}, Inactive in BasicWait: {inactiveSimulatedQueueCount}, Total Simulated: {totalSimulatedQueueCount}, Max Spots: {prescriptionQueueSpots?.Count ?? 0}. IsFull: {isFull}"); // Too noisy

             return isFull;
        }

        // --- END Simulation Status Methods ---


        // --- Placeholder Methods (Implemented in this substep or earlier) ---

        /// <summary>
        /// Gets the transform for the prescription claim point.
        /// </summary>
        public Transform GetPrescriptionClaimPoint()
        {
             return prescriptionClaimPoint;
        }

        // --- END Placeholder Methods ---
    }
}

// --- END OF FILE PrescriptionManager.cs ---