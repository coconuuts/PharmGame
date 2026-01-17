// --- START OF FILE PrescriptionManager.cs ---

// --- START OF FILE PrescriptionManager.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List and Dictionary, HashSet
using System; // Needed for System.Serializable and Enum
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionGenerator
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
using System.Linq; // Needed for LINQ operations like FirstOrDefault, Select, ToHashSet, ToList
using Game.NPC.BasicStates;
using Systems.Crafting; // Needed for DrugRecipeMappingSO
using Systems.Inventory; // Needed for ItemDetails


namespace Game.Prescriptions // Place the Prescription Manager in its own namespaces
{
    /// <summary>
    /// Manages the generation, assignment, and tracking of prescription orders.
    /// Also manages the Prescription Queue.
    /// Now includes reference to DrugRecipeMappingSO for delivery validation.
    /// MODIFIED: Uses PrescriptionGenerator for order creation.
    /// MODIFIED: Provides list of currently used patient names to the generator.
    /// MODIFIED: Provides a list of currently active orders (unassigned or assigned) for UI display.
    /// MODIFIED: Added tracking for orders marked as "ready" by the player. // <-- Added note
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
        // --- NEW: Reference to Prescription Generator ---
        [Tooltip("Reference to the PrescriptionGenerator component in the scene.")]
        [SerializeField] private PrescriptionGenerator prescriptionGenerator; // Will find if not assigned
        // --- END NEW ---


        [Header("Prescription Fulfillment")]
        [Tooltip("Reference to the ScriptableObject containing mappings from prescription drug names to crafting recipes and output items.")]
        [SerializeField] private DrugRecipeMappingSO drugRecipeMapping; // <-- Added reference


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
        // allOrdersGeneratedToday is kept for historical/debugging purposes if needed,
        // but the UI will now use GetCurrentlyActiveOrders().
        private List<PrescriptionOrder> allOrdersGeneratedToday = new List<PrescriptionOrder>();
        private List<PrescriptionOrder> unassignedOrders = new List<PrescriptionOrder>();
        // We need to track assigned orders, potentially linking them back to the NPC (TI Data or Runner)
        // A Dictionary might be better here: NPC -> Order
        private Dictionary<string, PrescriptionOrder> assignedTiOrders = new Dictionary<string, PrescriptionOrder>(); // TI ID -> Order
        // For transient, we might track them by Runner instance or GameObject
        private Dictionary<GameObject, PrescriptionOrder> assignedTransientOrders = new Dictionary<GameObject, PrescriptionOrder>(); // Transient GO -> Order

        // --- NEW: Tracking for orders marked as ready ---
        [Tooltip("Set of patient names for orders that have been marked as ready by the player.")]
        [SerializeField] // Serialize for debugging/saving
        private HashSet<string> readyOrders = new HashSet<string>(); // Store patient names
        // --- END NEW ---


        // --- Prescription Queue Data ---
        private List<QueueSpot> prescriptionQueueSpots; // List of QueueSpot objects for the prescription queue

        // --- Timers and Flags ---
        private bool ordersGeneratedToday = false;
        private float tiAssignmentTimer = 0f; // Timer for timed TI assignments

        // Coroutine references
        private Coroutine tiAssignmentCoroutine;
        private Coroutine orderGenerationCoroutine; // Added field

        // --- Runtime tracking for active prescription claim spot ---
        private GameObject currentClaimSpotOccupant = null; // Track the GameObject currently at the claim spot

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

               // --- Get PrescriptionGenerator reference ---
               if (prescriptionGenerator == null)
               {
                    prescriptionGenerator = PrescriptionGenerator.Instance;
               }
               if (prescriptionGenerator == null)
               {
                    // The error message is now more specific.
                    Debug.LogError("PrescriptionManager: PrescriptionGenerator.Instance is null. Make sure a GameObject with the PrescriptionGenerator component exists in the scene.", this);
               }

               // --- Check Drug Recipe Mapping reference ---
               if (drugRecipeMapping == null)
               {
                    Debug.LogError("PrescriptionManager: Drug Recipe Mapping SO reference is not assigned! Prescription delivery validation will not work.", this);
               }

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
            // Subscribe to EventManager events (safe to do here)
            EventManager.Subscribe<ClaimPrescriptionSpotEvent>(HandleClaimPrescriptionSpot);
            EventManager.Subscribe<FreePrescriptionClaimSpotEvent>(HandleFreePrescriptionClaimSpot);
            EventManager.Subscribe<QueueSpotFreedEvent>(HandlePrescriptionQueueSpotFreed);

            // 1. Prioritize the valid Singleton over the Inspector reference (which might be a "dead" duplicate).
            if (TimeManager.Instance != null)
            {
                timeManager = TimeManager.Instance;
            }

            // 2. Only subscribe if we have the specific Singleton instance.
            // If TimeManager.Instance is null (Start of game), we skip this and let Start() handle it.
            if (timeManager != null && timeManager == TimeManager.Instance)
            {
                timeManager.OnSunset += HandleSunset;
                timeManager.OnSunrise += HandleSunrise;
                
                // Restart logic if applicable
                if (orderGenerationCoroutine == null && ordersToGeneratePerDay > 0 && !ordersGeneratedToday && orderGenerationTime.IsWithinRange(timeManager.CurrentGameTime) && prescriptionGenerator != null)
                {
                    StartOrderGenerationRoutine();
                }
            }
        }

        private void OnDisable()
        {
             // Unsubscribe from events
             if (timeManager != null)
             {
                 timeManager.OnSunset -= HandleSunset;
                 timeManager.OnSunrise -= HandleSunrise;
             }
             // Unsubscribe from new prescription events
             EventManager.Unsubscribe<ClaimPrescriptionSpotEvent>(HandleClaimPrescriptionSpot);
             EventManager.Unsubscribe<FreePrescriptionClaimSpotEvent>(HandleFreePrescriptionClaimSpot);
             EventManager.Unsubscribe<QueueSpotFreedEvent>(HandlePrescriptionQueueSpotFreed);

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
                readyOrders.Clear(); // <-- Clear ready orders on destroy
                prescriptionQueueSpots.Clear();
                currentClaimSpotOccupant = null; // Clear occupant reference
            }
            Debug.Log("PrescriptionManager: OnDestroy completed.");
        }

        // --- Order Generation Logic ---

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
            readyOrders.Clear(); // <-- Clear ready orders at end of day
            // Note: NPCs currently holding orders will have their flags/references cleared
            // when they exit the prescription flow or are deactivated/pooled.
            // Clearing the manager's dictionaries here ensures they aren't tracked across days.

            ordersGeneratedToday = false; // Allow generation again on the *next* day
        }

        /// <summary>
        /// Handles the OnSunrise event from TimeManager. Starts the TI assignment routine.
        /// </summary>
        private void HandleSunrise()
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
        /// MODIFIED: Uses PrescriptionGenerator to create orders.
        /// MODIFIED: Checks if the generated order is valid before adding it.
        /// </summary>
        private IEnumerator GenerateOrdersRoutine()
        {
            if (prescriptionGenerator == null)
            {
                Debug.LogError("PrescriptionManager: PrescriptionGenerator reference is null! Cannot generate orders.", this);
                orderGenerationCoroutine = null; // Clear coroutine reference
                yield break; // Stop the routine
            }

            Debug.Log($"PrescriptionManager: GenerateOrdersRoutine started. Will generate {ordersToGeneratePerDay} orders.");
            // Lists are cleared on Sunset. Clear them here too for robustness if generation runs multiple times unexpectedly.
            allOrdersGeneratedToday.Clear();
            unassignedOrders.Clear();
            // Assigned orders are NOT cleared here, they persist across generation runs within the same day.
            // Ready orders are NOT cleared here, they persist across generation runs within the same day.

            for (int i = 0; i < ordersToGeneratePerDay; i++)
            {
                // --- MODIFIED: Use the PrescriptionGenerator to create the order ---
                // The generator will now handle uniqueness internally by querying this manager.
                PrescriptionOrder newOrder = prescriptionGenerator.GenerateNewOrder();
                // --- END MODIFIED ---

                // --- NEW: Check if the generated order is valid ---
                // The generator might return a default/invalid order if it couldn't find a unique name.
                if (!string.IsNullOrEmpty(newOrder.patientName) && newOrder.patientName != "Unknown Patient") // Check for default/fallback name
                {
                    allOrdersGeneratedToday.Add(newOrder);
                    unassignedOrders.Add(newOrder);
                    Debug.Log($"PrescriptionManager: Generated valid order {i + 1}/{ordersToGeneratePerDay}: {newOrder}");
                }
                else
                {
                    Debug.LogWarning($"PrescriptionManager: PrescriptionGenerator failed to generate a valid unique order for attempt {i + 1}/{ordersToGeneratePerDay}. Skipping this order.", this);
                    // Decrement i or handle this case based on whether you want exactly ordersToGeneratePerDay
                    // or up to ordersToGeneratePerDay valid orders. Decrementing i means we will try again
                    // to generate the same *number* of orders. Not decrementing means we might end up with fewer.
                    // Let's not decrement for simplicity; we get *up to* ordersToGeneratePerDay valid orders.
                }
                // --- END NEW ---


                // Wait before generating the next order
                yield return new WaitForSeconds(Random.Range(minOrderGenerationInterval, maxOrderGenerationInterval));
            }

            Debug.Log($"PrescriptionManager: Order generation routine finished. {allOrdersGeneratedToday.Count} valid orders generated.");
            orderGenerationCoroutine = null; // Clear coroutine reference
        }

        /// <summary>
        /// Gets a HashSet of all patient names currently associated with unassigned or assigned orders.
        /// Used by PrescriptionGenerator to ensure unique names.
        /// </summary>
        /// <returns>A HashSet of strings representing currently used patient names.</returns>
        public HashSet<string> GetCurrentlyUsedPatientNames()
        {
             HashSet<string> usedNames = new HashSet<string>();

             // Add names from unassigned orders
             foreach (var order in unassignedOrders)
             {
                  usedNames.Add(order.patientName);
             }

             // Add names from assigned TI orders
             foreach (var order in assignedTiOrders.Values)
             {
                  usedNames.Add(order.patientName);
             }

             // Add names from assigned Transient orders
             foreach (var order in assignedTransientOrders.Values)
             {
                  usedNames.Add(order.patientName);
             }

             // Add names from ready orders (they are still "active" in the sense they are being tracked)
             usedNames.UnionWith(readyOrders); // Add all names from the ready set

             // Debug.Log($"PrescriptionManager: Currently tracking {usedNames.Count} unique patient names."); // Too noisy
             return usedNames;
        }

        // --- NEW METHOD: GetCurrentlyActiveOrders ---
        /// <summary>
        /// Gets a list of all prescription orders that are currently unassigned or assigned to an NPC.
        /// These are the orders considered "active" and potentially visible to the player via the UI.
        /// </summary>
        /// <returns>A list of PrescriptionOrder structs representing active orders.</returns>
        public List<PrescriptionOrder> GetCurrentlyActiveOrders()
        {
             List<PrescriptionOrder> activeOrders = new List<PrescriptionOrder>();

             // Add all unassigned orders
             activeOrders.AddRange(unassignedOrders);

             // Add all assigned TI orders (values from the dictionary)
             activeOrders.AddRange(assignedTiOrders.Values);

             // Add all assigned Transient orders (values from the assigned dictionary)
             activeOrders.AddRange(assignedTransientOrders.Values);

             // Note: Ready orders are NOT included in this list, as they are considered fulfilled from the player's task perspective.
             // The UI should only show orders the player *can* still work on (unassigned or assigned).

             // Debug.Log($"PrescriptionManager: Providing {activeOrders.Count} currently active orders."); // Too noisy
             return activeOrders;
        }
        // --- END NEW METHOD ---


        // --- TI Assignment Logic ---

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
        private void AttemptTIAssignment()
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
                        // Also check if this TI NPC is currently assigned a *different* order
                        // This check might be redundant if pendingPrescription flag is the single source of truth,
                        // but adds robustness. However, the core issue is the *manager* tracking.
                        // We need to ensure a TI NPC isn't in assignedTiOrders already.
                        if (!assignedTiOrders.ContainsKey(tiData.Id)) // <-- Add check here
                        {
                            orderToAssign = order;
                            targetTi = tiData;
                            break; // Found a suitable order/NPC, stop searching
                        } else {
                             // Debug.Log($"PrescriptionManager: TI NPC '{tiData.Id}' already has an order assigned in manager tracking. Skipping order for '{order.patientName}'."); // Too noisy
                        }
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

                Debug.Log($"PrescriptionManager: Assigned prescription order for '{orderToAssign.Value.patientName}' to TI NPC '{targetTi.Id}'. {unassignedOrders.Count} unassigned orders remaining, {assignedTiOrders.Count} TI orders assigned.");
            }
        }

        // --- Transient Assignment Logic ---

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

            // Check if we are below the maximum assigned transient orders limit
            if (assignedTransientOrders.Count >= maxAssignedTransientOrders)
            {
                 // Debug.Log($"PrescriptionManager: Transient assignment limit ({maxAssignedTransientOrders}) reached ({assignedTransientOrders.Count}). Cannot assign."); // Too noisy
                 return false; // Limit reached
            }

            // Find the first unassigned order that does NOT match a TI NPC
            // Use ToList() to avoid modifying the collection while iterating
            PrescriptionOrder foundOrder = unassignedOrders.ToList().FirstOrDefault(order =>
            {
                 // Explicitly check if the order is valid (not the default struct) before checking TI match
                 // FirstOrDefault might return default(PrescriptionOrder) if list is empty or no match
                 if (string.IsNullOrEmpty(order.patientName) || order.patientName == "Unknown Patient")
                 {
                      return false; // Ignore invalid/blank orders returned by FirstOrDefault when no match is found
                 }
                 // Now check if it's a non-TI order
                 return tiNpcManager.GetTiNpcData(order.patientName) == null;
            });


            // --- MODIFIED CHECK ---
            // Check if the foundOrder is NOT the default/blank struct.
            // string.IsNullOrEmpty(foundOrder.patientName) will be true for the default struct.
            // Also check against the fallback name just in case.
            if (!string.IsNullOrEmpty(foundOrder.patientName) && foundOrder.patientName != "Unknown Patient")
            {
                // Found a suitable, non-blank order that is not for a TI NPC.
                // Now check the random chance.
                if (Random.value < transientAssignmentChance)
                {
                    // Check if the prescription queue is full. If so, we cannot assign.
                    if (IsPrescriptionQueueFull())
                    {
                        Debug.Log($"PrescriptionManager: Prescription queue is full. Cannot assign transient order to '{runner.gameObject.name}'.");
                        return false; // Cannot assign if queue is full
                    }

                    // All checks passed, assign the order
                    runner.hasPendingPrescriptionTransient = true;
                    runner.assignedOrderTransient = foundOrder; // Assign the valid struct value

                    // Move the order from unassigned to assignedTransientOrders
                    // Use the actual 'foundOrder' struct to remove from the list
                    unassignedOrders.Remove(foundOrder);
                    assignedTransientOrders[runner.gameObject] = foundOrder; // Add to assigned transient

                    Debug.Log($"PrescriptionManager: Assigned transient prescription order for '{foundOrder.patientName}' to NPC '{runner.gameObject.name}'. {unassignedOrders.Count} unassigned orders remaining. {assignedTransientOrders.Count}/{maxAssignedTransientOrders} transient orders assigned.");

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
                // FirstOrDefault returned the default/blank struct because no matching order was found
                // in the unassignedOrders list that was not for a TI NPC.
                // Debug.Log($"PrescriptionManager: No suitable unassigned non-TI orders found."); // Too noisy
                return false; // No order to assign
            }
        }

        // --- Prescription Queue & Spot Management ---

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
                            runnerAtSpot0.QueueHandler.GoToPrescriptionClaimSpotFromQueue();
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
             }
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
         /// Called when the TI NPC successfully completes the prescription flow or becomes impatient in WaitingForDelivery.
         /// </summary>
         public void RemoveAssignedTiOrder(string tiId)
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
         /// Called when the Transient NPC successfully completes the prescription flow or becomes impatient in WaitingForDelivery.
         /// </summary>
         public void RemoveAssignedTransientOrder(GameObject npcObject)
         {
              if (assignedTransientOrders.ContainsKey(npcObject))
              {
                   Debug.Log($"PrescriptionManager: Removing assigned Transient order for '{npcObject.name}' from tracking.");
                   assignedTransientOrders.Remove(npcObject);
              } else {
                   Debug.LogWarning($"PrescriptionManager: Attempted to remove assigned Transient order for '{npcObject.name}' but it was not found in the tracking dictionary.");
              }
         }

        // --- NEW: Methods for tracking Ready Orders ---
        /// <summary>
        /// Marks a prescription order as ready for delivery by the player.
        /// </summary>
        /// <param name="order">The order to mark ready.</param>
        public void MarkOrderReady(PrescriptionOrder order)
        {
            if (string.IsNullOrEmpty(order.patientName))
            {
                Debug.LogWarning("PrescriptionManager: Attempted to mark order ready with null or empty patient name. Ignoring.", this);
                return;
            }

            if (readyOrders.Add(order.patientName)) // Add returns true if the element was added (wasn't already present)
            {
                Debug.Log($"PrescriptionManager: Order for '{order.patientName}' marked as ready.", this);
                // Optional: Publish an event here if other systems need to know an order is ready
                // EventManager.Publish(new OrderMarkedReadyEvent(order));
            }
            else
            {
                Debug.LogWarning($"PrescriptionManager: Order for '{order.patientName}' was already marked as ready.", this);
            }
        }

        /// <summary>
        /// Checks if a prescription order for a given patient name has been marked as ready.
        /// </summary>
        /// <param name="patientName">The patient name to check.</param>
        /// <returns>True if the order is marked ready, false otherwise.</returns>
        public bool IsOrderReady(string patientName)
        {
            if (string.IsNullOrEmpty(patientName))
            {
                // Debug.LogWarning("PrescriptionManager: IsOrderReady called with null or empty patient name. Returning false."); // Too noisy
                return false;
            }
            return readyOrders.Contains(patientName);
        }

        /// <summary>
        /// Unmarks a prescription order for a given patient name as ready.
        /// Called when the order is successfully delivered or the NPC becomes impatient in the delivery state.
        /// </summary>
        /// <param name="patientName">The patient name to unmark.</param>
        public void UnmarkOrderReady(string patientName)
        {
             if (string.IsNullOrEmpty(patientName))
             {
                  Debug.LogWarning("PrescriptionManager: Attempted to unmark order ready with null or empty patient name. Ignoring.", this);
                  return;
             }

             if (readyOrders.Remove(patientName)) // Remove returns true if the element was found and removed
             {
                  Debug.Log($"PrescriptionManager: Order for '{patientName}' unmarked as ready.", this);
                  // Optional: Publish an event here if other systems need to know an order is no longer ready
                  // EventManager.Publish(new OrderUnmarkedReadyEvent(patientName));
             }
             else
             {
                  // This might happen if the order wasn't marked ready in the first place, which is fine.
                  // Debug.LogWarning($"PrescriptionManager: Attempted to unmark order ready for '{patientName}', but it was not found in the ready list."); // Too noisy
             }
        }
        // --- END NEW ---


        // --- Simulation Status Methods (Needed by Basic States) ---

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
        /// --- MODIFIED: Now correctly includes active transient NPCs in the count. ---
        /// </summary>
        public bool SimulateIsPrescriptionQueueFull()
        {
             // For simulation, we need to count both active NPCs in the queue states
             // AND inactive TI NPCs whose simulation state is BasicWaitForPrescription.

             if (tiNpcManager == null || customerManager == null) // Added customerManager check
             {
                  Debug.LogError("PrescriptionManager: TiNpcManager or CustomerManager is null! Cannot simulate prescription queue fullness.");
                  return true; // Assume full as a safe fallback
             }

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

             // Count active Transient NPCs in PrescriptionQueue state using the new method
             int activeTransientQueueCount = 0;
             List<NpcStateMachineRunner> transientRunners = customerManager.GetActiveTransientRunners();
             foreach (var runner in transientRunners)
             {
                 if (runner != null && runner.GetCurrentState() != null && runner.GetCurrentState().HandledState.Equals(CustomerState.PrescriptionQueue))
                 {
                     activeTransientQueueCount++;
                 }
             }

             int activeQueueCount = activeTiQueueCount + activeTransientQueueCount; // Sum both active counts

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

             Debug.Log($"SIM PrescriptionManager: Simulated Queue Status: Active TI: {activeTiQueueCount}, Active Transient: {activeTransientQueueCount}, Inactive Sim: {inactiveSimulatedQueueCount}, Total: {totalSimulatedQueueCount}, Max: {prescriptionQueueSpots?.Count ?? 0}. IsFull: {isFull}"); // More detailed debug log

             return isFull;
        }

        // --- Placeholder Methods ---

        /// <summary>
        /// Gets the transform for the prescription claim point.
        /// </summary>
        public Transform GetPrescriptionClaimPoint()
        {
             return prescriptionClaimPoint;
        }

        /// <summary>
        /// Gets a list of all prescription orders generated today.
        /// This list includes completed orders and is primarily for debugging/historical purposes.
        /// For UI display of active orders, use GetCurrentlyActiveOrders().
        /// </summary>
        /// <returns>A list of PrescriptionOrder structs.</returns>
        public List<PrescriptionOrder> GetAllGeneratedOrders()
        {
            // Returning the direct list for simplicity.
            // Return new List<PrescriptionOrder>(allOrdersGeneratedToday); // Return a copy if external modification is a concern
            return allOrdersGeneratedToday;
        }

        // --- NEW: Method to get expected output item details for delivery ---
          /// <summary>
          /// Looks up the expected ItemDetails of the crafted item required for a given prescription order.
          /// Uses the assigned DrugRecipeMappingSO.
          /// </summary>
          /// <param name="order">The prescription order.</param>
          /// <returns>The ItemDetails of the expected crafted item, or null if mapping not found or invalid.</returns>
          public ItemDetails GetExpectedOutputItemDetails(PrescriptionOrder order)
          {
               if (drugRecipeMapping == null)
               {
                    Debug.LogError("PrescriptionManager: Drug Recipe Mapping SO is null! Cannot get expected output item details.", this);
                    return null;
               }

               // Use the mapping SO to find the details based on the prescribed drug name
               ItemDetails expectedDetails = drugRecipeMapping.GetCraftedOutputItemDetailsForDrug(order.prescribedDrug);

               if (expectedDetails == null)
               {
                    Debug.LogWarning($"PrescriptionManager: No crafted output item details found in mapping for prescribed drug '{order.prescribedDrug}'.", this);
               }

               return expectedDetails;
          }
        // --- END NEW ---
    }
}