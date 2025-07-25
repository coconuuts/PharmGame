// --- START OF FILE TiNpcStateTransitionHandler.cs ---

using UnityEngine;
using System.Collections.Generic;
using System; // Needed for System.Enum, Type
using System.Linq; // Needed for ToDictionary
using Game.NPC.States; // Needed for NpcStateSO
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateSO
using Game.NPC.Types; // Needed for NpcTypeDefinitionSO, TestState enum
using Game.NPC; // Needed for CustomerState, GeneralState, PathState, CashierState enums
using CustomerManagement; // Needed for CustomerManager, BrowseLocation, QueueType
using Game.Navigation; // Needed for WaypointManager, PathSO
using Game.Prescriptions; // Needed for PrescriptionManager
using Game.Spatial; // Needed for GridManager (needed by WaypointManager.GetWaypointTransform)
using Game.NPC.TI;
using Game.NPC.Handlers;

/// <summary>
/// Handles the logic for determining state transitions between Active (NpcStateSO)
/// and Basic (BasicNpcStateSO) states for TI NPCs during activation and deactivation.
/// Also manages the mapping between these state types.
/// </summary>
public class TiNpcStateTransitionHandler : MonoBehaviour
{
    // --- State Mapping Dictionaries (Moved from TiNpcManager) ---
    private Dictionary<Enum, Enum> activeToBaseStateMap;
    private Dictionary<Enum, Enum> basicToActiveStateMap;
    // --- END Moved Fields ---

    // --- Dependencies (Will be injected by TiNpcManager) ---
    private BasicNpcStateManager basicNpcStateManager; // Needed to validate basic states during mapping setup, and for BasicStateSO.OnEnter calls
    private CustomerManager customerManager; // Needed for queue/register checks and getting browse locations
    private WaypointManager waypointManager; // Needed for path data lookup
    private PrescriptionManager prescriptionManager; // Needed for prescription queue/spot checks
    private CashierManager cashierManager; // Needed for cashier spot checks
    private TimeManager timeManager; // Needed for schedule checks
    private List<NpcTypeDefinitionSO> npcTypes; // Needed to set up mappings
    // GridManager is needed by WaypointManager.GetWaypointTransform, so WaypointManager dependency is sufficient here.
    // TiNpcManager is the caller, doesn't need to be injected here unless we need its data collection directly.

    void Awake()
    {
        // Dependencies are not set in Awake, they will be injected via Initialize.
    }

    void Start()
    {
        // Dependencies are not set in Start, they will be injected via Initialize.
    }

    // OnEnable/OnDisable/OnDestroy are not strictly needed for this component's current role.


    /// <summary>
    /// Initializes the State Transition Handler with necessary data and references.
    /// Called by the TiNpcManager after it has acquired singleton instances and loaded type definitions.
    /// </summary>
    /// <param name="npcTypes">List of NpcTypeDefinitionSO assets from TiNpcManager.</param>
    /// <param name="basicStateAssets">List of BasicNpcStateSO assets from BasicNpcStateManager.</param>
    /// <param name="basicNpcStateManager">Reference to the BasicNpcStateManager.</param>
    /// <param name="customerManager">Reference to the CustomerManager.</param>
    /// <param name="waypointManager">Reference to the WaypointManager.</param>
    /// <param name="prescriptionManager">Reference to the PrescriptionManager.</param>
    /// <param name="cashierManager">Reference to the CashierManager.</param>
    /// <param name="timeManager">Reference to the TimeManager.</param>
    public void Initialize(List<NpcTypeDefinitionSO> npcTypes, List<BasicNpcStateSO> basicStateAssets, BasicNpcStateManager basicNpcStateManager, CustomerManager customerManager, WaypointManager waypointManager, PrescriptionManager prescriptionManager, CashierManager cashierManager, TimeManager timeManager)
    {
        this.npcTypes = npcTypes; // Store reference
        this.basicNpcStateManager = basicNpcStateManager; // Store reference
        this.customerManager = customerManager;
        this.waypointManager = waypointManager;
        this.prescriptionManager = prescriptionManager;
        this.cashierManager = cashierManager;
        this.timeManager = timeManager;

        Debug.Log("TiNpcStateTransitionHandler: Initialized with manager references and type data.");

        // --- Setup State Mappings (Logic moved from TiNpcManager) ---
        SetupStateMappings(npcTypes, basicStateAssets); // Pass lists to the setup method
        // --- END Setup State Mappings ---
    }

     /// <summary>
     /// Sets up the mapping dictionaries between active and basic states.
     /// MOVED from TiNpcManager, now called by Initialize.
     /// </summary>
     private void SetupStateMappings(List<NpcTypeDefinitionSO> npcTypes, List<BasicNpcStateSO> basicStateAssets) // Accepts lists from Initialize
     {
          activeToBaseStateMap = new Dictionary<Enum, Enum>();
          basicToActiveStateMap = new Dictionary<Enum, Enum>();

          // Define the mappings based on the plan

          // Active -> Basic mappings
          // General States
          activeToBaseStateMap[GeneralState.Idle] = BasicState.BasicPatrol; // Assume general idle/non-customer states map to patrol
          activeToBaseStateMap[GeneralState.Emoting] = BasicState.BasicPatrol; // Assume these interruptions resume patrol when inactive
          activeToBaseStateMap[GeneralState.Social] = BasicState.BasicPatrol;
          activeToBaseStateMap[GeneralState.Combat] = BasicState.BasicPatrol; // Combatting NPC when inactive -> patrol
                                                                               // Note: GeneralState.Initializing, GeneralState.ReturningToPool, GeneralState.Death are terminal or transient and don't map to simulation states

          // Test States
          activeToBaseStateMap[TestState.Patrol] = BasicState.BasicPatrol;

          // Customer States
          activeToBaseStateMap[CustomerState.LookingToShop] = BasicState.BasicLookToShop;
          activeToBaseStateMap[CustomerState.Entering] = BasicState.BasicEnteringStore;
          activeToBaseStateMap[CustomerState.Browse] = BasicState.BasicBrowse;
          activeToBaseStateMap[CustomerState.MovingToRegister] = BasicState.BasicWaitForCashier; // Collapse multiple active states to one basic state
          activeToBaseStateMap[CustomerState.WaitingAtRegister] = BasicState.BasicWaitForCashier;
          activeToBaseStateMap[CustomerState.Queue] = BasicState.BasicWaitForCashier;
          activeToBaseStateMap[CustomerState.SecondaryQueue] = BasicState.BasicExitingStore; // Secondary queue maps to Exiting simulation (giving up)
          activeToBaseStateMap[CustomerState.Exiting] = BasicState.BasicExitingStore;
          // CustomerState.Inactive and CustomerState.TransactionActive are not mapped

          activeToBaseStateMap[PathState.FollowPath] = BasicPathState.BasicFollowPath;

          // Cashier States
          activeToBaseStateMap[CashierState.CashierMovingToCashSpot] = BasicState.BasicCashierMovingToCashSpot;
          activeToBaseStateMap[CashierState.CashierWaitingForCustomer] = BasicState.BasicCashierWaitingForCustomer;
          activeToBaseStateMap[CashierState.CashierProcessingCheckout] = BasicState.BasicCashierProcessingCheckout;
          activeToBaseStateMap[CashierState.CashierGoingHome] = BasicPathState.BasicFollowPath;

          // Prescription State Mappings ---
          // These mappings are STILL needed for Transient NPCs and for TI NPCs *before* they hit a Decision Point
          // if they somehow ended up in these states via old logic or specific non-DecisionPoint transitions.
          activeToBaseStateMap[CustomerState.LookToPrescription] = BasicState.BasicLookToPrescription;
          activeToBaseStateMap[CustomerState.PrescriptionEntering] = BasicState.BasicWaitForPrescription; // Map to BasicWaitForPrescription (queue sim)
          activeToBaseStateMap[CustomerState.PrescriptionQueue] = BasicState.BasicWaitForPrescription; // Map to BasicWaitForPrescription (queue sim)
          activeToBaseStateMap[CustomerState.WaitingForPrescription] = BasicState.BasicWaitingAtPrescriptionSpot; // Map active WaitingForPrescription to new basic state
          activeToBaseStateMap[CustomerState.WaitingForDelivery] = BasicState.BasicWaitingForDeliverySim; // Map new basic state back to active WaitingForDelivery

          // Basic -> Active mappings
          basicToActiveStateMap[BasicState.BasicPatrol] = TestState.Patrol;
          basicToActiveStateMap[BasicState.BasicLookToShop] = CustomerState.LookingToShop;
          basicToActiveStateMap[BasicState.BasicEnteringStore] = CustomerState.Entering;
          basicToActiveStateMap[BasicState.BasicWaitForCashier] = CustomerState.Queue; // This is the DEFAULT mapping, can be overridden during activation for specific logic.
          basicToActiveStateMap[BasicState.BasicExitingStore] = CustomerState.Exiting;
          basicToActiveStateMap[BasicState.BasicIdleAtHome] = GeneralState.Idle; // Map to Active Idle as a fallback
          basicToActiveStateMap[BasicPathState.BasicFollowPath] = PathState.FollowPath;
          basicToActiveStateMap[BasicState.BasicLookToPrescription] = CustomerState.LookToPrescription;
          basicToActiveStateMap[BasicState.BasicWaitForPrescription] = CustomerState.PrescriptionQueue; // Map back to the queue state as the default
          basicToActiveStateMap[BasicState.BasicWaitingAtPrescriptionSpot] = CustomerState.WaitingForPrescription; // Map new basic state back to active WaitingForPrescription
          basicToActiveStateMap[BasicState.BasicWaitingForDeliverySim] = CustomerState.WaitingForDelivery; // Map new basic state back to active WaitingForDelivery
          basicToActiveStateMap[BasicState.BasicCashierMovingToCashSpot] = CashierState.CashierMovingToCashSpot;
          basicToActiveStateMap[BasicState.BasicCashierWaitingForCustomer] = CashierState.CashierWaitingForCustomer;
          basicToActiveStateMap[BasicState.BasicCashierProcessingCheckout] = CashierState.CashierProcessingCheckout;

          Debug.Log($"TiNpcStateTransitionHandler: State mappings setup. Active->Basic: {activeToBaseStateMap.Count}, Basic->Active: {basicToActiveStateMap.Count}");

          // Optional: Add validation here using the provided lists to ensure all mapped states exist
          // This could iterate through activeToBaseStateMap and check if the BasicStateSO exists
          // and iterate through basicToActiveStateMap and check if the ActiveStateSO exists (via TiNpcManager's GetStateSO)
          // For now, we rely on GetBasicStateSO and GetStateSO logging errors if states are missing.
      }

    /// <summary>
    /// Gets the corresponding Basic State enum for a given Active State enum.
    /// Returns BasicState.BasicPatrol if no direct mapping is found (fallback for unmapped active states).
    /// MOVED from TiNpcManager.
    /// </summary>
    public Enum GetBasicStateFromActiveState(Enum activeStateEnum)
    {
        if (activeStateEnum == null)
        {
            Debug.LogWarning($"TiNpcStateTransitionHandler: GetBasicStateFromActiveState called with null activeStateEnum. Falling back to BasicPatrol.");
            return BasicState.BasicPatrol; // Safe default if input is null
        }

        if (activeToBaseStateMap.TryGetValue(activeStateEnum, out Enum basicStateEnum))
        {
            return basicStateEnum;
        }

        // Fallback for active states that don't have a defined mapping (e.g., unlisted states, future states)
        // Assume these should default to patrolling when inactive.
        Debug.LogWarning($"TiNpcStateTransitionHandler: No Basic State mapping found for Active State '{activeStateEnum.GetType().Name}.{activeStateEnum.ToString() ?? "NULL"}'. Falling back to BasicPatrol.");
        return BasicState.BasicPatrol; // Default fallback for unmapped active states
    }

    /// <summary>
    /// Gets the corresponding Active State enum for a given Basic State enum.
    /// Returns GeneralState.Idle if no direct mapping is found (should not happen with correct setup).
    /// MOVED from TiNpcManager.
    /// </summary>
    public Enum GetActiveStateFromBasicState(Enum basicStateEnum)
    {
        if (basicStateEnum == null)
        {
            Debug.LogWarning($"TiNpcStateTransitionHandler: GetActiveStateFromBasicState called with null basicStateEnum. Falling back to GeneralState.Idle.");
            return GeneralState.Idle; // Safe active fallback if input is null
        }

        if (basicToActiveStateMap.TryGetValue(basicStateEnum, out Enum activeStateEnum))
        {
            return activeStateEnum;
        }

        // Error if a Basic State doesn't have a mapping back to an Active State
        Debug.LogError($"TiNpcStateTransitionHandler: No Active State mapping found for Basic State '{basicStateEnum.GetType().Name}.{basicStateEnum.ToString() ?? "NULL"}'! Returning GeneralState.Idle as fallback. Review mappings!");
        return GeneralState.Idle; // Error fallback
    }

    /// <summary>
    /// Determines the appropriate Active State for a TI NPC to transition to upon activation,
    /// based on its saved Basic State and current game conditions.
    /// Also primes the Runner and TiData with necessary state-specific data before the transition.
    /// MOVED and REFACTORED from TiNpcManager.RequestActivateTiNpc.
    /// </summary>
    /// <param name="tiData">The persistent data of the NPC being activated.</param>
    /// <param name="runner">The NpcStateMachineRunner of the newly activated GameObject.</param>
    /// <param name="currentTime">The current game time.</param>
    /// <returns>The System.Enum key for the determined starting Active State, or null if determination fails.</returns>
    public Enum DetermineActivationState(TiNpcData tiData, NpcStateMachineRunner runner, DateTime currentTime)
    {
        if (tiData == null || runner == null || basicNpcStateManager == null || customerManager == null || waypointManager == null || prescriptionManager == null || cashierManager == null || timeManager == null)
        {
            Debug.LogError($"TiNpcStateTransitionHandler: Cannot determine activation state for '{tiData?.Id ?? "NULL"}'. Missing required dependencies.", this);
            // Clear simulation data on error
            tiData.simulatedTargetPosition = null;
            tiData.simulatedStateTimer = 0f;
            tiData.simulatedPathID = null;
            tiData.simulatedWaypointIndex = -1;
            tiData.simulatedFollowReverse = false;
            tiData.isFollowingPathBasic = false;
            // Return null, caller (TiNpcManager) will handle fallback
            return null;
        }

        Enum savedBasicStateEnum = tiData.CurrentStateEnum;
        Enum startingActiveStateEnum = null; // The active state we will transition to

        // --- Handle activation based on the saved BasicStateEnum ---
        if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicIdleAtHome))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicIdleAtHome. Checking schedule for activation.", runner.gameObject);

            if (tiData.startDay.IsWithinRange(currentTime))
            {
                // Day has started, activate to the intended day start state (uses the refined property)
                Debug.Log($"PROXIMITY {tiData.Id}: Day has started ({tiData.startDay}, Current Time: {currentTime:HH:mm}). Activating to intended day start Active state.", runner.gameObject);
                startingActiveStateEnum = tiData.DayStartActiveStateEnum;

                // --- If activating into a PathState, prime the TiData's simulated path data from the dayStart fields ---
                // This logic uses the DayStart... fields directly from TiNpcData.
                if (startingActiveStateEnum != null && startingActiveStateEnum.Equals(PathState.FollowPath))
                {
                    // Use the dayStart fields to initialize the simulated path data on TiData
                    // PathStateSO.OnEnter will read these directly from TiData when IsTrueIdentityNpc is true
                    tiData.simulatedPathID = tiData.DayStartPathID; // <-- Use DayStartPathID
                    tiData.simulatedWaypointIndex = tiData.DayStartStartIndex; // <-- Use DayStartStartIndex
                    tiData.simulatedFollowReverse = tiData.DayStartFollowReverse; // <-- Use DayStartFollowReverse
                    tiData.isFollowingPathBasic = true; // Flag to tell PathStateSO.OnEnter to restore

                    Debug.Log($"PROXIMITY {tiData.Id}: Primed TiData for PathState activation from DayStart: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", runner.gameObject);
                }

                // Clear all NON-PATH simulation data as active state takes over
                tiData.simulatedTargetPosition = null;
                tiData.simulatedStateTimer = 0f;
                // Note: simulatedPathID, simulatedWaypointIndex, simulatedFollowReverse, isFollowingPathBasic
                // are NOT cleared here if activating into a PathState, as they are needed by PathStateSO.OnEnter.
                // They ARE cleared if activating into a non-PathState (handled by the else block below).

            }
            else
            {
                // Day has NOT started, NPC should remain idle at home even if activated.
                Debug.Log($"PROXIMITY {tiData.Id}: Day has NOT started ({tiData.startDay}, Current Time: {currentTime:HH:mm}). Activating to Active Idle state.", runner.gameObject);
                startingActiveStateEnum = GeneralState.Idle; // Activate to Active Idle state

                // Clear all simulation data as active state takes over
                tiData.simulatedTargetPosition = null;
                tiData.simulatedStateTimer = 0f;
                // Clear path simulation data
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
            }
        }

        // --- Handle activation from BasicWaitForCashierState ---
        else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitForCashier))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitForCashier (Queue Sim). Checking live queue/register status.", runner.gameObject);

            // Check register occupancy
            if (customerManager.IsRegisterOccupied() == false)
            {
                // Register is free, go straight there
                Debug.Log($"PROXIMITY {tiData.Id}: Register is free. Activating to MovingToRegister state.", runner.gameObject);
                startingActiveStateEnum = CustomerState.MovingToRegister;
                // Clear simulation data as active state takes over
                tiData.simulatedTargetPosition = null;
                tiData.simulatedStateTimer = 0f;
                // Clear path simulation data
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
            }
            else
            {
                // Register is busy, try to join the main queue
                Debug.Log($"PROXIMITY {tiData.Id}: Register is busy. Attempting to join main queue.", runner.gameObject);

                // Need the Runner's QueueHandler to configure it *before* the state transition
                NpcQueueHandler queueHandler = runner.QueueHandler; // Get handler reference
                if (queueHandler != null)
                {
                    Transform assignedSpotTransform;
                    int assignedSpotIndex;

                    if (customerManager.TryJoinQueue(runner, out assignedSpotTransform, out assignedSpotIndex))
                    {
                        // Successfully joined queue, setup the handler and transition to Queue state
                        Debug.Log($"PROXIMITY {tiData.Id}: Successfully rejoined main queue at spot {assignedSpotIndex}. Activating to Queue state.", runner.gameObject);
                        // Use SetupQueueSpot method to configure the handler and runner target
                        queueHandler.SetupQueueSpot(assignedSpotTransform, assignedSpotIndex, QueueType.Main);
                        startingActiveStateEnum = CustomerState.Queue;
                        // Clear simulation data as active state takes over
                        tiData.simulatedTargetPosition = null; // Clear simulated target
                        tiData.simulatedStateTimer = 0f; // Reset timer
                        // Clear path simulation data
                        tiData.simulatedPathID = null;
                        tiData.simulatedWaypointIndex = -1;
                        tiData.simulatedFollowReverse = false;
                        tiData.isFollowingPathBasic = false;

                    }
                    else
                    {
                        // Main queue is full, cannot be a customer right now
                        Debug.Log($"PROXIMITY {tiData.Id}: Main queue is full. Cannot join. Activating to Exiting state.", runner.gameObject);
                        startingActiveStateEnum = CustomerState.Exiting; // Give up on shopping
                                                                         // Clear simulation data as active state takes over
                        tiData.simulatedTargetPosition = null;
                        tiData.simulatedStateTimer = 0f;
                        // Clear path simulation data
                        tiData.simulatedPathID = null;
                        tiData.simulatedWaypointIndex = -1;
                        tiData.simulatedFollowReverse = false;
                        tiData.isFollowingPathBasic = false;
                    }
                }
                else
                {
                    // QueueHandler missing - critical error for queue state
                    Debug.LogError($"PROXIMITY {tiData.Id}: Runner is missing NpcQueueHandler component during BasicWaitForCashier activation! Cannot handle queue logic. Activating to Exiting as fallback.", runner.gameObject);
                    startingActiveStateEnum = CustomerState.Exiting; // Fallback
                                                                     // Clear simulation data
                    tiData.simulatedTargetPosition = null;
                    tiData.simulatedStateTimer = 0f;
                    // Clear path simulation data
                    tiData.simulatedPathID = null;
                    tiData.simulatedWaypointIndex = -1;
                    tiData.simulatedFollowReverse = false;
                    tiData.isFollowingPathBasic = false;
                }
            }
        }
        // --- END BasicWaitForCashierState handling ---

        // --- Handle activation from BasicBrowseState ---
        else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicBrowse))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicBrowse. Getting a new browse location from CustomerManager.", runner.gameObject);

            BrowseLocation? newBrowseLocation = customerManager?.GetRandomBrowseLocation();

            if (newBrowseLocation.HasValue && newBrowseLocation.Value.browsePoint != null)
            {
                // Set the runner's target location BEFORE transitioning to the state
                runner.CurrentTargetLocation = newBrowseLocation; // Set Runner's target field
                runner.SetCurrentDestinationPosition(newBrowseLocation.Value.browsePoint.position); // Also set runner's last destination position field
                runner._hasReachedCurrentDestination = false; // Mark as needing to move
                startingActiveStateEnum = CustomerState.Browse; // Set the target state to active Browse

                Debug.Log($"PROXIMITY {tiData.Id}: Successfully got new browse location {newBrowseLocation.Value.browsePoint.name}. Activating to Browse state.", runner.gameObject);

                // Clear simulation data as active state takes over
                tiData.simulatedTargetPosition = null; // Clear simulated target
                tiData.simulatedStateTimer = 0f; // Reset timer
                // Clear path simulation data
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
            }
            else
            {
                Debug.LogError($"PROXIMITY {tiData.Id}: Could not get a valid browse location from CustomerManager during BasicBrowse activation! Activating to Exiting as fallback.", runner.gameObject);
                startingActiveStateEnum = CustomerState.Exiting; // Fallback if cannot get browse location
                                                                 // Clear simulation data
                tiData.simulatedTargetPosition = null;
                tiData.simulatedStateTimer = 0f;
                // Clear path simulation data
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
            }
        }
        // --- END BasicBrowseState handling ---

        // --- Handle activation from BasicPathState ---
        else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicPathState.BasicFollowPath))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicFollowPath. Restoring path progress.", runner.gameObject);

            // Get the corresponding active path state enum
            // startingActiveStateEnum = GetActiveStateFromBasicState(savedBasicStateEnum); // Should map to PathState.FollowPath // OLD LINE
            startingActiveStateEnum = PathState.FollowPath; // <-- NEW LINE: Directly assign the correct active state

            // Check if path data is valid on TiData
            if (string.IsNullOrWhiteSpace(tiData.simulatedPathID) || tiData.simulatedWaypointIndex < 0 || waypointManager == null)
            {
                Debug.LogError($"PROXIMITY {tiData.Id}: Invalid path simulation data found during BasicPathState activation! PathID: '{tiData.simulatedPathID}', Index: {tiData.simulatedWaypointIndex}. Transitioning to BasicPatrol fallback.", runner.gameObject);
                // Fallback to a safe state if path data is bad
                startingActiveStateEnum = GetActiveStateFromBasicState(BasicState.BasicPatrol); // Map BasicPatrol to its active counterpart
                                                                                               // Clear path simulation data as it's invalid
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
                tiData.simulatedTargetPosition = null; // Clear simulated target
                tiData.simulatedStateTimer = 0f; // Reset timer
            }
            else
            {
                // Path data seems valid. The generic PathStateSO.OnEnter will read the
                // tiData.simulated... fields directly when it detects activation from a saved state.
                // We just need to ensure the state mapping is correct and the data is still on tiData.
                Debug.Log($"PROXIMITY {tiData.Id}: Path data valid on TiData. PathState.OnEnter will handle restoration from TiData.", runner.gameObject);

                // Clear NON-PATH simulation data as active state takes over
                tiData.simulatedTargetPosition = null; // Clear simulated target
                tiData.simulatedStateTimer = 0f; // Reset timer on activation
                // Note: simulatedPathID, simulatedWaypointIndex, simulatedFollowReverse, isFollowingPathBasic
                // are NOT cleared here, as they are needed by the PathStateSO.OnEnter.
                // They WILL be cleared by the PathFollowingHandler itself when it stops following the path.
            }
        }

        // Handle activation from BasicWaitForPrescriptionState (Queue Sim) ---
        // This handles NPCs who were waiting in the *prescription queue* simulation.
        else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitForPrescription))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitForPrescription (Queue Sim). Attempting to rejoin prescription queue.", runner.gameObject);

            NpcQueueHandler queueHandler = runner.QueueHandler;
            if (queueHandler != null)
            {
                Transform assignedSpotTransform;
                int assignedSpotIndex;
                // Attempt to join the prescription queue
                if (prescriptionManager.TryJoinPrescriptionQueue(runner, out assignedSpotTransform, out assignedSpotIndex))
                {
                    // Successfully joined queue, setup the handler and transition to PrescriptionQueue state
                    Debug.Log($"PROXIMITY {tiData.Id}: Successfully rejoined prescription queue at spot {assignedSpotIndex}. Activating to PrescriptionQueue state.", runner.gameObject);
                    queueHandler.SetupQueueSpot(assignedSpotTransform, assignedSpotIndex, QueueType.Prescription);
                    startingActiveStateEnum = CustomerState.PrescriptionQueue;
                }
                else
                {
                    // Prescription queue is full, cannot rejoin
                    Debug.Log($"PROXIMITY {tiData.Id}: Prescription queue is full. Cannot rejoin. Activating to Exiting state.", runner.gameObject);
                    startingActiveStateEnum = CustomerState.Exiting; // Give up
                }
            }
            else
            {
                Debug.LogError($"PROXIMITY {tiData.Id}: Runner is missing NpcQueueHandler component during BasicWaitForPrescription activation! Cannot handle queue logic. Activating to Exiting.", runner.gameObject);
                startingActiveStateEnum = CustomerState.Exiting; // Fallback
            }
            // Clear simulation data after queue evaluation
            tiData.simulatedTargetPosition = null;
            tiData.simulatedStateTimer = 0f;
            tiData.simulatedPathID = null;
            tiData.simulatedWaypointIndex = -1;
            tiData.simulatedFollowReverse = false;
            tiData.isFollowingPathBasic = false;
        }

        // Handle activation from BasicWaitingAtPrescriptionSpot (Waiting Sim) ---
        // This handles NPCs who were waiting at the claim spot (WaitingForPrescription) simulation.
        else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitingAtPrescriptionSpot))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitingAtPrescriptionSpot (Waiting Sim). Activating to WaitingForPrescription state.", runner.gameObject);
            // Transition directly back to the active WaitingForPrescription state
            startingActiveStateEnum = CustomerState.WaitingForPrescription;

            // Ensure position is set to the claim spot before activation
            if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
            {
                Vector3 claimSpotPos = prescriptionManager.GetPrescriptionClaimPoint().position;
                Quaternion claimSpotRot = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                // Warp the NPC to the claim spot position
                if (runner.MovementHandler != null && runner.MovementHandler.Agent != null)
                {
                    runner.MovementHandler.EnableAgent(); // Ensure agent is enabled for warp
                    if (runner.MovementHandler.Warp(claimSpotPos))
                    {
                        Debug.Log($"PROXIMITY {tiData.Id}: Warped to claim spot {claimSpotPos} for BasicWaitingAtPrescriptionSpot activation.", runner.gameObject);
                        runner.transform.rotation = claimSpotRot; // Set rotation
                    }
                    else
                    {
                        Debug.LogError($"PROXIMITY {tiData.Id}: Failed to warp to claim spot {claimSpotPos} for BasicWaitingAtPrescriptionSpot activation! Is the point on the NavMesh? Activating to Exiting as fallback.", runner.gameObject);
                        startingActiveStateEnum = CustomerState.Exiting; // Fallback
                    }
                }
                else
                {
                    Debug.LogError($"PROXIMITY {tiData.Id}: Runner MovementHandler or Agent is null during BasicWaitingAtPrescriptionSpot activation! Cannot warp. Activating to Exiting as fallback.", runner.gameObject);
                    startingActiveStateEnum = CustomerState.Exiting; // Fallback
                }
            }
            else
            {
                Debug.LogError($"PROXIMITY {tiData.Id}: PrescriptionManager or claim point is null during BasicWaitingAtPrescriptionSpot activation! Cannot warp. Activating to Exiting as fallback.", runner.gameObject);
                startingActiveStateEnum = CustomerState.Exiting; // Fallback
            }

            // Clear simulation data as active state takes over
            tiData.simulatedTargetPosition = null;
            tiData.simulatedStateTimer = 0f;
            tiData.simulatedPathID = null;
            tiData.simulatedWaypointIndex = -1;
            tiData.simulatedFollowReverse = false;
            tiData.isFollowingPathBasic = false;
        }

        // Handle activation from BasicWaitingForDeliverySim (Delivery Sim) ---
        // This handles NPCs who were waiting at the claim spot for delivery (WaitingForDelivery) simulation.
        else if (savedBasicStateEnum != null && savedBasicStateEnum.Equals(BasicState.BasicWaitingForDeliverySim))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is BasicWaitingForDeliverySim (Delivery Sim). Activating to WaitingForDelivery state.", runner.gameObject);
            // Transition directly back to the active WaitingForDelivery state
            startingActiveStateEnum = CustomerState.WaitingForDelivery;

            // Ensure position is set to the claim spot before activation
            if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
            {
                Vector3 claimSpotPos = prescriptionManager.GetPrescriptionClaimPoint().position;
                Quaternion claimSpotRot = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                // Warp the NPC to the claim spot position
                if (runner.MovementHandler != null && runner.MovementHandler.Agent != null)
                {
                    runner.MovementHandler.EnableAgent(); // Ensure agent is enabled for warp
                    if (runner.MovementHandler.Warp(claimSpotPos))
                    {
                        Debug.Log($"PROXIMITY {tiData.Id}: Warped to claim spot {claimSpotPos} for BasicWaitingForDeliverySim activation.", runner.gameObject);
                        runner.transform.rotation = claimSpotRot; // Set rotation
                    }
                    else
                    {
                        Debug.LogError($"PROXIMITY {tiData.Id}: Failed to warp to claim spot {claimSpotPos} for BasicWaitingForDeliverySim activation! Is the point on the NavMesh? Activating to Exiting as fallback.", runner.gameObject);
                        startingActiveStateEnum = CustomerState.Exiting; // Fallback
                    }
                }
                else
                {
                    Debug.LogError($"PROXIMITY {tiData.Id}: Runner MovementHandler or Agent is null during BasicWaitingForDeliverySim activation! Cannot warp. Activating to Exiting as fallback.", runner.gameObject);
                    startingActiveStateEnum = CustomerState.Exiting; // Fallback
                }
            }
            else
            {
                Debug.LogError($"PROXIMITY {tiData.Id}: PrescriptionManager or claim point is null during BasicWaitingForDeliverySim activation! Cannot warp. Activating to Exiting as fallback.", runner.gameObject);
                startingActiveStateEnum = CustomerState.Exiting; // Fallback
            }

            // Clear simulation data as active state takes over
            tiData.simulatedTargetPosition = null;
            tiData.simulatedStateTimer = 0f;
            tiData.simulatedPathID = null;
            tiData.simulatedWaypointIndex = -1;
            tiData.simulatedFollowReverse = false;
            tiData.isFollowingPathBasic = false;
        }

        // Handle activation from Basic Cashier States at Register ---
        else if (savedBasicStateEnum != null &&
                (savedBasicStateEnum.Equals(BasicState.BasicCashierMovingToCashSpot) ||
                 savedBasicStateEnum.Equals(BasicState.BasicCashierWaitingForCustomer) ||
                 savedBasicStateEnum.Equals(BasicState.BasicCashierProcessingCheckout)))
        {
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state is a Cashier state at the register ({savedBasicStateEnum.ToString()}). Activating to corresponding Active state.", runner.gameObject);

            // Map the Basic state to the corresponding Active State
            startingActiveStateEnum = GetActiveStateFromBasicState(savedBasicStateEnum); // This will map to CashierMovingToCashSpot or CashierWaitingForCustomer

            // Set the Runner's target location to the Cashier Spot *before* the state transition
            Transform cashierSpot = cashierManager?.GetCashierSpot();
            if (cashierSpot != null)
            {
                runner.CurrentTargetLocation = new BrowseLocation { browsePoint = cashierSpot, inventory = null };
                runner.SetCurrentDestinationPosition(cashierSpot.position);
                // If activating into MovingToCashSpot, _hasReachedCurrentDestination should be false (handled by SetCurrentDestinationPosition)
                // If activating into WaitingForCustomer or ProcessingCheckout, they are already at the spot,
                // so _hasReachedCurrentDestination should be true. SetCurrentDestinationPosition handles this.
                Debug.Log($"PROXIMITY {tiData.Id}: Set Runner target location to Cashier Spot {cashierSpot.position}.", runner.gameObject);

                // If activating into WaitingForCustomer or ProcessingCheckout, warp them directly to the spot
                if (savedBasicStateEnum.Equals(BasicState.BasicCashierWaitingForCustomer) || savedBasicStateEnum.Equals(BasicState.BasicCashierProcessingCheckout))
                {
                    if (runner.MovementHandler != null && runner.MovementHandler.Agent != null)
                    {
                        runner.MovementHandler.EnableAgent(); // Ensure agent is enabled for warp
                        if (runner.MovementHandler.Warp(cashierSpot.position))
                        {
                            Debug.Log($"PROXIMITY {tiData.Id}: Warped to Cashier spot {cashierSpot.position} for activation into waiting/processing state.", runner.gameObject);
                            runner.transform.rotation = cashierSpot.rotation; // Set rotation
                        }
                        else
                        {
                            Debug.LogError($"PROXIMITY {tiData.Id}: Failed to warp to Cashier spot {cashierSpot.position} for activation! Is the point on the NavMesh? Activating to Idle as fallback.", runner.gameObject);
                            startingActiveStateEnum = GeneralState.Idle; // Fallback
                        }
                    }
                    else
                    {
                        Debug.LogError($"PROXIMITY {tiData.Id}: Runner MovementHandler or Agent is null during Cashier activation! Cannot warp. Activating to Idle as fallback.", runner.gameObject);
                        startingActiveStateEnum = GeneralState.Idle; // Fallback
                    }
                }
            }
            else
            {
                Debug.LogError($"PROXIMITY {tiData.Id}: CashierManager or Cashier Spot is null! Cannot set Runner target location. Activating to Idle as fallback.", runner.gameObject);
                startingActiveStateEnum = GeneralState.Idle; // Fallback
            }

            // Clear simulation data as active state takes over
            tiData.simulatedTargetPosition = null;
            tiData.simulatedStateTimer = 0f;
            tiData.simulatedPathID = null;
            tiData.simulatedWaypointIndex = -1;
            tiData.simulatedFollowReverse = false;
            tiData.isFollowingPathBasic = false;
        }


        // --- Handle activation from any other saved BasicState ---
        // This covers BasicPatrol, BasicLookToShop, BasicEnteringStore, BasicExitingStore, BasicIdleAtHome, BasicPathState
        else if (savedBasicStateEnum != null) // Check if there was *any* saved Basic state
        {
            // Map the Basic state to the corresponding Active State
            startingActiveStateEnum = GetActiveStateFromBasicState(savedBasicStateEnum);
            Debug.Log($"PROXIMITY {tiData.Id}: Saved Basic state '{savedBasicStateEnum.GetType().Name}.{savedBasicStateEnum.ToString() ?? "NULL"}' maps to Active State '{startingActiveStateEnum?.GetType().Name}.{startingActiveStateEnum?.ToString() ?? "NULL"}'. Activating to this state.", runner.gameObject);
            // Reset simulation data when transitioning from simulation to active
            tiData.simulatedTargetPosition = null; // Clear simulated target
            tiData.simulatedStateTimer = 0f; // Reset timer on activation
            // Clear path simulation data (unless it's BasicPathState, handled above)
            if (!savedBasicStateEnum.Equals(BasicPathState.BasicFollowPath))
            {
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
            }
        }
        else // No saved state at all (should default to primary type start state)
        {
            // If no state was saved, or mapping failed, startingActiveStateEnum remains null.
            // The Runner will then fall back to its GetPrimaryStartingStateSO logic (which checks TypeDefs).
            Debug.Log($"PROXIMITY {tiData.Id}: No valid saved Basic state found or mapped. Runner will determine primary starting state from TypeDefs.", runner.gameObject);
            // Ensure simulation data is clean if no saved state existed
            tiData.simulatedTargetPosition = null;
            tiData.simulatedStateTimer = 0f;
            // Clear path simulation data
            tiData.simulatedPathID = null;
            tiData.simulatedWaypointIndex = -1;
            tiData.simulatedFollowReverse = false;
            tiData.isFollowingPathBasic = false;
        }
        // --- END Handle activation based on saved BasicStateEnum ---

        return startingActiveStateEnum; // Return the determined state
    }

    /// <summary>
    /// Determines the appropriate Basic State for a TI NPC to transition to upon deactivation,
    /// based on its current Active State.
    /// Also saves relevant data (like path progress) to the TiNpcData and calls OnEnter for the basic state.
    /// MOVED and REFACTORED from TiNpcManager.RequestDeactivateTiNpc.
    /// </summary>
    /// <param name="tiData">The persistent data of the NPC being deactivated.</param>
    /// <param name="runner">The NpcStateMachineRunner of the GameObject being deactivated.</param>
    /// <returns>The System.Enum key for the determined target Basic State, or null if determination fails.</returns>
    public Enum DetermineDeactivationState(TiNpcData tiData, NpcStateMachineRunner runner)
    {
        if (tiData == null || runner == null || basicNpcStateManager == null || waypointManager == null)
        {
            Debug.LogError($"TiNpcStateTransitionHandler: Cannot determine deactivation state for '{tiData?.Id ?? "NULL"}'. Missing required dependencies (TiData, Runner, BNSM, WaypointManager).", this);
            // Return null, caller (TiNpcManager) will handle fallback/cleanup
            return null;
        }

        // Get the enum key of the current active state
        Enum currentActiveState = runner.GetCurrentState()?.HandledState;

        // Map to the corresponding basic state
        Enum targetBasicState = GetBasicStateFromActiveState(currentActiveState);

        if (targetBasicState != null)
        {
            // --- Save Path Progress if currently following a path OR was interrupted from path state ---
            // We need to save the state as it was *before* any interruption if one is active.
            // If not interrupted, save the current state of the PathFollowingHandler if it's active.
            if (runner.wasInterruptedFromPathState) // Check the flag set by NpcInterruptionHandler
            {
                Debug.Log($"PROXIMITY {tiData.Id}: NPC was interrupted from PathState. Saving TEMPORARY path progress data to TiData for simulation.", runner.gameObject);
                tiData.simulatedPathID = runner.interruptedPathID;
                tiData.simulatedWaypointIndex = runner.interruptedWaypointIndex;
                tiData.simulatedFollowReverse = runner.interruptedFollowReverse;
                tiData.isFollowingPathBasic = true; // Flag that they were on a path simulation when deactivated

                // Clear the temporary data on the runner now that it's saved to persistent TiData
                runner.interruptedPathID = null;
                runner.interruptedWaypointIndex = -1;
                runner.interruptedFollowReverse = false;
                runner.wasInterruptedFromPathState = false;

                Debug.Log($"PROXIMITY {tiData.Id}: Saved TEMPORARY path progress: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", runner.gameObject);
            }
            else if (currentActiveState != null && currentActiveState.Equals(PathState.FollowPath) && runner.PathFollowingHandler != null && runner.PathFollowingHandler.IsFollowingPath)
            {
                // This case should ideally not happen if the NPC is interrupted, as PathState.OnExit stops the handler.
                // But it handles cases where the NPC might be deactivated *while* in PathState without interruption logic triggering first.
                Debug.Log($"PROXIMITY {tiData.Id}: Currently in PathState and following a path (not interrupted). Saving CURRENT path progress.", runner.gameObject);
                tiData.simulatedPathID = runner.PathFollowingHandler.GetCurrentPathSO()?.PathID;
                tiData.simulatedWaypointIndex = runner.PathFollowingHandler.GetCurrentTargetWaypointIndex(); // Save the index they were moving *towards*
                tiData.simulatedFollowReverse = runner.PathFollowingHandler.GetFollowReverse();
                tiData.isFollowingPathBasic = true; // Flag that they were on a path simulation when deactivated

                Debug.Log($"PROXIMITY {tiData.Id}: Saved CURRENT path progress: PathID='{tiData.simulatedPathID}', Index={tiData.simulatedWaypointIndex}, Reverse={tiData.simulatedFollowReverse}.", runner.gameObject);
            }
            else
            {
                // Not following a path or not interrupted from path state, ensure path simulation data is cleared
                // This ensures that if they were previously on a path simulation but are now in BasicPatrol simulation,
                // the path data is correctly reset.
                tiData.simulatedPathID = null;
                tiData.simulatedWaypointIndex = -1;
                tiData.simulatedFollowReverse = false;
                tiData.isFollowingPathBasic = false;
                // simulatedTargetPosition and simulatedStateTimer are handled by the target BasicStateSO.OnEnter
            }
            // --- END MODIFIED ---


            // --- Call OnEnter for the target Basic State to initialize simulation data ---
            // This is crucial to set up simulatedTargetPosition, simulatedStateTimer etc. for the *next* simulation tick.
            // This call happens *after* saving path progress, so the BasicPathStateSO.OnEnter can read the saved data.
            BasicNpcStateSO targetBasicStateSO = basicNpcStateManager?.GetBasicStateSO(targetBasicState);
            if (targetBasicStateSO != null)
            {
                Debug.Log($"PROXIMITY {tiData.Id}: Calling OnEnter for Basic State '{targetBasicStateSO.name}' to initialize simulation data.", runner.gameObject);
                targetBasicStateSO.OnEnter(tiData, basicNpcStateManager); // Pass the data and the manager
            }
            else
            {
                // This shouldn't happen if mapping and GetBasicStateSO work, but defensive
                // Corrected syntax for the log message
                Debug.LogError($"PROXIMITY {tiData.Id}: Could not get target Basic State SO for '{targetBasicState?.GetType().Name}.{targetBasicState?.ToString() ?? "NULL"}' during deactivation request. Cannot initialize simulation state data!", runner.gameObject);
                // Data might be left in a bad state, but proceed with pooling.
            }
            // --- END Call OnEnter ---

            return targetBasicState; // Return the determined state
        }
        else
        {
            // This shouldn't happen if GetBasicStateFromActiveState has a fallback, but defensive.
            // Corrected syntax for the log message
            Debug.LogError($"TiNpcStateTransitionHandler: Could not determine a Basic State mapping for active state '{currentActiveState?.GetType().Name}.{currentActiveState?.ToString() ?? "NULL"}' during deactivation request. Cannot save state for simulation!", runner.gameObject);
            // Return null, caller (TiNpcManager) will handle fallback/cleanup
            return null;
        }
    }
}