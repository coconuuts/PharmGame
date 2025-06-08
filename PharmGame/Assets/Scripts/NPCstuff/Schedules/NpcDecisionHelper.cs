// --- START OF FILE NpcDecisionHelper.cs (Modified to Override for Pending Prescription) ---

using UnityEngine;
using System; // Needed for System.Enum
using System.Collections.Generic; // Needed for List, Dictionary
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.Decisions; // Needed for DecisionPointSO, DecisionOption, ConditionalDecision, ConditionType
using System.Linq; // Needed for ToList, OrderBy (for deterministic random, optional)
using Game.Navigation; // Needed for PathTransitionDetails, PathSO, WaypointManager // <-- Added PathSO, WaypointManager

namespace Game.NPC.Decisions // Place in the same namespace as DecisionPointSO and DecisionOption
{
    /// <summary>
    /// Static helper class containing the core data-driven decision logic for TI NPCs.
    /// Accessed by states (active and basic) upon reaching a Decision Point.
    /// Now evaluates conditional decisions first, and includes a NEW override for pending prescriptions.
    /// Returns PathTransitionDetails struct.
    /// MODIFIED: Added override logic for pending prescriptions.
    /// </summary>
    public static class NpcDecisionHelper // Static class, no inheritance needed
    {
        /// <summary>
        /// Determines the next state for an NPC based on options configured
        /// on a Decision Point and potentially unique options on the NPC's data.
        /// Evaluates conditional decisions first, with a NEW override for pending prescriptions.
        /// </summary>
        /// <param name="tiData">The TiNpcData of the NPC making the decision.</param>
        /// <param name="decisionPoint">The DecisionPointSO reached.</param>
        /// <param name="tiManager">The TiNpcManager instance (needed for lookups/mappings, including WaypointManager).</param> // <-- Updated tooltip
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
             if (tiManager == null) // tiManager is now needed for WaypointManager access
             {
                  Debug.LogError($"NpcDecisionHelper: MakeDecision called for NPC '{tiData.Id}' at point '{decisionPoint.PointID}' with null TiNpcManager!");
                  return new PathTransitionDetails(null); // Return invalid details
             }


            Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' evaluating decision at point '{decisionPoint.PointID}'.");

            // --- NEW: Check for pending prescription and override decision ---
            // This check happens BEFORE any conditional or standard options.
            if (tiData.pendingPrescription)
            {
                Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' has pending prescription. Overriding decision to go to pharmacy path.", tiData.NpcGameObject);

                // Attempt to get the "PharmacyToPrescription" path using WaypointManager via TiNpcManager
                PathSO pharmacyPath = tiManager.WaypointManager?.GetPath("PharmacyToPrescription"); // Access WaypointManager via TiNpcManager

                if (pharmacyPath != null)
                {
                    // Create transition details for the path
                    // Assuming the pharmacy path starts at index 0 and is followed forward.
                    PathTransitionDetails overrideDetails = new PathTransitionDetails(Game.NPC.PathState.FollowPath, pharmacyPath, 0, false);
                    Debug.Log($"NpcDecisionHelper: Overriding decision to follow path '{pharmacyPath.PathID}'.", tiData.NpcGameObject);
                    return overrideDetails; // Return the overridden details
                }
                else
                {
                    Debug.LogError($"NpcDecisionHelper: NPC '{tiData.Id}' has pending prescription, but 'PharmacyToPrescription' PathSO not found via WaypointManager! Falling back to standard decision logic.", tiData.NpcGameObject);
                    // If the designated path is missing, fall through to the standard/conditional logic.
                    // This prevents the NPC from getting stuck if the required path asset is deleted.
                }
            }

            // --- Proceed with standard options ---
            List<DecisionOption> availableOptions = new List<DecisionOption>();

            // Add standard options from the Decision Point asset
            if (decisionPoint.DecisionOptions != null)
            {
                availableOptions.AddRange(decisionPoint.DecisionOptions);
            }

            // Check for and add unique option from the NPC's data
            if (tiData.UniqueDecisionOptions != null && tiData.UniqueDecisionOptions.TryGetValue(decisionPoint.PointID, out DecisionOption uniqueOption))
            {
                 availableOptions.Add(uniqueOption);
            }

            // 3. Filter out invalid options (Existing Logic)
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

            // 4. Randomly select one valid option (Existing Logic)
            int randomIndex = UnityEngine.Random.Range(0, validOptions.Count);
            DecisionOption chosenOption = validOptions[randomIndex];

            Debug.Log($"NpcDecisionHelper: NPC '{tiData.Id}' at Decision Point '{decisionPoint.PointID}' chose standard option leading to state '{chosenOption.TargetStateEnum?.GetType().Name}.{chosenOption.TargetStateEnum?.ToString() ?? "NULL"}' (from {validOptions.Count} valid options).");

            // 5. Return the chosen option's transition details (Existing Logic)
            return chosenOption.GetTransitionDetails();
        }

        // TODO: Add other helper methods for decision logic if needed (e.g., filtering by NPC type)
    }
}
// --- END OF FILE NpcDecisionHelper.cs (Modified to Override for Pending Prescription) ---