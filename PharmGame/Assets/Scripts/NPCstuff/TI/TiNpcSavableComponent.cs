using UnityEngine;
using System;
using Systems.Persistence;
using Systems.Inventory; // Needed for SerializableGuid if referenced
using Game.NPC.TI;
using Game.NPC;
using CustomerManagement;

namespace Game.NPC.TI
{
    /// <summary>
    /// Component attached to an active True Identity NPC GameObject.
    /// It allows the generic SaveLoadSystem to trigger a "Flush" of this object's state
    /// into its core TiNpcData record before the game is saved.
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))]
    public class TiNpcSavableComponent : MonoBehaviour, ISavableComponent
    {
        [Header("TI Persistence Link")]
        [Tooltip("The persistent string ID that links this active GameObject to its TiNpcData record.")]
        private string tiNpcDataStringId;

        [Tooltip("The GUID assigned to this instance for scene-based saving/loading.")]
        [SerializeField] private SerializableGuid instanceGuid = SerializableGuid.Empty;

        public SerializableGuid Id => instanceGuid;

        private NpcStateMachineRunner runner;
        private TiNpcManager tiNpcManager;

        void Awake()
        {
            runner = GetComponent<NpcStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogError($"TiNpcSavableComponent on {gameObject.name}: Missing NpcStateMachineRunner! Cannot function.", this);
                enabled = false;
                return;
            }
        }

        void Start()
        {
             tiNpcManager = TiNpcManager.Instance;
        }

        /// <summary>
        /// Used to set the GUID upon activation if it wasn't set earlier.
        /// </summary>
        public void SetInstanceGuid(SerializableGuid guid)
        {
             instanceGuid = guid;
        }

        /// <summary>
        /// Sets the persistent string ID used to look up the core TiNpcData.
        /// </summary>
        public void SetTiDataStringId(string id)
        {
             tiNpcDataStringId = id;
        }
        
        /// <summary>
        /// Implements ISavableComponent.
        /// This method  "flushes" the active state to the central TiNpcData 
        /// and returns NULL to indicate no separate save artifact is needed.
        /// </summary>
        public ISaveable CreateSaveData()
        {
             // 1. Ensure Manager linkage
             if (tiNpcManager == null) tiNpcManager = TiNpcManager.Instance;
             if (tiNpcManager == null) return null;

             // 2. Validate ID
             if (string.IsNullOrEmpty(tiNpcDataStringId))
             {
                  Debug.LogWarning($"TiNpcSavableComponent on {gameObject.name}: Missing TiNpcDataStringId. Cannot flush state.", this);
                  return null;
             }

             // 3. Retrieve the Master Data Record
             TiNpcData data = tiNpcManager.GetTiNpcData(tiNpcDataStringId);
             if (data == null)
             {
                  Debug.LogError($"TiNpcSavableComponent on {gameObject.name}: Could not find TiNpcData for ID '{tiNpcDataStringId}'. Saving failed for this NPC.", this);
                  return null;
             }

             // 4. FLUSH STATE: Update the Master Record with current Active values
             data.CurrentWorldPosition = transform.position;
             data.CurrentWorldRotation = transform.rotation;
             
             // Save the current Active State Enum
             if (runner != null)
             {
                 var currentState = runner.GetCurrentState();
                 if (currentState != null)
                 {
                     // Convert the Active State enum to a Basic State enum before saving.
                     // This ensures DetermineActivationState sees the data format it expects (Basic States) when loading.
                     Enum activeState = currentState.HandledState;
                     Enum basicState = tiNpcManager.GetBasicStateFromActiveState(activeState);
                     
                     data.SetCurrentState(basicState);
                 }

                 // --- Flush Browse Location Index ---
                 // If the Runner has a target location selected (e.g., during CustomerEntering), save its index.
                 if (runner.CurrentTargetLocation.HasValue)
                 {
                     var customerManager = runner.Manager ?? CustomerManager.Instance;
                     
                     if (customerManager != null)
                     {
                         // Find the index of the specific location struct
                         int index = customerManager.GetBrowseLocationIndex(runner.CurrentTargetLocation.Value);
                         data.savedBrowseLocationIndex = index;
                         Debug.Log($"[TiNpcSavableComponent] Flushed BrowseLocation Index {index} for '{data.Id}' (Active Flush)."); 
                     }
                     else
                     {
                         Debug.LogWarning($"[TiNpcSavableComponent] Cannot flush BrowseLocation for '{data.Id}' - CustomerManager not found.");
                         data.savedBrowseLocationIndex = -1;
                     }
                 }
                 else
                 {
                     // Clear the index if no location is targeted
                     data.savedBrowseLocationIndex = -1;
                 }

                 // Flush Path Data if applicable ---
                 if (runner.PathFollowingHandler != null && runner.PathFollowingHandler.IsFollowingPath)
                 {
                     var pathSO = runner.PathFollowingHandler.GetCurrentPathSO();
                     if (pathSO != null)
                     {
                         data.simulatedPathID = pathSO.PathID;
                         data.simulatedWaypointIndex = runner.PathFollowingHandler.GetCurrentTargetWaypointIndex();
                         data.simulatedFollowReverse = runner.PathFollowingHandler.GetFollowReverse();
                         data.isFollowingPathBasic = true; // Flag for simulation logic
                         
                         Debug.Log($"[TiNpcSavableComponent] Flushed Path Data for '{data.Id}': Path={data.simulatedPathID}, Index={data.simulatedWaypointIndex}, Reverse={data.simulatedFollowReverse}");
                     }
                 }
                 else
                 {
                     // Ensure we clear these if NOT following a path, to prevent stale data
                     // This prevents an NPC saved in 'Idle' from accidentally resuming an old path if they transition back to a path state later.
                     data.simulatedPathID = null;
                     data.simulatedWaypointIndex = -1;
                     data.simulatedFollowReverse = false;
                     data.isFollowingPathBasic = false;
                 }
             }

             // Confirm the position being saved to data
             Debug.Log($"[TiNpcSavableComponent] Saving '{data.Id}'. Flushed WorldPos: {data.CurrentWorldPosition}, Rotation: {data.CurrentWorldRotation.eulerAngles}. State: {data.CurrentStateEnumKey}");

             // 5. Return NULL
             return null; 
        }
        
        /// <summary>
        /// Binds saved data back to the component.
        /// For TI NPCs, this is largely handled by the Activation logic in TiNpcManager,
        /// but we implement it to satisfy the interface.
        /// </summary>
        public void Bind(ISaveable data)
        {
             // No operation needed here for Phase 2 architecture.
             // The Manager handles re-activation and state restoration.
        }
    }
}