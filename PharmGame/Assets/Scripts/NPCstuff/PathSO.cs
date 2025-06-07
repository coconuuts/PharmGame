// --- START OF FILE PathSO.cs (Modified) ---

// --- START OF FILE PathSO.cs (Modified) --- // Keep original comment for history

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Game.NPC.Decisions; // Needed for DecisionPointSO, NpcDecisionHelper
using System; // Needed for System.Enum, Type
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.Navigation; // Needed for PathTransitionDetails // <-- Added PathTransitionDetails

namespace Game.Navigation
{
    /// <summary>
    /// Defines a sequence of waypoints that form a navigation path, referenced by ID.
    /// This is a Scriptable Object asset.
    /// Now includes configuration for the behavior upon reaching the end of the path.
    /// MODIFIED: Returns PathTransitionDetails from GetNextActiveStateEnum.
    /// </summary>
    [CreateAssetMenu(fileName = "Path_", menuName = "Navigation/Path", order = 1)]
    [HelpURL("https://your-documentation-link-here.com/PathSO")]
    public class PathSO : ScriptableObject
    {
        [Header("Path Settings")]
        [Tooltip("A unique identifier for this path.")]
        [SerializeField] private string pathID;
        public string PathID { get => pathID; internal set => pathID = value; }

        [Tooltip("The ordered list of waypoint IDs that make up this path.")]
        [SerializeField] private List<string> waypointIDs;
        public List<string> WaypointIDs { get => waypointIDs; internal set => waypointIDs = value; }


        // --- End Behavior Configuration ---
        [Header("End Behavior")]
        [Tooltip("If true, reaching the end of this path will trigger decision logic at a Decision Point. If false, it will transition to a fixed state.")]
        [SerializeField] private bool leadsToDecisionPoint = false;

        [Tooltip("If 'Leads To Decision Point' is true: The Decision Point asset linked to the end of this path.")]
        [SerializeField] private DecisionPointSO decisionPoint;

        [Tooltip("If 'Leads To Decision Point' is false: The Enum key for the *Active* state to transition to upon reaching the end of the path.")]
        [SerializeField] private string nextStateEnumKey;
        [Tooltip("If 'Leads To Decision Point' is false: The Type name of the Enum key for the next *Active* state (e.g., Game.NPC.CustomerState, Game.NPC.GeneralState, Game.NPC.PathState).")]
        [SerializeField] private string nextStateEnumType;

        // --- Fixed Transition Path Details (Only used if LeadsToDecisionPoint is false AND nextStateEnum resolves to PathState.FollowPath) ---
        [Header("Fixed Transition Path Details")]
        [Tooltip("If 'Leads To Decision Point' is false AND the Next State is PathState.FollowPath: The PathSO asset for the next path.")]
        [SerializeField] private PathSO fixedTransitionPathAsset; 
        [Tooltip("If 'Leads To Decision Point' is false AND the Next State is PathState.FollowPath: The index of the waypoint to start the next path from (0-based).")]
        [SerializeField] private int fixedTransitionStartIndex; 
        [Tooltip("If 'Leads To Decision Point' is false AND the Next State is PathState.FollowPath: If true, follow the next path in reverse from the start index.")]
        [SerializeField] private bool fixedTransitionFollowReverse; 


        public int WaypointCount => waypointIDs?.Count ?? 0;

        public string GetWaypointID(int index)
        {
            if (waypointIDs != null && index >= 0 && index < waypointIDs.Count)
            {
                string id = waypointIDs[index];
                if (string.IsNullOrWhiteSpace(id))
                {
                     Debug.LogWarning($"PathSO '{name}': Waypoint ID at index {index} is null or empty!", this);
                     return null;
                }
                return id;
            }
            Debug.LogWarning($"PathSO '{name}': Requested waypoint index {index} is out of bounds or list is null! WaypointCount: {WaypointCount}", this);
            return null;
        }

        private void OnEnable()
        {
            ValidatePath();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            ValidatePath();
        }
        #endif

        private void ValidatePath()
        {
            if (string.IsNullOrWhiteSpace(pathID))
            {
                Debug.LogWarning($"PathSO '{name}': Path ID is empty or whitespace. Please assign a unique ID.", this);
            }
            if (waypointIDs == null || waypointIDs.Count < 2)
            {
                Debug.LogWarning($"PathSO '{name}': Path should contain at least 2 waypoint IDs. Current count: {WaypointCount}.", this);
            }
            else if (waypointIDs.Any(id => string.IsNullOrWhiteSpace(id)))
            {
                Debug.LogError($"PathSO '{name}': Contains null or empty waypoint ID entries! Please remove/fix entries.", this);
            }

            // --- Validate End Behavior Configuration ---
            if (leadsToDecisionPoint)
            {
                 if (decisionPoint == null)
                 {
                      Debug.LogWarning($"PathSO '{name}': 'Leads To Decision Point' is true, but no Decision Point asset is assigned.", this);
                 } else {
                      // Optional: Validate Decision Point WaypointID matches the end of this path
                      if (WaypointCount > 0)
                      {
                           // This validation is better placed where the path is *used* (PathStateSO, BasicPathStateSO)
                           // or rely on the DecisionPointSO's own validation that its WaypointID exists.
                           // Let's skip the end waypoint match validation here for now.
                      }
                 }
                 // Warn if fixed transition is also configured
                 if (!string.IsNullOrEmpty(nextStateEnumKey) || !string.IsNullOrEmpty(nextStateEnumType))
                 {
                      Debug.LogWarning($"PathSO '{name}': 'Leads To Decision Point' is true, but fixed Next State ('{nextStateEnumKey}' of type '{nextStateEnumType}') is also configured. The fixed state will be ignored.", this);
                 }
                 // Warn if fixed transition path details are configured when not used
                 if (fixedTransitionPathAsset != null || fixedTransitionStartIndex != 0 || fixedTransitionFollowReverse != false)
                 {
                      Debug.LogWarning($"PathSO '{name}': Fixed Transition Path Details are configured, but 'Leads To Decision Point' is true. These details will be ignored.", this);
                 }
            }
            else // Not leading to a Decision Point, check fixed transition
            {
                 if (string.IsNullOrEmpty(nextStateEnumKey) || string.IsNullOrEmpty(nextStateEnumType))
                 {
                      Debug.LogWarning($"PathSO '{name}': 'Leads To Decision Point' is false, but no fixed Next State is configured. Upon completion, this path will have no defined transition.", this);
                      // Warn if fixed transition path details are configured when not used
                      if (fixedTransitionPathAsset != null || fixedTransitionStartIndex != 0 || fixedTransitionFollowReverse != false)
                      {
                           Debug.LogWarning($"PathSO '{name}': Fixed Transition Path Details are configured, but no fixed Next State is configured. These details will be ignored.", this);
                      }
                 } else {
                      // Attempt to parse the enum to check if it's valid
                      System.Enum parsedEnum = null;
                      try
                      {
                           Type type = Type.GetType(nextStateEnumType);
                           if (type == null || !type.IsEnum)
                           {
                                Debug.LogError($"PathSO '{name}': Invalid Next State Enum Type string '{nextStateEnumType}' configured for fixed transition.", this);
                           } else
                           {
                                // Optional: Check if the key exists in the enum
                                if (!Enum.GetNames(type).Contains(nextStateEnumKey))
                                {
                                     Debug.LogError($"PathSO '{name}': Next State Enum Type '{nextStateEnumType}' is valid, but key '{nextStateEnumKey}' does not exist in that enum for fixed transition.", this);
                                } else {
                                     // Successfully parsed the enum type and key
                                     parsedEnum = (System.Enum)Enum.Parse(type, nextStateEnumKey);
                                }
                           }
                      }
                      catch (Exception e)
                      {
                           Debug.LogError($"PathSO '{name}': Error parsing fixed Next State config: {e.Message}", this);
                      }

                      // If the parsed enum is PathState.FollowPath, validate fixed transition path details
                      if (parsedEnum != null && parsedEnum.GetType() == typeof(Game.NPC.PathState) && parsedEnum.Equals(Game.NPC.PathState.FollowPath))
                      {
                           if (fixedTransitionPathAsset == null)
                           {
                                Debug.LogWarning($"PathSO '{name}': Fixed Next State is PathState.FollowPath, but no Fixed Transition Path Asset is assigned.", this);
                           } else {
                                // TODO: Add validation for fixedTransitionStartIndex being within fixedTransitionPathAsset bounds
                                if (fixedTransitionStartIndex < 0 || fixedTransitionStartIndex >= fixedTransitionPathAsset.WaypointCount)
                                {
                                     Debug.LogWarning($"PathSO '{name}': Fixed Transition Start Index ({fixedTransitionStartIndex}) is out of bounds for Fixed Transition Path Asset '{fixedTransitionPathAsset.name}' (Waypoint Count: {fixedTransitionPathAsset.WaypointCount}).", this);
                                }
                           }
                      } else {
                           // If the parsed enum is NOT PathState.FollowPath, warn if fixed transition path details ARE configured
                           if (fixedTransitionPathAsset != null || fixedTransitionStartIndex != 0 || fixedTransitionFollowReverse != false)
                           {
                                Debug.LogWarning($"PathSO '{name}': Fixed Next State ('{nextStateEnumKey}' of type '{nextStateEnumType}') is NOT PathState.FollowPath, but Fixed Transition Path Details are configured. These details will be ignored.", this);
                           }
                      }
                 }
            }
            // --- END Validate End Behavior Configuration ---
        }

        /// <summary>
        /// Determines the next *Active* state and potential path details for an NPC
        /// based on the end behavior configured on this PathSO.
        /// </summary>
        /// <param name="tiData">The TiNpcData of the NPC making the decision (required for Decision Points).</param>
        /// <param name="tiManager">The TiNpcManager instance (required for Decision Points and state mapping).</param>
        /// <returns>The PathTransitionDetails for the chosen outcome, or details with a null TargetStateEnum if no valid options or configuration.</returns>
        public PathTransitionDetails GetNextActiveStateDetails(Game.NPC.TI.TiNpcData tiData, Game.NPC.TI.TiNpcManager tiManager) // <-- MODIFIED Method Name and Return Type
        {
             if (leadsToDecisionPoint)
             {
                  // --- Handle Decision Point ---
                  if (decisionPoint == null)
                  {
                       Debug.LogError($"PathSO '{name}': Configured to lead to a Decision Point, but 'decisionPoint' field is null! Cannot determine next state.", this);
                       return new PathTransitionDetails(null); // Return invalid details
                  }
                  if (tiData == null || tiManager == null)
                  {
                       Debug.LogError($"PathSO '{name}': Cannot make decision at point '{decisionPoint.PointID}' - TiNpcData ({tiData == null}) or TiNpcManager ({tiManager == null}) is null!", this);
                       return new PathTransitionDetails(null); // Return invalid details
                  }

                  // Call the shared decision logic helper (which now returns PathTransitionDetails)
                  PathTransitionDetails decisionResult = NpcDecisionHelper.MakeDecision(tiData, decisionPoint, tiManager);

                  if (!decisionResult.HasValidTransition)
                  {
                       Debug.LogWarning($"PathSO '{name}': NpcDecisionHelper returned invalid details for NPC '{tiData.Id}' at Decision Point '{decisionPoint.PointID}'. No valid decision could be made.", this);
                       // NpcDecisionHelper already logs errors/warnings, just return the invalid details for caller to handle fallback
                  }

                  return decisionResult; // Return the details from the decision
             }
             else
             {
                  // --- Handle Fixed State Transition ---
                  if (string.IsNullOrEmpty(nextStateEnumKey) || string.IsNullOrEmpty(nextStateEnumType))
                  {
                       Debug.LogWarning($"PathSO '{name}': Configured for fixed transition, but Next State Key ('{nextStateEnumKey}') or Type ('{nextStateEnumType}') is empty! Cannot determine next state.", this);
                       return new PathTransitionDetails(null); // Return invalid details
                  }

                  // Attempt to parse the string key into an enum value
                  System.Enum targetEnum = TiNpcData.ParseStateEnum(nextStateEnumKey, nextStateEnumType);

                  if (targetEnum == null)
                  {
                       Debug.LogError($"PathSO '{name}': Failed to parse fixed Next State config '{nextStateEnumKey}' of type '{nextStateEnumType}'! Cannot determine next state.", this);
                       // TiNpcData.ParseStateEnum already logs errors, just return invalid details for caller to handle fallback
                       return new PathTransitionDetails(null);
                  }

                  // Check if the target state is PathState.FollowPath
                  if (targetEnum.GetType() == typeof(Game.NPC.PathState) && targetEnum.Equals(Game.NPC.PathState.FollowPath))
                  {
                       // If it's a path state, include the fixed transition path details
                       if (fixedTransitionPathAsset == null)
                       {
                            Debug.LogError($"PathSO '{name}': Fixed Next State is PathState.FollowPath but Fixed Transition Path Asset is null! Cannot create valid path transition details.", this);
                            // Return details with null path asset, caller should handle
                            return new PathTransitionDetails(targetEnum, null, fixedTransitionStartIndex, fixedTransitionFollowReverse);
                       }
                       return new PathTransitionDetails(targetEnum, fixedTransitionPathAsset, fixedTransitionStartIndex, fixedTransitionFollowReverse);
                  }
                  else
                  {
                       // If it's not a path state, return details without path info
                       return new PathTransitionDetails(targetEnum);
                  }
             }
        }
    }
}

// --- END OF FILE PathSO.cs (Modified) ---