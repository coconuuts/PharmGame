// --- START OF FILE NpcDecisionHelper.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using System.Collections.Generic; // Needed for List, Dictionary
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.Decisions; // Needed for DecisionPointSO, DecisionOption
using System.Linq; // Needed for ToList, OrderBy (for deterministic random, optional)

namespace Game.NPC.Decisions // Place in the same namespace as DecisionPointSO and DecisionOption
{
    /// <summary>
    /// Static helper class containing the core data-driven decision logic for TI NPCs.
    /// Accessed by states (active and basic) upon reaching a Decision Point.
    /// </summary>
    public static class NpcDecisionHelper // Static class, no inheritance needed
    {
        /// <summary>
        /// Determines the next state for an NPC based on options configured
        /// on a Decision Point and potentially unique options on the NPC's data.
        /// </summary>
        /// <param name="tiData">The TiNpcData of the NPC making the decision.</param>
        /// <param name="decisionPoint">The DecisionPointSO reached.</param>
        /// <param name="tiManager">The TiNpcManager instance (needed for lookups/mappings).</param>
        /// <returns>The System.Enum key for the chosen target Active state, or null if no valid options.</returns>
        public static System.Enum MakeDecision(TiNpcData tiData, DecisionPointSO decisionPoint, TiNpcManager tiManager)
        {
            if (tiData == null)
            {
                Debug.LogError("NpcDecisionHelper: MakeDecision called with null TiNpcData!");
                return null;
            }
            if (decisionPoint == null)
            {
                Debug.LogError($"NpcDecisionHelper: MakeDecision called for NPC '{tiData.Id}' with null DecisionPointSO!");
                // In a real game, maybe transition to a safe state like Patrol or Idle.
                return null; // Caller must handle null return
            }
             if (tiManager == null)
             {
                  Debug.LogError($"NpcDecisionHelper: MakeDecision called for NPC '{tiData.Id}' at point '{decisionPoint.PointID}' with null TiNpcManager!");
                  return null;
             }


            List<DecisionOption> availableOptions = new List<DecisionOption>();

            // 1. Add standard options from the Decision Point asset
            if (decisionPoint.DecisionOptions != null)
            {
                availableOptions.AddRange(decisionPoint.DecisionOptions);
            }

            // 2. Check for and add unique option from the NPC's data
            // Note: uniqueDecisionOptions getter returns a runtime Dictionary
            if (tiData.UniqueDecisionOptions != null && tiData.UniqueDecisionOptions.TryGetValue(decisionPoint.PointID, out DecisionOption uniqueOption))
            {
                 // Optional: Check if the unique option should *replace* or *add* to standard options.
                 // For simplicity, let's assume it *adds* to the list of possibilities.
                 // If it should replace, clear availableOptions here before adding uniqueOption.
                 availableOptions.Add(uniqueOption);
            }

            // 3. Filter out invalid options (e.g., null state key/type, missing PathSO for path states)
            // This assumes the validation on the SO assets is a hint, but runtime check is needed.
            List<DecisionOption> validOptions = availableOptions
                .Where(option =>
                    option.TargetStateEnum != null && // Must have a valid, parsable target state enum
                    // If it's a PathState.FollowPath, it must have a PathAsset assigned
                    !(option.TargetStateEnum is PathState pathState && pathState.Equals(PathState.FollowPath) && option.PathAsset == null)
                    // TODO: Add checks for other states requiring specific data if needed
                )
                .ToList();

            if (validOptions.Count == 0)
            {
                Debug.LogWarning($"NpcDecisionHelper: No valid decision options available for NPC '{tiData.Id}' at Decision Point '{decisionPoint.PointID}'! Cannot make a decision.");
                // Return a fallback state here? Or let the caller handle null?
                // Returning null means the caller transitions to their fallback (Idle/Patrol).
                return null; // No valid options found
            }

            // 4. Randomly select one valid option
            // TODO: Implement weighting logic if selectionWeight is added to DecisionOption
            int randomIndex = UnityEngine.Random.Range(0, validOptions.Count);
            DecisionOption chosenOption = validOptions[randomIndex];

            Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' at Decision Point '{decisionPoint.PointID}' chose option leading to state '{chosenOption.TargetStateEnum?.GetType().Name}.{chosenOption.TargetStateEnum?.ToString() ?? "NULL"}' (from {validOptions.Count} valid options).");

            // 5. Return the chosen target state Enum (which is the Active state enum from the config)
            return chosenOption.TargetStateEnum;
        }

        // TODO: Add other helper methods for decision logic if needed (e.g., filtering by NPC type)
        // For now, the core logic is just collecting options and picking one randomly.
    }
}
// --- END OF FILE NpcDecisionHelper.cs ---