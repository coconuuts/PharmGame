// --- START OF FILE ProximityManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Needed for Coroutine
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.Spatial; // Needed for GridManager
using System; // Needed for Enum
using Game.NPC; // Needed for NpcStateMachineRunner
using Game.NPC.States; // Needed for NpcStateSO

namespace Game.Proximity
{
    /// <summary>
    /// Manages proximity checks for TI NPCs relative to the player.
    /// Categorizes NPCs into Near, Moderate, or Far zones and orchestrates
    /// activation, deactivation, and update throttling based on zone transitions.
    /// Relies on GridManager for efficient spatial queries.
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

        // Coroutine reference for the proximity check loop
        private Coroutine proximityCheckCoroutine;

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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Clear the zone tracking dictionary
                npcProximityZones.Clear();
                Instance = null;
                Debug.Log("ProximityManager: OnDestroy completed. Zone tracking cleared.");
            }
        }

        /// <summary>
        /// The periodic routine that checks NPC proximity to the player and updates their zones.
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
                            Debug.Log($"PROXIMITY {tiData.Id}: Triggering Activation.");
                            tiNpcManager.RequestActivateTiNpc(tiData);
                            // The runner will be available *after* RequestActivateTiNpc returns and the object is pooled.
                            // We need to wait a frame or handle this after activation is complete.
                            // A simple approach for now is to set the mode on the runner *immediately* after activation,
                            // assuming RequestActivateTiNpc completes synchronously enough to link the GameObject.
                            // A more robust approach might involve an event or callback from TiNpcManager.
                            // Let's try setting it immediately after the request for now.
                            // The runner will be linked to tiData.NpcGameObject by RequestActivateTiNpc.
                            // We'll set the mode below after the main transition logic.
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
                            }
                            else
                            {
                                 // This shouldn't happen if IsActiveGameObject is true, but defensive
                                 Debug.LogError($"PROXIMITY {tiData.Id}: Expected active NPC GameObject/Runner for deactivation, but found none! Forcing data cleanup.", tiData.NpcGameObject);
                                 // Attempt to force cleanup without proper deactivation flow
                                 tiData.UnlinkGameObject(); // Clear data link and flags
                                 gridManager?.RemoveItem(tiData, tiData.CurrentWorldPosition); // Attempt to remove from grid
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
                                  runner.SetUpdateMode(currentZone); // Set the new update mode
                             } else {
                                  Debug.LogError($"PROXIMITY {tiData.Id}: Expected active NPC GameObject/Runner for throttling change, but found none! Cannot set update mode.", tiData.NpcGameObject);
                             }
                        }
                        // --- END PHASE 2.3 & 3.2 LOGIC ---
                    }

                    // Update the tracked zone *after* processing transitions (unless deactivation was blocked by state)
                    npcProximityZones[tiData] = currentZone;

                    // --- NEW: Set initial update mode upon activation (Far -> Near/Moderate) ---
                    // This needs to happen *after* the NPC is activated and the runner is available.
                    // We check if the NPC just became active AND is in the Near or Moderate zone.
                    if (tiData.IsActiveGameObject && tiData.NpcGameObject != null && (currentZone == ProximityZone.Near || currentZone == ProximityZone.Moderate) && previousZone == ProximityZone.Far)
                    {
                         NpcStateMachineRunner runner = tiData.NpcGameObject.GetComponent<NpcStateMachineRunner>();
                         if (runner != null)
                         {
                              Debug.Log($"PROXIMITY {tiData.Id}: Setting initial update mode to {currentZone} after activation.");
                              runner.SetUpdateMode(currentZone);
                         } else {
                              Debug.LogError($"PROXIMITY {tiData.Id}: Activated NPC is missing Runner! Cannot set initial update mode.", tiData.NpcGameObject);
                         }
                    }
                    // --- END NEW ---
                }

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
                    if (npcProximityZones[tiData] == ProximityZone.Far && !tiData.IsActiveGameObject && distanceToPlayerSq > farRadius * farRadius)
                    {
                         // Debug.Log($"ProximityManager: Removing inactive Far NPC '{tiData.Id}' from tracking dictionary."); // Too noisy
                         npcProximityZones.Remove(tiData);
                    }
                }
                // --- END Cleanup ---
            }
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

        // --- DEBUG: Draw gizmos for proximity zones ---
        [Header("Debug Visualization")]
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