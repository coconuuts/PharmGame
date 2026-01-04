// --- START OF FILE TransientNpcTracker.cs (Modified for Phase 2.2) ---

using UnityEngine;
using System.Collections.Generic;
using System; // Needed for GUID structures and Enum
using Game.NPC; // Needed for NpcStateMachineRunner
using Systems.Inventory; // Needed for SerializableGuid
using System.Linq; // Needed for LINQ (ToDictionary)
using Game.Prescriptions;

namespace CustomerManagement.Tracking // Assuming this namespace structure
{
    /// <summary>
    /// Structure to hold serializable information about a single item the customer possesses.
    /// </summary>
    [System.Serializable]
    public struct SerializableTransientItemSnapshot
    {
        [Tooltip("The unique ID of the ItemDetails being held (used for reloading).")]
        public string ItemDetailsID; 

        [Tooltip("The quantity of this item held.")]
        public int Quantity;

        public SerializableTransientItemSnapshot(string id, int quantity)
        {
            ItemDetailsID = id;
            Quantity = quantity;
        }
    }


    /// <summary>
    /// Holds the complete snapshot state of a transient NPC at a specific moment in time.
    /// </summary>
    [System.Serializable]
    public class TransientNpcSnapshotData
    {
        [Tooltip("The unique identifier for this NPC.")]
        public SerializableGuid Guid;

        [Header("World & Time Data")]
        public Vector3 WorldPosition;
        public Quaternion WorldRotation;
        
        [Header("State Data")]
        // Stores the serialized representation of the Active State Enum
        public string StateEnumKey;
        public string StateEnumType;

        [Header("Queue Status")]
        public QueueType CurrentQueueType;
        public int AssignedQueueSpotIndex;

        [Header("Inventory Data")]
        public List<SerializableTransientItemSnapshot> InventoryContents = new List<SerializableTransientItemSnapshot>();

        [Header("Prescription Data")]
        public bool HasPendingPrescription;
        public PrescriptionOrder AssignedOrder;

        // Constructor for easier creation during snapshotting
        public TransientNpcSnapshotData(SerializableGuid guid)
        {
            Guid = guid;
            // Set safe defaults
            CurrentQueueType = QueueType.Main; 
            AssignedQueueSpotIndex = -1;
            HasPendingPrescription = false;
            AssignedOrder = new PrescriptionOrder(); // Default struct initialization
        }
        
        // Helper setter for prescription data
        public void SetPendingPrescriptionData(bool hasPrescription, PrescriptionOrder order)
        {
             HasPendingPrescription = hasPrescription;
             AssignedOrder = order;
        }
    }

    /// <summary>
    /// Singleton manager responsible for tracking active, transient customer GameObjects
    /// using a temporary, unique SerializableGuid assigned at initialization.
    /// </summary>
    public class TransientNpcTracker : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static TransientNpcTracker Instance { get; private set; }

        [Header("Tracking Status")]
        [Tooltip("The total number of transient NPCs currently being tracked.")]
        [SerializeField] private int activeTrackedCount = 0;

        // --- Internal State ---
        private Dictionary<SerializableGuid, NpcStateMachineRunner> activeTransientRunners;
        private Dictionary<SerializableGuid, TransientNpcSnapshotData> snapshotData; 

        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning("TransientNpcTracker: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Initialize dictionary
            activeTransientRunners = new Dictionary<SerializableGuid, NpcStateMachineRunner>();
            snapshotData = new Dictionary<SerializableGuid, TransientNpcSnapshotData>(); 
            activeTrackedCount = 0;
            Debug.Log("TransientNpcTracker: Awake completed. Dictionaries initialized.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Clean up data on destruction
                activeTransientRunners?.Clear();
                snapshotData?.Clear(); 
                Instance = null;
                Debug.Log("TransientNpcTracker: OnDestroy completed. Tracking dictionaries cleared.");
            }
        }

        /// <summary>
        /// Begins tracking an active transient NPC runner.
        /// </summary>
        public void StartTracking(SerializableGuid guid, NpcStateMachineRunner runner)
        {
            if (guid.Equals(SerializableGuid.Empty))
            {
                Debug.LogError($"TransientNpcTracker: Attempted to start tracking with an Empty GUID for runner {runner?.gameObject.name ?? "NULL"}.", runner?.gameObject);
                return;
            }

            if (activeTransientRunners.ContainsKey(guid))
            {
                Debug.LogWarning($"TransientNpcTracker: GUID {guid.ToHexString()} is already being tracked. Overwriting previous entry for runner {activeTransientRunners[guid]?.gameObject.name ?? "NULL"}.", runner?.gameObject);
            }

            activeTransientRunners[guid] = runner;
            activeTrackedCount = activeTransientRunners.Count;
            
            // Initialize a placeholder snapshot data entry
            if (!snapshotData.ContainsKey(guid))
            {
                 snapshotData.Add(guid, new TransientNpcSnapshotData(guid));
            }
            
            Debug.Log($"TransientNpcTracker: Started tracking NPC with GUID {guid.ToHexString()}. Total tracked: {activeTrackedCount}.");
        }

        /// <summary>
        /// Stops tracking a transient NPC when it is pooled/destroyed.
        /// </summary>
        public void StopTracking(SerializableGuid guid)
        {
            if (guid.Equals(SerializableGuid.Empty))
            {
                Debug.LogWarning("TransientNpcTracker: Attempted to stop tracking with an Empty GUID.");
                return;
            }

            if (activeTransientRunners.ContainsKey(guid))
            {
                NpcStateMachineRunner runner = activeTransientRunners[guid];
                activeTransientRunners.Remove(guid);
                activeTrackedCount = activeTransientRunners.Count;
                
                // Also remove snapshot data
                snapshotData.Remove(guid);

                Debug.Log($"TransientNpcTracker: Stopped tracking GUID {guid.ToHexString()}. Runner {runner?.gameObject.name ?? "NULL"} returned to pool. Total tracked: {activeTrackedCount}.");
            }
            else
            {
                Debug.LogWarning($"TransientNpcTracker: Attempted to stop tracking GUID {guid.ToHexString()}, but it was not found in the active tracking dictionary.");
            }
        }

        /// <summary>
        /// Retrieves an active transient runner by its GUID.
        /// </summary>
        public NpcStateMachineRunner GetRunnerByGuid(SerializableGuid guid)
        {
            if (activeTransientRunners.TryGetValue(guid, out NpcStateMachineRunner runner))
            {
                return runner;
            }
            return null;
        }

        /// <summary>
        /// Returns all currently tracked runners.
        /// </summary>
        public List<NpcStateMachineRunner> GetAllTrackedRunners()
        {
             return new List<NpcStateMachineRunner>(activeTransientRunners.Values);
        }

        /// <summary>
        /// Gathers the current state of all tracked transient NPCs and updates the snapshot data. 
        /// Commented out right now to prevent compilation errors. NpcStateMachineRunner needs addition of GatherSnapshotData() method first. 
        /// </summary>
     //    public void TakeSnapshot()
     //    {
     //         Debug.Log($"TransientNpcTracker: Initiating snapshot of {activeTransientRunners.Count} tracked NPCs.");
             
     //         foreach (var kvp in activeTransientRunners)
     //         {
     //              SerializableGuid guid = kvp.Key;
     //              NpcStateMachineRunner runner = kvp.Value;

     //              if (runner == null || !runner.isActiveAndEnabled)
     //              {
     //                   Debug.LogWarning($"TransientNpcTracker: Runner for GUID {guid.ToHexString()} is null or inactive. Cannot gather snapshot data. Skipping.", this);
     //                   continue;
     //              }

     //              // 1. Gather data from the runner
     //              TransientNpcSnapshotData snapshot = runner.GatherSnapshotData();
                  
     //              if (snapshot == null)
     //              {
     //                   Debug.LogWarning($"TransientNpcTracker: GatherSnapshotData returned null for runner {runner.gameObject.name}. Skipping update for this GUID.", runner.gameObject);
     //                   continue;
     //              }

     //              // 2. Update the internal storage dictionary
     //              if (snapshotData.ContainsKey(guid))
     //              {
     //                   snapshotData[guid] = snapshot;
     //              } else {
     //                   // This should not happen if StartTracking worked correctly, but handle defensively
     //                   snapshotData.Add(guid, snapshot);
     //                   Debug.LogWarning($"TransientNpcTracker: GUID {guid.ToHexString()} found in active runners but not in snapshot data. Adding new entry.", runner.gameObject);
     //              }
     //              Debug.Log($"TransientNpcTracker: Snapshot taken for {runner.gameObject.name} (State: {snapshot.StateEnumKey}).");
     //         }
     //         activeTrackedCount = activeTransientRunners.Count; // Update count based on active runners
     //         Debug.Log($"TransientNpcTracker: Snapshot process complete. {snapshotData.Count} entries updated.");
     //    }

        /// <summary>
        /// Retrieves the compiled snapshot data for all tracked transient NPCs.
        /// </summary>
        public List<TransientNpcSnapshotData> GetAllSnapshotData()
        {
             return new List<TransientNpcSnapshotData>(snapshotData.Values);
        }

        public void LoadSnapshots(List<TransientNpcSnapshotData> loadedSnapshots)
        {
             snapshotData.Clear();
             if (loadedSnapshots != null)
             {
                  foreach(var snapshot in loadedSnapshots)
                  {
                       snapshotData[snapshot.Guid] = snapshot;
                  }
             }
             Debug.Log($"TransientNpcTracker: Loaded {snapshotData.Count} snapshots.");
        }

        [Header("Debug Visualization (Editor Only)")]
        [SerializeField] private bool drawSnapshotGizmos = true;
        [SerializeField] private float snapshotGizmoRadius = 0.3f;

        private void OnDrawGizmos()
        {
             if (!drawSnapshotGizmos || snapshotData == null || snapshotData.Count == 0)
             {
                  return;
             }

             Gizmos.color = Color.cyan; // Color for transient snapshots

             foreach (var snapshot in snapshotData.Values)
             {
                  // Draw sphere at the last known position
                  Gizmos.DrawSphere(snapshot.WorldPosition, snapshotGizmoRadius);

                  // Optional: Draw state info near the NPC if the state is known
                  if (!string.IsNullOrEmpty(snapshot.StateEnumKey))
                  {
                       #if UNITY_EDITOR
                       // UnityEditor.Handles.Label(snapshot.WorldPosition + Vector3.up * (snapshotGizmoRadius + 0.1f), $"{snapshot.StateEnumKey.Split('.').Last()} @ {snapshot.WorldPosition.y:F1}");
                       #endif
                  }
             }
        }
    }
}
// --- END OF FILE TransientNpcTracker.cs ---