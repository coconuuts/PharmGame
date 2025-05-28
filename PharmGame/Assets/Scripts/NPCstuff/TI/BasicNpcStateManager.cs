// --- START OF FILE BasicNpcStateManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System; // Needed for System.Enum, Type
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO
using System.Linq; // Needed for ToDictionary

namespace Game.NPC.BasicStates // Place Basic State Manager in the Basic States namespace
{
    /// <summary>
    /// Manages the simulation of True Identity (TI) NPCs when their GameObject is inactive.
    /// Executes the logic defined in BasicNpcStateSO assets directly on TiNpcData.
    /// Works in conjunction with TiNpcManager.
    /// </summary>
    public class BasicNpcStateManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static BasicNpcStateManager Instance { get; private set; }

        [Header("Basic State Assets")]
        [Tooltip("Drag all BasicNpcStateSO assets into this list.")]
        [SerializeField] private List<BasicNpcStateSO> basicStateAssets;

        // --- Internal Dictionary of available basic states ---
        private Dictionary<Enum, BasicNpcStateSO> availableBasicStates;

        // Reference to the TiNpcManager (needed for state lookups etc.)
        // This reference will be obtained in Awake/Start
        private TiNpcManager tiNpcManager;


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // Consider if this manager should persist
            }
            else
            {
                Debug.LogWarning("BasicNpcStateManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Load Basic State SOs into the dictionary
            LoadBasicStates();

            if (availableBasicStates == null || availableBasicStates.Count == 0)
            {
                 Debug.LogError("BasicNpcStateManager: No Basic State assets loaded! Cannot function. Self-disabling.", this);
                 enabled = false;
                 return;
            }

            Debug.Log($"BasicNpcStateManager: Awake completed. Loaded {availableBasicStates.Count} basic states.");
        }

        private void Start()
        {
            // Get reference to the TiNpcManager
             tiNpcManager = TiNpcManager.Instance;
             if (tiNpcManager == null)
             {
                  Debug.LogError("BasicNpcStateManager: TiNpcManager instance not found! Cannot simulate TI NPCs. Ensure TiNpcManager is in the scene.", this);
                  // Do NOT disable the manager entirely, just simulation won't work.
             }

             Debug.Log("BasicNpcStateManager: Start completed.");
        }

        private void OnEnable()
        {
             // No events to subscribe to yet in this manager itself
        }

        private void OnDisable()
        {
             // No events to unsubscribe from yet
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
             Debug.Log("BasicNpcStateManager: OnDestroy completed.");
        }


        /// <summary>
        /// Loads the BasicNpcStateSO assets into a dictionary for quick lookup by enum key.
        /// </summary>
        private void LoadBasicStates()
        {
            if (basicStateAssets == null)
            {
                 availableBasicStates = new Dictionary<Enum, BasicNpcStateSO>();
                 Debug.LogWarning("BasicNpcStateManager: basicStateAssets list is null.");
                 return;
            }

            try
            {
                 // Use LINQ to create dictionary, checking for nulls and duplicates
                 availableBasicStates = basicStateAssets
                    .Where(stateSO => stateSO != null && stateSO.HandledBasicState != null) // Filter out null SOs and SOs with null handled state
                    .ToDictionary(stateSO => stateSO.HandledBasicState, stateSO => stateSO);

                 // Check for duplicates explicitly if needed, ToDictionary will throw if keys are duplicated
                 // You might want to add specific checks/warnings for duplicate enum keys if desired

            }
            catch (ArgumentException e)
            {
                 Debug.LogError($"BasicNpcStateManager: Error loading basic states into dictionary - Duplicate enum key detected! Review your basicStateAssets list. Error: {e.Message}", this);
                 // Attempt to build dictionary manually to log which key is duplicated if needed
                 availableBasicStates = new Dictionary<Enum, BasicNpcStateSO>();
                  foreach (var stateSO in basicStateAssets)
                  {
                       if (stateSO != null && stateSO.HandledBasicState != null)
                       {
                            if (availableBasicStates.ContainsKey(stateSO.HandledBasicState))
                            {
                                 Debug.LogError($"BasicNpcStateManager: Duplicate basic state enum key '{stateSO.HandledBasicState}' found for asset '{stateSO.name}'. Previous asset was '{availableBasicStates[stateSO.HandledBasicState].name}'. Skipping duplicate.", stateSO);
                            }
                            else
                            {
                                 availableBasicStates.Add(stateSO.HandledBasicState, stateSO);
                            }
                       }
                  }
            }
            catch (Exception e)
            {
                 Debug.LogError($"BasicNpcStateManager: Unexpected error during basic state loading: {e.Message}", this);
            }
        }

        /// <summary>
        /// Gets a Basic State SO by its Enum key.
        /// </summary>
        public BasicNpcStateSO GetBasicStateSO(Enum basicStateEnum)
        {
             if (basicStateEnum == null)
             {
                  Debug.LogError("BasicNpcStateManager: Attempted to get basic state with a null Enum key!");
                  return null;
             }
             if (availableBasicStates != null && availableBasicStates.TryGetValue(basicStateEnum, out BasicNpcStateSO stateSO))
             {
                  return stateSO;
             }

             Debug.LogError($"BasicNpcStateManager: Basic State SO not found in available basic states for Enum '{basicStateEnum.GetType().Name}.{basicStateEnum.ToString()}'!");
             // Fallback logic? Maybe return a default BasicState.None or BasicPatrol?
             // For now, return null and require calling code to handle.

             // Example fallback:
             // if (availableBasicStates != null && availableBasicStates.TryGetValue(BasicState.BasicPatrol, out BasicNpcStateSO patrolFallback))
             // {
             //      Debug.LogWarning($"BasicNpcStateManager: Falling back to BasicPatrol for missing state '{basicStateEnum.GetType().Name}.{basicStateEnum.ToString()}'.");
             //      return patrolFallback;
             // }

             return null; // No fallback implemented yet
        }

        /// <summary>
        /// Executes one simulation tick for a single inactive TI NPC data instance.
        /// Called by TiNpcManager.
        /// </summary>
        /// <param name="data">The TiNpcData instance to simulate.</param>
        /// <param name="deltaTime">The time elapsed since the last simulation tick for this NPC.</param>
        public void SimulateTickForNpc(TiNpcData data, float deltaTime)
        {
            if (data == null || data.IsActiveGameObject)
            {
                // This shouldn't happen if called correctly by TiNpcManager, but defensive check
                Debug.LogWarning("BasicNpcStateManager: SimulateTickForNpc called with null data or an active NPC data!", data?.NpcGameObject);
                return;
            }

            BasicNpcStateSO currentStateSO = GetBasicStateSO(data.CurrentStateEnum);

            if (currentStateSO == null)
            {
                Debug.LogError($"SIM {data.Id}: Current Basic State SO not found for Enum '{data.CurrentStateEnum?.GetType().Name}.{data.CurrentStateEnum?.ToString() ?? "NULL"}' during simulation tick! Transitioning to BasicPatrol (fallback).", data.NpcGameObject);
                // Attempt to transition to a safe fallback state if the current state is invalid
                TransitionToBasicState(data, BasicState.BasicPatrol); // Use BasicState enum directly
                // Note: TransitionToBasicState logs errors if BasicPatrol isn't found either.
                return; // Exit tick processing for this NPC this frame
            }

            // --- Handle Timeout Logic ---
            if (currentStateSO.ShouldUseTimeout)
            {
                // Only decrement and check timeout timer if the NPC is not currently moving towards a simulated target.
                // If simulatedTargetPosition.HasValue is true, the NPC is still in a movement phase, not a waiting/timeout phase.
                if (!data.simulatedTargetPosition.HasValue) // <-- ADDED CHECK HERE
                {
                     data.simulatedStateTimer -= deltaTime;
                     // Debug.Log($"SIM {data.Id}: Timeout timer: {data.simulatedStateTimer:F2}s remaining in state '{currentStateSO.name}'."); // Still too noisy

                     if (data.simulatedStateTimer <= 0)
                     {
                         Debug.Log($"SIM {data.Id}: Timeout occurred in Basic State '{currentStateSO.name}'. Transitioning to BasicExitingStore.");
                         TransitionToBasicState(data, BasicState.BasicExitingStore);
                         return; // Exit tick processing after transition
                     }
                }
                // else { Debug.Log($"SIM {data.Id}: In state '{currentStateSO.name}' (timeout enabled) but moving towards target. Timeout timer paused."); } // Optional debug for clarity
            }

            // --- Execute the state's simulation logic ---
            // This logic might *set* the simulatedTargetPosition or *clear* it upon arrival,
            // or it might start the timer if target is cleared and timeout is enabled.
            // The state's SimulateTick for BasicLookToShop will set timer to -1 while moving,
            // and set timer to Random.Range() when it arrives.
            currentStateSO.SimulateTick(data, deltaTime, this);
            // --- END Execution ---
        }

        /// <summary>
        /// Handles transitioning an inactive NPC's TiNpcData from one Basic State to another.
        /// Called internally by SimulateTickForNpc or by Basic State SOs.
        /// </summary>
        public void TransitionToBasicState(TiNpcData data, Enum nextBasicStateEnum) // Accept System.Enum
        {
             if (data == null)
             {
                  Debug.LogError("BasicNpcStateManager: TransitionToBasicState called with null TiNpcData!", data?.NpcGameObject);
                  return;
             }

             BasicNpcStateSO currentStateSO = GetBasicStateSO(data.CurrentStateEnum);
             BasicNpcStateSO nextStateSO = GetBasicStateSO(nextBasicStateEnum);

             if (nextStateSO == null)
             {
                  Debug.LogError($"SIM {data.Id}: Attempted to transition to a null Basic State SO for Enum '{nextBasicStateEnum?.GetType().Name}.{nextBasicStateEnum?.ToString() ?? "NULL"}'! Attempting fallback to BasicPatrol.", data.NpcGameObject);
                  // Fallback to BasicPatrol if the requested state is null
                  BasicNpcStateSO patrolFallback = GetBasicStateSO(BasicState.BasicPatrol); // Use BasicState enum directly
                  if (patrolFallback != null && currentStateSO != patrolFallback)
                  {
                       Debug.LogWarning($"SIM {data.Id}: Transitioning to BasicPatrol fallback for missing state.", data.NpcGameObject);
                       TransitionToBasicState(data, BasicState.BasicPatrol); // Recursive call with fallback
                       return; // Exit this transition attempt
                  }
                  else
                  {
                       // BasicPatrol fallback is also null or already current
                       Debug.LogError($"SIM {data.Id}: BasicPatrol fallback state is also null or already current! Cannot transition to a safe basic state.", data.NpcGameObject);
                       // Leave the data in its current (potentially invalid) state. Simulation tick might handle.
                       return; // Cannot transition
                  }
             }

              // Prevent transitioning to the current state
             if (currentStateSO == nextStateSO)
             {
                  // Debug.Log($"SIM {data.Id}: Attempted to transition to current basic state '{currentStateSO.name}'. Skipping."); // Too noisy
                  return;
             }


             Debug.Log($"SIM {data.Id}: <color=orange>Basic Transition from {(currentStateSO != null ? currentStateSO.name : "NULL")} to {nextStateSO.name}</color>");

             // Call OnExit for the current state if it exists
             currentStateSO?.OnExit(data, this); // Pass 'this' manager reference

             // Set the new state on the TiNpcData
             data.SetCurrentState(nextBasicStateEnum); // This sets the key/type strings

             // Call OnEnter for the new state
             nextStateSO.OnEnter(data, this); // Pass 'this' manager reference
        }

        /// <summary>
        /// Determines if a given System.Enum value corresponds to a Basic State.
        /// Used by TiNpcManager during activation.
        /// </summary>
        public bool IsBasicState(Enum stateEnum)
        {
             if (stateEnum == null) return false;
             // Check if the enum type matches the BasicState enum type
             if (stateEnum.GetType() == typeof(BasicState)) return true;
             // Alternatively, check if the enum key exists in the availableBasicStates dictionary keys
             // return availableBasicStates?.ContainsKey(stateEnum) ?? false; // This is also valid

             return false; // Does not match BasicState enum type
        }
    }
}
// --- END OF FILE BasicNpcStateManager.cs ---