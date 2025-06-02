// --- START OF FILE DecisionOption.cs ---

using UnityEngine;
using System; // Needed for System.Serializable and System.Enum
using Game.Navigation; // Needed for PathSO

namespace Game.NPC.Decisions // Place in the same namespace as DecisionPointSO
{
    /// <summary>
    /// Represents a single possible outcome or action an NPC can choose
    /// when reaching a Decision Point.
    /// </summary>
    [System.Serializable]
    public struct DecisionOption
    {
        [Tooltip("The Enum key for the target state to transition to.")]
        [SerializeField] private string targetStateEnumKey;
        [Tooltip("The Type name of the Enum key for the target state (e.g., Game.NPC.CustomerState, Game.NPC.GeneralState, Game.NPC.PathState).")]
        [SerializeField] private string targetStateEnumType;

        [Header("Path Settings (If Target State is a Path State)")]
        [Tooltip("Optional: The PathSO asset to follow if the target state is a Path State.")]
        [SerializeField] private PathSO pathAsset;
        [Tooltip("Optional: The index of the waypoint to start the path from (0-based).")]
        [SerializeField] private int startIndex;
        [Tooltip("Optional: If true, follow the path in reverse from the start index.")]
        [SerializeField] private bool followReverse;

        // Optional: Add weight, criteria, etc. here in the future
        // [Header("Selection Criteria")]
        // [Tooltip("Weight for random selection (higher = more likely).")]
        // [SerializeField] private float selectionWeight;
        // [Tooltip("Optional: Specific NPC types this option is available for.")]
        // [SerializeField] private List<string> allowedNpcTypeIDs;


        // --- Public Properties ---
        public string TargetStateEnumKey => targetStateEnumKey;
        public string TargetStateEnumType => targetStateEnumType;
        public PathSO PathAsset => pathAsset;
        public int StartIndex => startIndex;
        public bool FollowReverse => followReverse;
        // public float SelectionWeight => selectionWeight;


        /// <summary>
        /// Attempts to parse the stored state strings into a runtime System.Enum value.
        /// Returns null if parsing fails or strings are empty.
        /// </summary>
        public System.Enum TargetStateEnum
        {
            get
            {
                if (string.IsNullOrEmpty(targetStateEnumKey) || string.IsNullOrEmpty(targetStateEnumType)) return null;

                try
                {
                    Type enumType = Type.GetType(targetStateEnumType);
                    if (enumType == null || !enumType.IsEnum)
                    {
                        Debug.LogError($"DecisionOption: Failed to get Enum Type '{targetStateEnumType}' for state '{targetStateEnumKey}'.");
                        return null;
                    }
                    return (System.Enum)Enum.Parse(enumType, targetStateEnumKey);
                }
                catch (Exception e)
                {
                    Debug.LogError($"DecisionOption: Failed to parse enum '{targetStateEnumKey}' of type '{targetStateEnumType}': {e.Message}");
                    return null;
                }
            }
        }

        // Optional: Add validation in editor (requires MonoBehaviour or ScriptableObject context)
        // This struct itself cannot have OnValidate directly, but the SO containing it can iterate and validate.
    }
}
// --- END OF FILE DecisionOption.cs ---