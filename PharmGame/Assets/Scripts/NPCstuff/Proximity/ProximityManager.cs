// --- START OF FILE ProximityManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Needed for Coroutine
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.Spatial; // Needed for GridManager
using System; // Needed for Enum
using Game.NPC; // Needed for NpcStateMachineRunner
using Game.NPC.States; // Needed for NpcStateSO
using System.Linq; // Added for Where
using Game.Utilities; // Needed for TimeRange // <-- NEW: Added using directive

namespace Game.Proximity
{
    /// <summary>
    /// Singleton manager responsible for managing proximity checks for TI NPCs relative to the player.
    /// Categorizes NPCs into Near, Moderate, or Far zones and orchestrates
    /// activation, deactivation, and update throttling based on zone transitions.
    /// Relies on GridManager for efficient spatial queries.
    /// Now also manages the ticking of active NPCs in Near and Moderate zones, and handles interrupted NPCs.
    /// Integrates time-based scheduling for activation.
    /// </summary>
    public class ProximityManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static ProximityManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Reference to the Player's Transform for proximity checks.")]
        [SerializeField] private Transform playerTransform; // Assign Player Transform
        // Reference to TiNpcManager (will be obtained in Awake/Start)
        private TiNpcManager tiNpcManager;
        // Reference to GridManager (will be obtained in Awake/Start)
        private GridManager gridManager;
        // Reference to ProximityManager (will be obtained in Awake/Start)
        // private ProximityManager proximityManager; // Removed self-reference, use Instance


        [Header("Proximity Settings")]
        [Tooltip("The radius around the player for the 'Near' zone (full updates).")]
        [SerializeField] internal float nearRadius = 10f; // New: Inner radius for 'Near' zone
        [Tooltip("The radius around the player for the 'Moderate' zone (throttled updates).")]
        [SerializeField] internal float moderateRadius = 20f; // New: Middle radius for 'Moderate' zone
        [Tooltip("The radius around the player for the 'Far' zone (simulation/inactive).")]
        [SerializeField] internal float farRadius = 30f; // Renamed/Adjusted: Outer radius for 'Far' zone

        [Tooltip("The interval (in seconds) to check proximity and update zones.")]
        [SerializeField] private float proximityCheckInterval = 1.0f; // Check proximity every second

        // Enum to define the proximity zones
        public enum ProximityZone { Far, Moderate, Near }

        // Internal dictionary to track the current zone of each TI NPC
        private Dictionary<TiNpcData, ProximityZone> npcProximityZones = new Dictionary<TiNpcData, ProximityZone>();

        // --- NEW: Lists to manage active Runners by zone ---
        private List<NpcStateMachineRunner> activeNearRunners;
        private List<NpcStateMachineRunner> activeModerateRunners;
        // --- NEW: List to manage interrupted Runners ---
        private List<NpcStateMachineRunner> interruptedRunners;
        // --- END NEW ---

        // --- NEW: Fields for Moderate Zone Ticking ---
        [Header("Moderate Zone Ticking")]
        [Tooltip("Update rate (in Hz) for active NPCs in the Moderate proximity zone.")]
        [SerializeField] private float moderateUpdateRateHz = 8f; // Example: 8 updates per second
        [Tooltip("The maximum number of Moderate NPCs to tick per frame when their timer elapses.")]
        [SerializeField] private int moderateBatchSize = 10; // Process 10 Moderate NPCs per tick

        private float fixedModerateDeltaTime; // Calculated from moderateUpdateRateHz
        private float moderateTickTimer; // Timer to track when to perform a moderate tick
        private int moderateTickIndex; // Index for round-robin ticking of moderate NPCs
        // --- END NEW ---


        // Coroutine reference for the proximity check loop
        private Coroutine proximityCheckCoroutine;
        // --- NEW: Coroutine reference for the moderate tick loop ---
        private Coroutine moderateTickCoroutine;
        // --- END NEW ---


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
                Debug.LogWarning("ProximityManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Basic validation for radii
            if (nearRadius <= 0) { Debug.LogError("ProximityManager: Near Radius must be positive! Setting to 1.", this); nearRadius = 1f; }
            if (moderateRadius <= nearRadius) { Debug.LogError("ProximityManager: Moderate Radius must be greater than Near Radius! Setting to Near + 1.", this); moderateRadius = nearRadius + 1f; }
            if (farRadius <= moderateRadius) { Debug.LogError("ProximityManager: Far Radius must be greater than Moderate Radius! Setting to Moderate + 1.", this); farRadius = moderateRadius + 1f; }

            // --- NEW: Initialize active runner lists ---
            activeNearRunners = new List<NpcStateMachineRunner>();
            activeModerateRunners = new List<NpcStateMachineRunner>();
            // --- NEW: Initialize interrupted runner list ---
            interruptedRunners = new List<NpcStateMachineRunner>();
            // --- END NEW ---

            // --- NEW: Initialize Moderate Zone Ticking fields ---
            if (moderateUpdateRateHz <= 0)
            {
                 Debug.LogWarning("ProximityManager: Moderate Update Rate Hz must be positive! Setting to 1.", this);
                 moderateUpdateRateHz = 1f;
            }
            fixedModerateDeltaTime = 1.0f / moderateUpdateRateHz;
            moderateTickTimer = 0f;
            moderateTickIndex = 0;
            if (moderateBatchSize <= 0)
            {
                 Debug.LogWarning("ProximityManager: Moderate Batch Size must be positive! Setting to 1.", this);
                 moderateBatchSize = 1;
            }
            // --- END NEW ---


            Debug.Log("ProximityManager: Awake completed.");
        }

        private void Start()
        {
            // Get references to required managers
            tiNpcManager = TiNpcManager.Instance;
            if (tiNpcManager == null)
            {
                Debug.LogError("ProximityManager: TiNpcManager instance not found! Cannot manage TI NPC proximity. Ensure TiNpcManager is in the scene.", this);
                // Do NOT disable the manager entirely, just functionality will be limited.
            }

            gridManager = GridManager.Instance;
            if (gridManager == null)
            {
                Debug.LogError("ProximityManager: GridManager instance not found! Cannot perform efficient spatial queries. Falling back to iterating all NPCs (INEFFICIENT!). Ensure GridManager is in the scene.", this); // Updated log
                // Proximity checks will fall back to iterating all NPCs (inefficient).
            }

            // Validate Player Transform
            if (playerTransform == null)
            {
                // Attempt to find Player by tag if not assigned
                GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assumes Player has "Player" tag
                if (playerGO != null) playerTransform = playerGO.transform;
                else Debug.LogError("ProximityManager: Player Transform not assigned and GameObject with tag 'Player' not found! Proximity checks will not work.", this);
            }

            Debug.Log("ProximityManager: Start completed.");
        }

        private void OnEnable()
        {
            // Start the proximity check coroutine if player is available. GridManager is optional for fallback.
            if (playerTransform != null) // GridManager is optional for fallback
            {
                proximityCheckCoroutine = StartCoroutine(ProximityCheckRoutine());
                Debug.Log("ProximityManager: Proximity check coroutine started.");
            }
            else
            {
                Debug.LogWarning("ProximityManager: Player Transform is null, cannot start proximity checks on OnEnable.");
            }

            // --- NEW: Start the moderate tick coroutine ---
            moderateTickCoroutine = StartCoroutine(ModerateTickRoutine());
            Debug.Log("ProximityManager: Moderate tick coroutine started.");
            // --- END NEW ---
        }

        private void OnDisable()
        {
            // Stop the proximity check coroutine
            if (proximityCheckCoroutine != null)
            {
                Debug.Log("ProximityManager: Stopping proximity check coroutine on OnDisable.");
                StopCoroutine(proximityCheckCoroutine);
                proximityCheckCoroutine = null;
            }
            // --- NEW: Stop the moderate tick coroutine ---
            if (moderateTickCoroutine != null)
            {
                Debug.Log("ProximityManager: Stopping moderate tick coroutine on OnDisable.");
                StopCoroutine(moderateTickCoroutine);
                moderateTickCoroutine = null;
            }
            // --- END NEW ---

            // --- NEW: Clear active runner lists on disable ---
            activeNearRunners.Clear();
            activeModerateRunners.Clear();
            // --- NEW: Clear interrupted runner list on disable ---
            interruptedRunners.Clear();
            // --- END NEW ---
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Clear the zone tracking dictionary
                npcProximityZones.Clear();
                // --- NEW: Clear active runner lists on destroy ---
                activeNearRunners.Clear();
                activeModerateRunners.Clear();
                // --- NEW: Clear interrupted runner list on destroy ---
                interruptedRunners.Clear();
                // --- END NEW ---
                Instance = null;
                Debug.Log("ProximityManager: OnDestroy completed. Zone tracking and active lists cleared.");
            }
        }

        /// <summary>
        /// The periodic routine that checks NPC proximity to the player and updates their zones.
        /// Also handles time-based activation/deactivation checks.
        /// </summary>
        private IEnumerator ProximityCheckRoutine()
        {
            while (true) // Loop indefinitely
            {
                yield return new WaitForSeconds(proximityCheckInterval);

                if (playerTransform == null)
                {
                    Debug.LogWarning("ProximityManager: Player Transform is null, skipping proximity checks.");
                    yield return new WaitForSeconds(proximityCheckInterval * 5); // Wait longer if player is missing
                    continue; // Skip check logic
                }
                 if (tiNpcManager == null)
                 {
                      Debug.LogError("ProximityManager: TiNpcManager is null! Cannot manage NPC activation/deactivation.", this);
                      yield return new WaitForSeconds(proximityCheckInterval * 5);
                      continue; // Cannot proceed
                 }
                 // --- NEW: Check TimeManager dependency ---
                 if (TimeManager.Instance == null)
                 {
                      Debug.LogError("ProximityManager: TimeManager instance is null! Cannot perform time-based scheduling checks.", this);
                      yield return new WaitForSeconds(proximityCheckInterval * 5);
                      continue; // Cannot proceed
                 }
                 DateTime currentTime = TimeManager.Instance.CurrentGameTime; // Get current game time
                 // --- END NEW ---


                Vector3 playerPosition = playerTransform.position;
                List<TiNpcData> relevantNpcs;

                // --- Use GridManager for efficient query or fallback ---
                if (gridManager != null)
                {
                    // Query a radius slightly larger than the farthest zone radius to catch NPCs just outside.
                    float queryRadius = farRadius + gridManager.cellSize; // Query slightly beyond far radius
                    relevantNpcs = gridManager.QueryItemsInRadius(playerPosition, queryRadius);
                    // Debug.Log($"ProximityManager: Queried grid for NPCs within {queryRadius:F2}m. Found {relevantNpcs.Count} potential NPCs.");
                }
                else
                {
                    // Fallback: Iterate all NPCs if GridManager is missing (INEFFICIENT!)
                    Debug.LogError("ProximityManager: GridManager is null! Falling back to iterating all NPCs for proximity check (INEFFICIENT!).", this);
                    // Get ALL managed NPCs (both active and inactive)
                    relevantNpcs = new List<TiNpcData>(tiNpcManager.allTiNpcs.Values); // Get all directly
                }

                // --- Categorize NPCs and check for zone transitions ---
                // Keep track of NPCs that were tracked but are no longer in the query results
                HashSet<TiNpcData> npcsInQueryResults = new HashSet<TiNpcData>(relevantNpcs);
                List<TiNpcData> npcsToProcess = new List<TiNpcData>(relevantNpcs); // Start with NPCs found in query

                // Add any previously tracked NPCs that were NOT in the query results this tick
                // This handles NPCs that moved outside the query radius
                List<TiNpcData> trackedKeys = new List<TiNpcData>(npcProximityZones.Keys); // Get a copy of keys to iterate
                foreach(var trackedNpc in trackedKeys)
                {
                    if (!npcsInQueryResults.Contains(trackedNpc))
                    {
                        npcsToProcess.Add(trackedNpc);
                    }
                }

                // --- NEW: Temporary lists to track runners to add/remove this tick ---
                List<NpcStateMachineRunner> runnersToAddNear = new List<NpcStateMachineRunner>();
                List<NpcStateMachineRunner> runnersToAddModerate = new List<NpcStateMachineRunner>();
                List<NpcStateMachineRunner> runnersToRemove = new List<NpcStateMachineRunner>(); // Runners that are becoming Far or invalid
                // --- END NEW ---


                foreach (var tiData in npcsToProcess) // Iterate over the combined list
                {
                    // Determine the NPC's actual world position
                    // If the NPC is active, use its GameObject position
                    // If inactive, use its data's stored position
                    Vector3 npcPosition = tiData.IsActiveGameObject && tiData.NpcGameObject != null ?
                                            tiData.NpcGameObject.transform.position :
                                            tiData.CurrentWorldPosition;

                    float distanceToPlayerSq = (npcPosition - playerPosition).sqrMagnitude;

                    ProximityZone currentZone;
                    if (distanceToPlayerSq <= nearRadius * nearRadius)
                    {
                        currentZone = ProximityZone.Near;
                    }
                    else if (distanceToPlayerSq <= moderateRadius * moderateRadius)
                    {
                        currentZone = ProximityZone.Moderate;
                    }
                    else // Distance is greater than moderateRadius
                    {
                        // This includes NPCs between moderateRadius and farRadius, AND those outside farRadius
                        currentZone = ProximityZone.Far;
                    }


                    // Get the previous zone (default to Far if not tracked yet)
                    // If the NPC was not in the query results, it's implicitly Far now.
                    ProximityZone previousZone;
                    if (!npcProximityZones.TryGetValue(tiData, out previousZone))
                    {
                         // If not tracked, assume it was Far (or just loaded)
                         previousZone = ProximityZone.Far;
                    }


                    // Check for zone transition
                    if (currentZone != previousZone)
                    {
                        Debug.Log($"PROXIMITY {tiData.Id}: Zone Transition: {previousZone} -> {currentZone} (Distance: {Mathf.Sqrt(distanceToPlayerSq):F2}m)");

                        // --- PHASE 2.3 & 3.2 LOGIC: Trigger Activation/Deactivation/Throttling ---

                        // Transition from Far to Moderate or Near -> ACTIVATE
                        if (previousZone == ProximityZone.Far && (currentZone == ProximityZone.Moderate || currentZone == ProximityZone.Near))
                        {
                            // --- NEW: Check if within startDay schedule before activating ---
                            if (tiData.startDay.IsWithinRange(currentTime))
                            {
                                Debug.Log($"PROXIMITY {tiData.Id}: Triggering Activation (Within startDay schedule {tiData.startDay}).");
                                tiNpcManager.RequestActivateTiNpc(tiData);
                                // The runner will be available *after* RequestActivateTiNpc returns and the object is pooled.
                                // We'll add the runner to the list below after the main transition logic.
                            }
                            else
                            {
                                // NPC is within proximity but outside their schedule. Keep them inactive/Far.
                                Debug.Log($"PROXIMITY {tiData.Id}: Skipping Activation. Outside startDay schedule {tiData.startDay} (Current Time: {currentTime:HH:mm}).", tiData.NpcGameObject);
                                // Keep currentZone as Far in the tracking dictionary for this NPC
                                npcProximityZones[tiData] = ProximityZone.Far; // Explicitly ensure it stays Far in tracking
                                continue; // Skip further processing for this NPC this tick
                            }
                            // --- END NEW ---
                        }
                        // Transition from Moderate or Near to Far -> DEACTIVATE
                        else if ((previousZone == ProximityZone.Moderate || previousZone == ProximityZone.Near) && currentZone == ProximityZone.Far)
                        {
                            // Need the active runner to trigger deactivation flow
                            NpcStateMachineRunner runner = tiData.NpcGameObject?.GetComponent<NpcStateMachineRunner>();
                            if (runner != null)
                            {
                                 // Check if the NPC is in a state that should prevent deactivation (e.g., Combat, Transaction)
                                 // This check is now primarily here in ProximityManager, before requesting deactivation.
                                 NpcStateSO currentStateSO = runner.GetCurrentState();
                                 if (currentStateSO != null && !currentStateSO.IsInterruptible)
                                 {
                                      Debug.Log($"PROXIMITY {tiData.Id}: Deactivation request skipped. Current state '{currentStateSO.name}' is not interruptible.");
                                      // Keep the NPC in its current zone for now, don't update npcProximityZones[tiData] to Far
                                      // This prevents spamming deactivation attempts every tick while in a non-interruptible state.
                                      // The next tick will re-evaluate.
                                      continue; // Skip updating zone and processing for this NPC this tick
                                 }

                                 Debug.Log($"PROXIMITY {tiData.Id}: Triggering Deactivation.");
                                 tiNpcManager.RequestDeactivateTiNpc(tiData, runner);
                                 // The runner GameObject will be pooled shortly after this.
                                 // Add the runner to the removal list
                                 runnersToRemove.Add(runner); // Add to list for removal from active lists
                            }
                            else
                            {
                                 // This shouldn't happen if IsActiveGameObject is true, but defensive
                                 Debug.LogError($"PROXIMITY {tiData.Id}: Expected active NPC GameObject/Runner for deactivation, but found none! Forcing data cleanup.", tiData.NpcGameObject);
                                 // Attempt to force cleanup without proper deactivation flow
                                 tiData.UnlinkGameObject(); // Clear data link and flags
                                 gridManager?.RemoveItem(tiData, tiData.CurrentWorldPosition); // Attempt to remove from grid
                                 // No runner to add to runnersToRemove list in this case
                            }
                        }
                        // Transitions between Near and Moderate zones -> THROTTLE CHANGE
                        else if ((previousZone == ProximityZone.Near && currentZone == ProximityZone.Moderate) ||
                                 (previousZone == ProximityZone.Moderate && currentZone == ProximityZone.Near))
                        {
                             // This is a throttling change for an already active NPC
                             NpcStateMachineRunner runner = tiData.NpcGameObject?.GetComponent<NpcStateMachineRunner>();
                             if (runner != null)
                             {
                                  Debug.Log($"PROXIMITY {tiData.Id}: Triggering Throttling Change.");
                                  // REMOVED: runner.SetUpdateMode(currentZone); // Set the new update mode
                                  // This logic will be replaced by adding/removing the runner from lists in ProximityManager
                                  // Add the runner to the appropriate list for this tick's update
                                  if (currentZone == ProximityZone.Near) runnersToAddNear.Add(runner);
                                  else if (currentZone == ProximityZone.Moderate) runnersToAddModerate.Add(runner);
                                  // Also add to removal list from old zone
                                  runnersToRemove.Add(runner); // Add to list for removal from active lists
                             } else {
                                  Debug.LogError($"PROXIMITY {tiData.Id}: Expected active NPC GameObject/Runner for throttling change, but found none! Cannot set update mode.", tiData.NpcGameObject);
                             }
                        }
                        // --- END PHASE 2.3 & 3.2 LOGIC ---
                    }

                    // Update the tracked zone *after* processing transitions (unless deactivation was blocked by state or schedule)
                    // Ensure we only update if we didn't skip processing this NPC
                    if (npcsToProcess.Contains(tiData)) // Check if we are still processing this NPC
                    {
                         npcProximityZones[tiData] = currentZone;
                    }


                    // --- NEW: Check endDay schedule for active/simulated NPCs ---
                    // This check happens regardless of zone transition, for NPCs currently tracked
                    if (tiData.IsActiveGameObject || (npcProximityZones.TryGetValue(tiData, out ProximityZone trackedZone) && trackedZone != ProximityZone.Far))
                    {
                         bool shouldEndDay = tiData.endDay.IsWithinRange(currentTime);
                         if (shouldEndDay && !tiData.isEndingDay)
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Entered endDay schedule {tiData.endDay} (Current Time: {currentTime:HH:mm}). Setting isEndingDay flag to true.", tiData.NpcGameObject);
                              tiData.isEndingDay = true; // Set the flag
                         }
                         else if (!shouldEndDay && tiData.isEndingDay)
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Exited endDay schedule {tiData.endDay} (Current Time: {currentTime:HH:mm}). Setting isEndingDay flag to false.", tiData.NpcGameObject);
                              tiData.isEndingDay = false; // Clear the flag
                         }
                    }
                    // --- END NEW ---


                    // --- NEW: Add newly activated runners to the lists ---
                    // This handles the case where Far -> Near/Moderate activation just happened.
                    // The runner should now exist and be linked.
                    // This needs to happen *after* RequestActivateTiNpc and the tiData.LinkGameObject call.
                    // We also need to check if the activation was *not* skipped due to schedule.
                    if (previousZone == ProximityZone.Far && (currentZone == ProximityZone.Moderate || currentZone == ProximityZone.Near) && tiData.IsActiveGameObject && tiData.NpcGameObject != null) // Check if activation actually happened
                    {
                         NpcStateMachineRunner runner = tiData.NpcGameObject.GetComponent<NpcStateMachineRunner>(); // Get the runner from the newly linked GO
                         if (runner != null)
                         {
                              if (currentZone == ProximityZone.Near) runnersToAddNear.Add(runner);
                              else if (currentZone == ProximityZone.Moderate) runnersToAddModerate.Add(runner);
                              Debug.Log($"PROXIMITY {tiData.Id}: Activated. Adding runner to {currentZone} list.", runner.gameObject);
                         } else {
                              Debug.LogError($"PROXIMITY {tiData.Id}: Activated but Runner is null! Cannot add to active lists.", tiData.NpcGameObject);
                         }
                    }
                    // --- END NEW ---
                }

                // --- NEW: Process the lists of runners to add/remove from active lists ---

                // Remove runners from their old lists
                foreach(var runner in runnersToRemove)
                {
                    if (activeNearRunners.Contains(runner)) activeNearRunners.Remove(runner);
                    if (activeModerateRunners.Contains(runner)) activeModerateRunners.Remove(runner);
                    // Interrupted list removal is handled by ExitInterruptionMode (Phase 3)
                }

                // Add runners to their new lists (avoiding duplicates)
                foreach(var runner in runnersToAddNear)
                {
                    if (!activeNearRunners.Contains(runner)) activeNearRunners.Add(runner);
                }
                 foreach(var runner in runnersToAddModerate)
                {
                    if (!activeModerateRunners.Contains(runner)) activeModerateRunners.Add(runner);
                }

                // --- END NEW ---


                // --- Clean up tracking for NPCs that are now truly outside the far radius and not active ---
                // If an NPC transitions to Far and is successfully deactivated/pooled,
                // its IsActiveGameObject flag becomes false.
                // We can remove truly 'Far' and inactive NPCs from the tracking dictionary
                // to keep it from growing indefinitely if NPCs move far away and stay there.
                // Iterate over a copy of keys to allow modification of the dictionary.
                List<TiNpcData> trackedKeysAfterProcessing = new List<TiNpcData>(npcProximityZones.Keys);
                foreach(var tiData in trackedKeysAfterProcessing)
                {
                    // Re-check the zone based on current position, as it might have changed during processing
                     Vector3 npcPosition = tiData.IsActiveGameObject && tiData.NpcGameObject != null ?
                                            tiData.NpcGameObject.transform.position :
                                            tiData.CurrentWorldPosition;
                     float distanceToPlayerSq = (npcPosition - playerPosition).sqrMagnitude;

                    // If the NPC is currently tracked as Far AND is inactive AND is outside the far radius...
                    if (npcProximityZones.TryGetValue(tiData, out ProximityZone trackedZone) && trackedZone == ProximityZone.Far && !tiData.IsActiveGameObject && distanceToPlayerSq > farRadius * farRadius)
                    {
                         // Debug.Log($"ProximityManager: Removing inactive Far NPC '{tiData.Id}' from tracking dictionary."); // Too noisy
                         npcProximityZones.Remove(tiData);
                    }
                }
                // --- END Cleanup ---
            }
        }

        /// <summary>
        /// The coroutine that ticks active NPCs in the Moderate zone in a round-robin fashion.
        /// </summary>
        private IEnumerator ModerateTickRoutine()
        {
            while (true)
            {
                // Wait for the fixed delta time before processing the next batch
                yield return new WaitForSeconds(fixedModerateDeltaTime);

                // --- Process a batch of Moderate Runners ---
                // Ensure the list is not null and has elements before processing
                if (activeModerateRunners == null || activeModerateRunners.Count == 0)
                {
                    // If the list is empty, reset index and continue waiting
                    moderateTickIndex = 0;
                    continue;
                }

                int totalModerateCount = activeModerateRunners.Count;

                // Wrap the index if it exceeds the total count
                if (moderateTickIndex >= totalModerateCount)
                {
                    moderateTickIndex = 0;
                }

                // Get the batch of runners to process in this tick
                // Use Skip and Take, handling the case where the batch wraps around the end of the list
                List<NpcStateMachineRunner> currentBatch;
                if (moderateTickIndex + moderateBatchSize <= totalModerateCount)
                {
                    // Simple case: batch is entirely within the remaining list
                    currentBatch = activeModerateRunners.Skip(moderateTickIndex).Take(moderateBatchSize).ToList();
                }
                else
                {
                    // Batch wraps around: take remaining from current index, then take from the start
                    int remainingInList = totalModerateCount - moderateTickIndex;
                    int neededFromStart = moderateBatchSize - remainingInList;
                    currentBatch = activeModerateRunners.Skip(moderateTickIndex).Take(remainingInList).ToList();
                    currentBatch.AddRange(activeModerateRunners.Take(neededFromStart));
                }


                // Process the batch
                foreach (var runner in currentBatch)
                {
                    // Double-check runner validity before ticking
                    if (runner != null && runner.isActiveAndEnabled)
                    {
                        // --- Call the Runner's ThrottledTick method ---
                        runner.ThrottledTick(fixedModerateDeltaTime); // Use the fixed delta time
                        // --- END Call ---
                    }
                    // Note: If a runner is null or inactive here, it means it was likely
                    // deactivated/pooled since the last ProximityCheckRoutine updated the list.
                    // The list cleanup will happen in the next ProximityCheckRoutine.
                }

                // Advance the index by the batch size
                moderateTickIndex += moderateBatchSize; // Use the actual batch size processed

                // Wrap the index again based on the current total count (it might have changed)
                totalModerateCount = activeModerateRunners.Count; // Re-get count
                if (totalModerateCount > 0)
                {
                    moderateTickIndex %= totalModerateCount;
                }
                else
                {
                    moderateTickIndex = 0; // Reset if list is now empty
                }

                // Debug.Log($"PROXIMITY: Ticked {currentBatch.Count} Moderate NPCs. Total Moderate: {totalModerateCount}. Next batch starts at index {moderateTickIndex}."); // Too noisy
                // The coroutine automatically waits for fixedModerateDeltaTime before the next iteration.
            }
        }

        /// <summary>
        /// Removes a runner from the active Near and Moderate lists.
        /// Called by TiNpcManager when a runner is being returned to the pool.
        /// </summary>
        /// <param name="runner">The runner to remove.</param>
        public void RemoveRunnerFromActiveLists(NpcStateMachineRunner runner)
        {
             if (runner == null) return;

             // Remove from both lists (Remove handles if it's not present)
             activeNearRunners.Remove(runner);
             activeModerateRunners.Remove(runner);
             // Interrupted list removal is handled by ExitInterruptionMode (Phase 3)
        }


        /// <summary>
        /// Public method to get the current proximity zone for a specific NPC.
        /// Useful for other systems that might need this information.
        /// Returns ProximityZone.Far if the NPC is not currently tracked.
        /// </summary>
        public ProximityZone GetNpcProximityZone(TiNpcData npcData)
        {
            if (npcData == null) return ProximityZone.Far; // Default for null data
            if (npcProximityZones.TryGetValue(npcData, out ProximityZone zone))
            {
                return zone;
            }
            return ProximityZone.Far; // Default if NPC is not currently tracked (e.g., just loaded, or outside far radius)
        }

        // --- NEW: Methods for Interruption Handler to notify ProximityManager ---
        /// <summary>
        /// Called by NpcInterruptionHandler when an interruption state begins.
        /// Notifies ProximityManager that this runner needs full updates.
        /// </summary>
        public void EnterInterruptionMode(NpcStateMachineRunner runner)
        {
             if (runner == null) return;

             Debug.Log($"PROXIMITY {runner.gameObject.name}: Notified of Interruption Start. Moving to Interrupted list.", runner.gameObject);

             // Remove from zone lists if present
             activeNearRunners.Remove(runner);
             activeModerateRunners.Remove(runner);

             // Add to interrupted list (Add handles if already present, but shouldn't be)
             if (!interruptedRunners.Contains(runner))
             {
                 interruptedRunners.Add(runner);
             }
        }

        /// <summary>
        /// Called by NpcInterruptionHandler when an interruption state ends.
        /// Notifies ProximityManager that this runner can return to zone-based updates.
        /// </summary>
        public void ExitInterruptionMode(NpcStateMachineRunner runner)
        {
             if (runner == null) return;

             Debug.Log($"PROXIMITY {runner.gameObject.name}: Notified of Interruption End. Moving from Interrupted list back to zone list.", runner.gameObject);

             // Remove from interrupted list
             interruptedRunners.Remove(runner);

             // Determine current zone based on position
             ProximityZone currentZone = ProximityZone.Far; // Default if position check fails
             if (playerTransform != null && runner.isActiveAndEnabled) // Need player and active runner
             {
                 float distanceToPlayerSq = (runner.transform.position - playerTransform.position).sqrMagnitude;
                 if (distanceToPlayerSq <= nearRadius * nearRadius)
                 {
                     currentZone = ProximityZone.Near;
                 }
                 else if (distanceToPlayerSq <= moderateRadius * moderateRadius)
                 {
                     currentZone = ProximityZone.Moderate;
                 }
                 // If distance > moderateRadius, it's Far, and we don't add it back to active lists.
             } else if (runner != null)
             {
                  Debug.LogWarning($"PROXIMITY {runner.gameObject.name}: Player Transform or Runner not active/enabled during ExitInterruptionMode zone check. Cannot determine zone, not adding back to active lists.", runner.gameObject);
             }


             // Add back to the appropriate zone list if not Far
             if (currentZone == ProximityZone.Near)
             {
                 if (!activeNearRunners.Contains(runner)) activeNearRunners.Add(runner);
                 Debug.Log($"PROXIMITY {runner.gameObject.name}: Returned to Near zone list.", runner.gameObject);
             }
             else if (currentZone == ProximityZone.Moderate)
             {
                 if (!activeModerateRunners.Contains(runner)) activeModerateRunners.Add(runner);
                 Debug.Log($"PROXIMITY {runner.gameObject.name}: Returned to Moderate zone list.", runner.gameObject);
             }
             else
             {
                  // If currentZone is Far, the NPC should be deactivated/pooled.
                  // The ProximityCheckRoutine will handle triggering deactivation if needed.
                  // We just ensure it's removed from interrupted list here.
                  Debug.Log($"PROXIMITY {runner.gameObject.name}: Interruption ended, but NPC is now in Far zone. Not adding back to active lists.", runner.gameObject);
             }
        }
        // --- END NEW ---


        // --- DEBUG: Draw gizmos for proximity zones ---
        [Header("Debug Visualization (Editor Only)")]
        [Tooltip("Enable drawing gizmos for tracked TI NPCs in the Scene view based on their zone.")]
        [SerializeField] private bool drawZoneGizmos = true;
        [Tooltip("Color for Near zone gizmos.")]
        [SerializeField] private Color nearGizmoColor = Color.green;
        [Tooltip("Color for Moderate zone gizmos.")]
        [SerializeField] private Color moderateGizmoColor = Color.yellow;
        [Tooltip("Color for Far zone gizmos.")]
        [SerializeField] private Color farGizmoColor = Color.red;
        [Tooltip("Radius of the zone gizmo spheres.")]
        [SerializeField] private float zoneGizmoRadius = 0.3f;


        private void OnDrawGizmos()
        {
             if (!drawZoneGizmos || npcProximityZones == null || npcProximityZones.Count == 0)
             {
                 return;
             }

             // Iterate over the currently tracked NPCs and their zones
             foreach (var pair in npcProximityZones)
             {
                 TiNpcData tiData = pair.Key;
                 ProximityZone zone = pair.Value;

                 // Determine position (use GameObject position if active, data position if inactive)
                 Vector3 npcPosition = tiData.IsActiveGameObject && tiData.NpcGameObject != null ?
                                         tiData.NpcGameObject.transform.position :
                                         tiData.CurrentWorldPosition;

                 // Set color based on zone
                 switch (zone)
                 {
                     case ProximityZone.Near:
                         Gizmos.color = nearGizmoColor;
                         break;
                     case ProximityZone.Moderate:
                         Gizmos.color = moderateGizmoColor;
                         break;
                     case ProximityZone.Far:
                         Gizmos.color = farGizmoColor;
                         break;
                 }

                 // Draw sphere gizmo
                 Gizmos.DrawSphere(npcPosition, zoneGizmoRadius);

                 // Optional: Draw zone name as text (Requires UnityEditor and Handles)
                 // try { UnityEditor.Handles.Label(npcPosition + Vector3.up * (zoneGizmoRadius + 0.1f), zone.ToString()); } catch {}
             }
        }
        // --- END DEBUG ---
    }
}
// --- END OF FILE ProximityManager.cs ---