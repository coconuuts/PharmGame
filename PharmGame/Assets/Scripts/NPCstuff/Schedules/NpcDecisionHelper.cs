using UnityEngine;
using System; // Needed for System.Enum
using System.Collections.Generic; // Needed for List, Dictionary
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.Decisions; // Needed for DecisionPointSO, DecisionOption
using System.Linq; // Needed for ToList, OrderBy 
using Game.Navigation; // Needed for PathTransitionDetails, PathSO, WaypointManager

namespace Game.NPC.Decisions // Place in the same namespace as DecisionPointSO and DecisionOption
{
    /// <summary>
    /// Static helper class containing the core data-driven decision logic for TI NPCs.
    /// Accessed by states (active and basic) upon reaching a Decision Point.
    /// Evaluates conditional decisions first, and includes an override for pending prescriptions using PathTags.
    /// Returns PathTransitionDetails struct.
    /// </summary>
    public static class NpcDecisionHelper // Static class, no inheritance needed
    {
        /// <summary>
        /// Determines the next state for an NPC based on options configured
        /// on a Decision Point and potentially unique options on the NPC's data.
        /// Evaluates conditional decisions first, with an override for pending prescriptions based on PathGroupTag.
        /// </summary>
        /// <param name="tiData">The TiNpcData of the NPC making the decision.</param>
        /// <param name="decisionPoint">The DecisionPointSO reached.</param>
        /// <param name="tiManager">The TiNpcManager instance (needed for lookups/mappings).</param> 
        /// <returns>The PathTransitionDetails for the chosen outcome, or details with a null TargetStateEnum if no valid options.</returns>
        public static PathTransitionDetails MakeDecision(TiNpcData tiData, DecisionPointSO decisionPoint, TiNpcManager tiManager)
        {
            if (tiData == null)
            {
                Debug.LogError("NpcDecisionHelper: MakeDecision called with null TiNpcData!");
                return new PathTransitionDetails(null); // Return invalid details
            }
            if (decisionPoint == null)
            {
                Debug.LogError($"NpcDecisionHelper: MakeDecision called for NPC '{tiData.Id}' with null DecisionPointSO!");
                return new PathTransitionDetails(null); // Return invalid details
            }
            if (tiManager == null) 
            {
                Debug.LogError($"NpcDecisionHelper: MakeDecision called for NPC '{tiData.Id}' at point '{decisionPoint.PointID}' with null TiNpcManager!");
                return new PathTransitionDetails(null); // Return invalid details
            }

            Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' evaluating decision at point '{decisionPoint.PointID}'.");

            // Compile all available options at this decision point to check for tags and fallbacks
            List<DecisionOption> availableOptions = new List<DecisionOption>();

            if (decisionPoint.DecisionOptions != null)
            {
                availableOptions.AddRange(decisionPoint.DecisionOptions);
            }

            if (tiData.UniqueDecisionOptions != null && tiData.UniqueDecisionOptions.TryGetValue(decisionPoint.PointID, out DecisionOption uniqueOption))
            {
                availableOptions.Add(uniqueOption);
            }

            // --- Check for pending prescription and override decision using Path Tags ---
            // This check happens BEFORE standard random selection.
            if (tiData.pendingPrescription)
            {
                Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' has pending prescription. Looking for path with 'ToPrescription' tag.", tiData.NpcGameObject);

                // Look through available options for a path tagged "ToPrescription"
                DecisionOption? prescriptionOption = null;
                foreach (var option in availableOptions)
                {
                    if (option.PathAsset != null && option.PathAsset.PathGroupTag == "ToPrescription")
                    {
                        prescriptionOption = option;
                        break;
                    }
                }

                if (prescriptionOption.HasValue)
                {
                    Debug.Log($"NpcDecisionHelper: Overriding decision to follow path '{prescriptionOption.Value.PathAsset.PathID}' (Tag: ToPrescription).", tiData.NpcGameObject);
                    return prescriptionOption.Value.GetTransitionDetails(); // Return the overridden details
                }
                else
                {
                    Debug.LogWarning($"NpcDecisionHelper: NPC '{tiData.Id}' has pending prescription, but no option with 'ToPrescription' tag found at decision point '{decisionPoint.PointID}'. Falling back to standard decision logic.", tiData.NpcGameObject);
                    // Falls through to standard logic if the tag isn't found
                }
            }

            // --- Proceed with standard options ---
            
            // Filter out invalid options (Existing Logic)
            List<DecisionOption> validOptions = availableOptions
                .Where(option =>
                    option.TargetStateEnum != null &&
                    !(option.TargetStateEnum is Game.NPC.PathState pathState && pathState.Equals(Game.NPC.PathState.FollowPath) && option.PathAsset == null)
                )
                .ToList();

            if (validOptions.Count == 0)
            {
                Debug.LogWarning($"NpcDecisionHelper: No valid standard decision options available for NPC '{tiData.Id}' at Decision Point '{decisionPoint.PointID}' after evaluating conditionals and pending prescription override fallback! Cannot make a decision.");
                return new PathTransitionDetails(null); // No valid options found, return invalid details
            }

            // Randomly select one valid option (Existing Logic)
            int randomIndex = UnityEngine.Random.Range(0, validOptions.Count);
            DecisionOption chosenOption = validOptions[randomIndex];

            Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' at Decision Point '{decisionPoint.PointID}' chose standard option leading to state '{chosenOption.TargetStateEnum?.GetType().Name}.{chosenOption.TargetStateEnum?.ToString() ?? "NULL"}' (from {validOptions.Count} valid options).");

            // Return the chosen option's transition details (Existing Logic)
            return chosenOption.GetTransitionDetails();
        }

        // TODO: Add other helper methods for decision logic if needed (e.g., filtering by NPC type)
    }
}