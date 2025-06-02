// --- START OF FILE DecisionPointSO.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List
using Game.Navigation; // Needed for WaypointManager (optional, but good practice)
using System.Linq; // Needed for LINQ (optional, for validation)
using System; // Needed for Exception, Type (for validation)

namespace Game.NPC.Decisions // A new namespace for decision-related assets/logic
{
    /// <summary>
    /// Defines a specific world location where True Identity NPCs can make
    /// data-driven decisions about their next state or path.
    /// </summary>
    [CreateAssetMenu(fileName = "DecisionPoint_", menuName = "NPC/Decisions/Decision Point", order = 1)]
    [HelpURL("https://your-documentation-link-here.com/DecisionPointSO")] // Placeholder
    public class DecisionPointSO : ScriptableObject
    {
        [Header("Decision Point Settings")]
        [Tooltip("A unique identifier for this Decision Point.")]
        [SerializeField] private string pointID;
        public string PointID => pointID; // Public getter for the unique ID

        [Tooltip("The ID of the Waypoint that marks the physical location of this Decision Point.")]
        [SerializeField] private string waypointID;
        public string WaypointID => waypointID; // Public getter for the associated Waypoint ID

        // --- Decision Options --- // <-- NEW HEADER
        [Header("Decision Options")]
        [Tooltip("The list of possible outcomes (states or paths) an NPC can choose from at this point.")]
        [SerializeField] private List<DecisionOption> decisionOptions = new List<DecisionOption>(); // <-- NEW FIELD
        public List<DecisionOption> DecisionOptions => decisionOptions; // Public getter
        // --- End Decision Options ---


        // Optional: Add validation in editor
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(pointID))
            {
                Debug.LogWarning($"DecisionPointSO '{name}': Point ID is empty or whitespace. Please assign a unique ID.", this);
            }
             if (string.IsNullOrWhiteSpace(waypointID))
             {
                  Debug.LogWarning($"DecisionPointSO '{name}': Waypoint ID is empty or whitespace. This Decision Point needs a linked physical location.", this);
             }

             // Optional: Validate Decision Options
             if (decisionOptions != null)
             {
                 for (int i = 0; i < decisionOptions.Count; i++)
                 {
                     DecisionOption option = decisionOptions[i];
                     if (string.IsNullOrWhiteSpace(option.TargetStateEnumKey) || string.IsNullOrWhiteSpace(option.TargetStateEnumType))
                     {
                         Debug.LogWarning($"DecisionPointSO '{name}': Decision Option at index {i} has empty Target State Key or Type. This option will be invalid.", this);
                     }
                     else
                     {
                         // Attempt to parse the enum to check if it's valid
                         System.Enum parsedEnum = option.TargetStateEnum;
                         if (parsedEnum == null)
                         {
                             Debug.LogError($"DecisionPointSO '{name}': Decision Option at index {i} has invalid Target State config: Key='{option.TargetStateEnumKey}', Type='{option.TargetStateEnumType}'. Enum parsing failed.", this);
                         }
                         else
                         {
                             // If it's a path state, check if a PathAsset is assigned
                             if (parsedEnum.GetType() == typeof(Game.NPC.PathState) && parsedEnum.Equals(Game.NPC.PathState.FollowPath))
                             {
                                 if (option.PathAsset == null)
                                 {
                                     Debug.LogWarning($"DecisionPointSO '{name}': Decision Option at index {i} targets PathState.FollowPath but has no Path Asset assigned.", this);
                                 }
                                 // TODO: Add validation for startIndex being within PathAsset bounds if PathAsset is assigned
                             }
                             // TODO: Add checks for other state types if they require specific data
                         }
                     }
                 }
             }
        }
        #endif
    }
}
// --- END OF FILE DecisionPointSO.cs ---