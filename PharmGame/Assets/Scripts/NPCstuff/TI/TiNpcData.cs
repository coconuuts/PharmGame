// --- START OF FILE TiNpcData.cs ---

using UnityEngine;
using System;
using System.Collections.Generic; // Needed for Dictionary
using Game.NPC.States;
using Systems.Inventory;
using Game.NPC.Types;
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum
using Game.Utilities; // Needed for TimeRange
using Game.NPC.Decisions; // Needed for DecisionOption
using System.Linq; // Needed for accessing PathState enum

namespace Game.NPC.TI // Keep in the TI namespace
{
    /// <summary>
    /// Represents the persistent data for a True Identity NPC, independent of their GameObject.
    /// Includes fields needed for off-screen simulation, including path following.
    /// Now includes schedule time ranges, unique decision options, and intended day start behavior.
    /// </summary>
    [System.Serializable]
    public class TiNpcData
    {
        [Tooltip("A unique identifier for this TI NPC.")]
        [SerializeField] private string id; // Unique ID for lookup

        [Header("Core Persistent Data")]
        [Tooltip("The NPC's designated home position.")]
        [SerializeField] private Vector3 homePosition;
        [Tooltip("The NPC's designated home rotation.")]
        [SerializeField] private Quaternion homeRotation;

        [Tooltip("The NPC's current world position.")]
        [SerializeField] private Vector3 currentWorldPosition;
        [Tooltip("The NPC's current world rotation.")]
        [SerializeField] private Quaternion currentWorldRotation;

        // Storing state using strings to allow serialization across different enum types
        // This will store EITHER an active state enum (like CustomerState, TestState, PathState) OR a BasicState/BasicPathState enum
        [Tooltip("The string name of the NPC's current state enum value (can be Active or Basic).")]
        [SerializeField] private string currentStateEnumKey;
        [Tooltip("The assembly qualified name of the NPC's current state enum type (can be Active or Basic).")]
        [SerializeField] private string currentStateEnumType;

        // --- Schedule Settings ---
        [Header("Schedule Settings")]
        [Tooltip("The time range during the day when this NPC is allowed to be active or simulated.")]
        [SerializeField] public Game.Utilities.TimeRange startDay;
        [Tooltip("The time range during the day when this NPC should begin exiting/returning home.")]
        [SerializeField] public Game.Utilities.TimeRange endDay;
        // --- END NEW ---

        // --- Decision Point Settings ---
        [Header("Decision Point Settings")]
        [Tooltip("Unique decision options for this NPC, keyed by Decision Point ID.")]
        [SerializeField]
        // --- FIX: Make the serialized field public internal for manager access ---
        public SerializableDecisionOptionDictionary uniqueDecisionOptions = new SerializableDecisionOptionDictionary();
        // --- END FIX ---

        // Public getter returns a runtime Dictionary derived from the serialized list
        public Dictionary<string, DecisionOption> UniqueDecisionOptions => uniqueDecisionOptions.ToDictionary();
        // --- END NEW ---


        // --- PHASE 4, SUBSTEP 1: Add Simulation Data Fields ---
        [Header("Simulation Data (Managed Off-screen)")]
        [Tooltip("The target position for off-screen movement simulation (e.g., patrol point, exit, waypoint). Null if no target.")]
        [SerializeField] public Vector3? simulatedTargetPosition; // Made public for direct access by simulation logic

        [Tooltip("A timer used for off-screen simulation of states like waiting, browsing, etc.")]
        [SerializeField] public float simulatedStateTimer; // Made public for direct access by simulation logic

        // --- NEW: Path Following Simulation Data (Phase 3) ---
        [Tooltip("The ID of the path currently being followed in simulation.")]
        [SerializeField] public string simulatedPathID; // Public for direct access by simulation logic
        [Tooltip("The index of the waypoint the NPC is currently moving *towards* in simulation.")]
        [SerializeField] public int simulatedWaypointIndex; // Public for direct access by simulation logic
        [Tooltip("If true, the NPC is following the path in reverse in simulation.")]
        [SerializeField] public bool simulatedFollowReverse; // Public for direct access by simulation logic
        [Tooltip("True if the NPC is currently following a path in simulation.")]
        [SerializeField] public bool isFollowingPathBasic; // Public flag for simulation logic
        // --- END NEW ---

        // --- NEW: Schedule Runtime Flags (Phase 1, Substep 1.5) ---
        [System.NonSerialized] public bool isEndingDay; // Flag set by ProximityManager when within endDay range
        // --- END NEW ---

        // --- NEW: Intended Day Start Behavior Fields (Step 2) ---
        [Header("Day Start Behavior")]
        [Tooltip("If true, the NPC will follow a path when its day starts. If false, it will transition to a specific state.")]
        [SerializeField] public bool usePathForDayStart = false; // <-- NEW Toggle Field

        [Tooltip("The string name of the NPC's intended *Active* state enum value when its day starts (e.g., TestState.Patrol, CustomerState.LookingToShop). Only used if 'Use Path For Day Start' is false.")]
        [SerializeField] internal string dayStartActiveStateEnumKey;
        [Tooltip("The assembly qualified name of the NPC's intended *Active* state enum type when its day starts (e.g., Game.NPC.TestState, Game.NPC.CustomerState). Only used if 'Use Path For Day Start' is false.")]
        [SerializeField] internal string dayStartActiveStateEnumType;

        [Tooltip("The Path ID if the day start behavior is path following. Only used if 'Use Path For Day Start' is true.")]
        [SerializeField] internal string dayStartPathID;
        [Tooltip("The index of the waypoint to start the path from (0-based) if the day start behavior is path following.")]
        [SerializeField] internal int dayStartStartIndex;
        [Tooltip("Optional: If true, follow the path in reverse from the start index if the day start behavior is path following.")]
        [SerializeField] internal bool dayStartFollowReverse;
        // --- END NEW ---


        [System.NonSerialized] public GameObject NpcGameObject; // Runtime reference to the active GameObject
        // --- END PHASE 4 Fields ---


        [Header("Runtime Data (Not Saved Persistently)")]
        [Tooltip("True if this NPC currently has an active GameObject representation in the scene.")]
        [SerializeField] public bool isActiveGameObject; // Indicates if the data is currently linked to a pooled GameObject


        // --- Public Properties ---
        public string Id => id;
        public Vector3 HomePosition => homePosition;
        public Quaternion HomeRotation => homeRotation;

        // World position and rotation are writable as they change dynamically (simulated or live)
        public Vector3 CurrentWorldPosition { get => currentWorldPosition; set => currentWorldPosition = value; }
        public Quaternion CurrentWorldRotation { get => currentWorldRotation; set => currentWorldRotation = value; }

        // State keys are writable as they change dynamically (simulated or live)
        public string CurrentStateEnumKey { get => currentStateEnumKey; set => currentStateEnumKey = value; }
        public string CurrentStateEnumType { get => currentStateEnumType; set => currentStateEnumType = value; }

        // Activation status is writable
        public bool IsActiveGameObject { get => isActiveGameObject; set => isActiveGameObject = value; }

        // --- NEW: Public Getters for Day Start Behavior (Step 2) ---
        public string DayStartActiveStateEnumKey => dayStartActiveStateEnumKey;
        public string DayStartActiveStateEnumType => dayStartActiveStateEnumType;
        public string DayStartPathID => dayStartPathID;
        public int DayStartStartIndex => dayStartStartIndex;
        public bool DayStartFollowReverse => dayStartFollowReverse;
        // --- END NEW ---

        // --- NEW: Public Getter for the toggle (Step 2) ---
        public bool UsePathForDayStart => usePathForDayStart;
        // --- END NEW ---


        /// <summary>
        /// Attempts to parse the stored state strings into a runtime System.Enum value.
        /// Returns null if parsing fails.
        /// </summary>
        public System.Enum CurrentStateEnum
        {
            get
            {
                return ParseStateEnum(currentStateEnumKey, currentStateEnumType);
            }
        }

         /// <summary>
         /// Attempts to determine and parse the intended day start Active state enum value
         /// based on the 'usePathForDayStart' toggle.
         /// Returns PathState.FollowPath if usePathForDayStart is true,
         /// otherwise attempts to parse the stored state key/type.
         /// Returns null if parsing fails or configuration is invalid.
         /// </summary>
         public System.Enum DayStartActiveStateEnum
         {
              get
              {
                   if (usePathForDayStart)
                   {
                        // If using a path, the intended state is PathState.FollowPath
                        // We need to get the enum value for PathState.FollowPath.
                        // This requires knowing the type Game.NPC.PathState.
                        try
                        {
                            Type pathStateType = typeof(Game.NPC.PathState); // Directly get the type
                            if (pathStateType.IsEnum)
                            {
                                 // Get the enum value for 'FollowPath'
                                 return (System.Enum)Enum.Parse(pathStateType, PathState.FollowPath.ToString());
                            } else {
                                Debug.LogError($"TiNpcData ({id}): Expected Game.NPC.PathState to be an enum, but it's not! Cannot determine Day Start Active State for path.", NpcGameObject);
                                return null;
                            }
                        }
                        catch (Exception e)
                        {
                             Debug.LogError($"TiNpcData ({id}): Failed to get PathState.FollowPath enum value for Day Start Active State: {e.Message}. Check if PathState.FollowPath exists.", NpcGameObject);
                             return null;
                        }
                   }
                   else
                   {
                        // If not using a path, parse the stored state key/type
                        return ParseStateEnum(dayStartActiveStateEnumKey, dayStartActiveStateEnumType);
                   }
              }
         }


        /// <summary>
        /// Constructor for creating a new TiNpcData instance.
        /// </summary>
        public TiNpcData(string id, Vector3 homePosition, Quaternion homeRotation)
        {
            this.id = id;
            this.homePosition = homePosition;
            this.homeRotation = homeRotation;

            // Initialize current state to home location and a default state
            this.currentWorldPosition = homePosition;
            this.currentWorldRotation = homeRotation;

            this.currentStateEnumKey = null; // Set on load/simulation/activation
            this.currentStateEnumType = null; // Set on load/simulation/activation

            // Initialize schedule fields with default (full day)
            this.startDay = new Game.Utilities.TimeRange(0, 0, 23, 59); // Default: available all day
            this.endDay = new Game.Utilities.TimeRange(22, 0, 5, 0); // Default: start exiting between 22:00 and 05:00

            // Initialize unique decision options dictionary wrapper
            this.uniqueDecisionOptions = new SerializableDecisionOptionDictionary(); // <-- Initialize dictionary wrapper

            // Initialize day start behavior fields (will be populated on load)
            this.usePathForDayStart = false; // <-- NEW: Initialize toggle
            this.dayStartActiveStateEnumKey = null;
            this.dayStartActiveStateEnumType = null;
            this.dayStartPathID = null;
            this.dayStartStartIndex = 0;
            this.dayStartFollowReverse = false;


            // Debug.Log($"DEBUG TiNpcData ({id}): Constructor called (InstanceID: {this.GetHashCode()}). Initializing isActiveGameObject=false, NpcGameObject=null.", NpcGameObject); // Too verbose
            this.isActiveGameObject = false; // Starts without a GameObject
            this.NpcGameObject = null;

            // Initialize simulation fields
            this.simulatedTargetPosition = null;
            this.simulatedStateTimer = 0f;

            // Initialize path following simulation fields
            this.simulatedPathID = null;
            this.simulatedWaypointIndex = -1;
            this.simulatedFollowReverse = false;
            this.isFollowingPathBasic = false;

            // Initialize schedule runtime flag
            this.isEndingDay = false; // <-- NEW: Initialize flag
        }

        /// <summary>
        /// Helper to set the current state using a System.Enum. Can be an Active State or a Basic State enum.
        /// </summary>
        public void SetCurrentState(System.Enum stateEnum)
        {
            if (stateEnum == null)
            {
                // Debug.Log($"DEBUG TiNpcData ({id}): Setting CurrentStateEnum to null (InstanceID: {this.GetHashCode()}).", NpcGameObject); // Too verbose
                currentStateEnumKey = null;
                currentStateEnumType = null;
                return;
            }
            // Debug.Log($"DEBUG TiNpcData ({id}): Setting CurrentStateEnum to '{stateEnum.GetType().Name}.{stateEnum.ToString()}' (InstanceID: {this.GetHashCode()}).", NpcGameObject); // Too verbose
            currentStateEnumKey = stateEnum.ToString();
            currentStateEnumType = stateEnum.GetType().AssemblyQualifiedName; // Use AssemblyQualifiedName for robust loading
        }

         /// <summary>
         /// Helper to set the intended day start Active state using a System.Enum.
         /// Note: This does NOT set the usePathForDayStart toggle or path data.
         /// </summary>
         public void SetDayStartActiveState(System.Enum stateEnum)
         {
              if (stateEnum == null)
              {
                   dayStartActiveStateEnumKey = null;
                   dayStartActiveStateEnumType = null;
                   return;
              }
              dayStartActiveStateEnumKey = stateEnum.ToString();
              dayStartActiveStateEnumType = stateEnum.GetType().AssemblyQualifiedName; // Use AssemblyQualifiedName for robust loading
         }

         /// <summary>
         /// Helper to set the intended day start path data.
         /// Note: This does NOT set the usePathForDayStart toggle or state key/type.
         /// </summary>
         public void SetDayStartPath(string pathID, int startIndex, bool followReverse)
         {
              dayStartPathID = pathID;
              dayStartStartIndex = startIndex;
              dayStartFollowReverse = followReverse;
         }


        /// <summary>
        /// Links this TiNpcData instance to an active GameObject.
        /// Called by TiNpcManager during activation.
        /// </summary>
        public void LinkGameObject(GameObject go)
        {
            // Debug.Log($"DEBUG TiNpcData ({id}): Linking GameObject '{go?.name ?? "NULL"}' (InstanceID: {this.GetHashCode()}). Setting NpcGameObject and isActiveGameObject=true.", NpcGameObject); // Too verbose
            this.NpcGameObject = go;
            this.isActiveGameObject = true;
        }

        /// <summary>
        /// Unlinks the active GameObject from this TiNpcData instance.
        /// Called by TiNpcManager when the GameObject is returned to the pool.
        /// </summary>
        public void UnlinkGameObject()
        {
            // Debug.Log($"DEBUG TiNpcData ({id}): Unlinking GameObject '{this.NpcGameObject?.name ?? "NULL"}' (InstanceID: {this.GetHashCode()}). Setting NpcGameObject=null and isActiveGameObject=false.", this.NpcGameObject); // Too verbose
            this.NpcGameObject = null;
            this.isActiveGameObject = false;
        }
         public override int GetHashCode()
         {
             // Simple hash code based on ID (assuming ID is unique)
             return id.GetHashCode();
         }
         // Need Equals as well if overriding GetHashCode and using in collections that might check equality
          public override bool Equals(object obj)
          {
              if (obj == null || GetType() != obj.GetType())
              {
                  return false;
              }

              TiNpcData other = (TiNpcData)obj;
              return id == other.id; // Assuming ID is the unique identifier
          }

         /// <summary>
         /// Static helper to parse state enum strings into a System.Enum value.
         /// </summary>
         /// <param name="enumKey">The string name of the enum value.</param>
         /// <param name="enumTypeString">The assembly qualified name of the enum type.</param>
         /// <returns>The parsed System.Enum value, or null if parsing fails.</returns>
         public static System.Enum ParseStateEnum(string enumKey, string enumTypeString)
         {
              if (string.IsNullOrEmpty(enumKey) || string.IsNullOrEmpty(enumTypeString)) return null;

              try
              {
                   Type enumType = Type.GetType(enumTypeString);
                   if (enumType == null || !enumType.IsEnum)
                   {
                        // Debug.LogError($"TiNpcData: Failed to get Enum Type '{enumTypeString}' for state '{enumKey}'."); // Too noisy
                        return null;
                   }
                   return (System.Enum)Enum.Parse(enumType, enumKey);
              }
              catch (Exception e)
              {
                   Debug.LogError($"TiNpcData: Failed to parse enum '{enumKey}' of type '{enumTypeString}': {e.Message}");
                   return null;
              }
         }
    }

    // --- NEW: Serializable Dictionary Wrapper for Unique Decision Options ---
    // Unity cannot directly serialize Dictionaries, so we use a wrapper with a List of KeyValuePair structs.
    [System.Serializable]
    public class SerializableDecisionOptionDictionary
    {
        [System.Serializable]
        public struct KeyValuePair
        {
            [Tooltip("The ID of the Decision Point.")]
            public string decisionPointID;
            [Tooltip("The unique decision option for this NPC at this point.")]
            public DecisionOption decisionOption;
        }

        // --- FIX: Make the list public internal for direct manager access ---
        [SerializeField] public List<KeyValuePair> entries = new List<KeyValuePair>();
        // --- END FIX ---

        // Helper method to convert the list to a runtime Dictionary
        public Dictionary<string, DecisionOption> ToDictionary()
        {
            var dict = new Dictionary<string, DecisionOption>();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.decisionPointID))
                {
                    // Handle potential duplicate keys if needed, or let Add throw
                    if (!dict.ContainsKey(entry.decisionPointID))
                    {
                         dict.Add(entry.decisionPointID, entry.decisionOption);
                    } else {
                         Debug.LogWarning($"SerializableDecisionOptionDictionary: Duplicate key '{entry.decisionPointID}' found. Skipping duplicate entry.");
                    }
                } else {
                     Debug.LogWarning("SerializableDecisionOptionDictionary: Found entry with null or empty decisionPointID. Skipping.");
                }
            }
            return dict;
        }

        // Optional: Helper method to populate the list from a dictionary (for saving)
        // public void FromDictionary(Dictionary<string, DecisionOption> dict)
        // {
        //     entries.Clear();
        //     if (dict != null)
        //     {
        //         foreach (var pair in dict)
        //         {
        //             entries.Add(new KeyValuePair { decisionPointID = pair.Key, decisionOption = pair.Value });
        //         }
        //     }
        // }
    }
    // --- END NEW ---
}
// --- END OF FILE TiNpcData.cs ---