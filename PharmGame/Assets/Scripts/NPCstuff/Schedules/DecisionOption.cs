using UnityEngine;
using System;
using Game.Navigation;

namespace Game.NPC.Decisions
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

        // --- Public Properties ---
        public string TargetStateEnumKey => targetStateEnumKey;
        public string TargetStateEnumType => targetStateEnumType;
        public PathSO PathAsset => pathAsset;
        public int StartIndex => startIndex;
        public bool FollowReverse => followReverse;

        // --- Constructor for Binary Reconstitution ---
        public DecisionOption(string targetStateEnumKey, string targetStateEnumType, PathSO pathAsset = null, int startIndex = 0, bool followReverse = false)
        {
            this.targetStateEnumKey = targetStateEnumKey;
            this.targetStateEnumType = targetStateEnumType;
            this.pathAsset = pathAsset;
            this.startIndex = startIndex;
            this.followReverse = followReverse;
        }

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

        /// <summary>
        /// Gets the PathTransitionDetails for this decision option.
        /// </summary>
        public PathTransitionDetails GetTransitionDetails()
        {
            System.Enum targetEnum = TargetStateEnum;
            if (targetEnum == null)
            {
                return new PathTransitionDetails(null);
            }

            if (targetEnum.GetType() == typeof(Game.NPC.PathState) && targetEnum.Equals(Game.NPC.PathState.FollowPath))
            {
                if (pathAsset == null)
                {
                    Debug.LogError($"DecisionOption: Target state is PathState.FollowPath but Path Asset is null! Cannot create valid path transition details.", PathAsset);
                    return new PathTransitionDetails(targetEnum, null, startIndex, followReverse);
                }
                return new PathTransitionDetails(targetEnum, pathAsset, startIndex, followReverse);
            }
            else
            {
                return new PathTransitionDetails(targetEnum);
            }
        }
    }
}