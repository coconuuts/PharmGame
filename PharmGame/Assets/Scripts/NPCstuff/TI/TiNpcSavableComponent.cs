using UnityEngine;
using System;
using Systems.Persistence;
using Systems.Inventory; // Needed for SerializableGuid if referenced
using Game.NPC.TI;
using Game.NPC;

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
        [SerializeField] private string tiNpcDataStringId;

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
        /// PHASE 2 UPDATE: This method now "flushes" the active state to the central TiNpcData 
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
                     data.SetCurrentState(currentState.HandledState);
                     // Debug.Log($"TiNpcSavableComponent: Flushed state '{currentState.HandledState}' for '{data.Id}' to data.");
                 }
             }

             // 5. Return NULL
             // We return null because the data is now safely inside 'data', which resides in the 'TiNpcManager.allTiNpcs' list.
             // The SaveLoadSystem will grab that ENTIRE list via the TiNpcPersistenceBridge.
             // We do NOT want to add a duplicate partial record to the generic save list.
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