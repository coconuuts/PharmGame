// --- START OF FILE PathTransitionDetails.cs ---

using UnityEngine;
using System; // Needed for System.Enum

namespace Game.Navigation // Place alongside PathSO
{
    /// <summary>
    /// Represents the details of a transition triggered by completing a path,
    /// including the target state and optional path details if the target is a path state.
    /// </summary>
    public struct PathTransitionDetails
    {
        /// <summary>
        /// The System.Enum key for the target state to transition to.
        /// Null if no valid transition is determined.
        /// </summary>
        public System.Enum TargetStateEnum;

        /// <summary>
        /// The PathSO asset to follow if TargetStateEnum is a Path State.
        /// Null otherwise.
        /// </summary>
        public PathSO PathAsset;

        /// <summary>
        /// The index of the waypoint to start the path from (0-based) if TargetStateEnum is a Path State.
        /// </summary>
        public int StartIndex;

        /// <summary>
        /// If true, follow the path in reverse from the start index if TargetStateEnum is a Path State.
        /// </summary>
        public bool FollowReverse;

        /// <summary>
        /// Creates PathTransitionDetails for a non-path state transition.
        /// </summary>
        public PathTransitionDetails(System.Enum targetStateEnum)
        {
            TargetStateEnum = targetStateEnum;
            PathAsset = null;
            StartIndex = 0;
            FollowReverse = false;
        }

        /// <summary>
        /// Creates PathTransitionDetails for a path state transition.
        /// </summary>
        public PathTransitionDetails(System.Enum targetStateEnum, PathSO pathAsset, int startIndex, bool followReverse)
        {
            // Ensure the target state is actually a path state if path details are provided
            if (targetStateEnum != null && targetStateEnum.GetType() == typeof(Game.NPC.PathState) && targetStateEnum.Equals(Game.NPC.PathState.FollowPath))
            {
                 TargetStateEnum = targetStateEnum;
                 PathAsset = pathAsset;
                 StartIndex = startIndex;
                 FollowReverse = followReverse;
            }
            else
            {
                 Debug.LogWarning($"PathTransitionDetails: Created with path details but target state '{targetStateEnum?.GetType().Name}.{targetStateEnum?.ToString() ?? "NULL"}' is not PathState.FollowPath. Path details will be ignored.");
                 TargetStateEnum = targetStateEnum;
                 PathAsset = null;
                 StartIndex = 0;
                 FollowReverse = false;
            }
        }

        /// <summary>
        /// Returns true if the TargetStateEnum is not null.
        /// </summary>
        public bool HasValidTransition => TargetStateEnum != null;
    }
}

// --- END OF FILE PathTransitionDetails.cs ---