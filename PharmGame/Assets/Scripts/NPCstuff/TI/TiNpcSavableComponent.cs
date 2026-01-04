// --- START OF FILE TiNpcSavableComponent.cs ---

using UnityEngine;
using System;
using Systems.Persistence;
using Systems.Inventory; // Needed for SerializableGuid
using Game.NPC.TI;
using Game.NPC;

namespace Game.NPC.TI
{
    /// <summary>
    /// Component attached to an active True Identity NPC GameObject.
    /// It allows the generic SaveLoadSystem to register this object's state
    /// by providing a SerializableGuid, which maps back to its core TiNpcData.
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
            
            // For TI NPCs, the GUID should be set during activation or initialization
            if (instanceGuid.Equals(SerializableGuid.Empty))
            {
                 Debug.LogWarning($"TiNpcSavableComponent on {gameObject.name}: Instance GUID is empty. This might be okay if the object is only tracked via TiNpcData, but requires manual setup or assignment during activation.", this);
            }
        }

        void Start()
        {
             tiNpcManager = TiNpcManager.Instance;
             if (tiNpcManager == null)
             {
                  Debug.LogError($"TiNpcSavableComponent on {gameObject.name}: TiNpcManager not found!", this);
             }
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
        /// Implements ISavableComponent: Creates the data structure needed for saving this specific component's state.
        /// For TI NPCs, this primarily saves the *instance GUID* so the system knows which TiNpcData to update upon load.
        /// </summary>
        public ISaveable CreateSaveData()
        {
             // If this component is active, the authoritative state is saved in TiNpcData.
             // We just need to ensure the runner's GUID is saved, which is already handled by the Runner's transient snapshot mechanism (if applicable) or rely on the fact that TI NPCs are saved via TiNpcManager's central dump.
             
             // Since TI NPCs are saved centrally via TiNpcManager (Phase 4.1 adjustment), 
             // this component's primary role upon saving is to ensure its GUID is known if necessary, 
             // or to save its *Active State* if the generic SaveLoadSystem is scanning everything.

             // Given the plan is to save TI data centrally, this component might only need to be found to confirm existence, 
             // or to save the *Active* state if the TI NPC is active.

             // For now, we will only save the GUID if it's not empty, allowing the SaveLoadSystem to find *something* associated with this object.
             // Since the primary saving mechanism is the TiNpcManager, we return a minimal structure.
             
             // NOTE: For maximum compatibility with the GUID-based saving structure, 
             // we create a simple container structure that holds the necessary link data.
             
             if (string.IsNullOrEmpty(tiNpcDataStringId) || instanceGuid.Equals(SerializableGuid.Empty))
             {
                  Debug.LogWarning($"TiNpcSavableComponent on {gameObject.name} has incomplete link data (String ID: {tiNpcDataStringId}, GUID: {instanceGuid}). Returning null save data.", this);
                  return null;
             }

             // We will use a custom container structure that encapsulates the TI link ID, 
             // but since the interface expects ISaveable (which usually returns InventoryData or PlayerData), 
             // we must adapt or create a new route.
             
             // To adhere strictly to the provided SaveLoadSystem structure where it expects to route to known lists (inventories, player data), 
             // we must assume that the *Active State* information needs to be stored somewhere that SaveLoadSystem recognizes.
             
             // Let's create a placeholder data class for the Active State of a TI NPC, assuming we need to save it here if the object is active.
             // This will require adding a route in SaveLoadSystem.SaveGame().
             
             return new TiNpcActiveStateData
             {
                 Id = instanceGuid,
                 TiNpcStringId = tiNpcDataStringId,
                 ActiveStateEnumKey = runner.GetCurrentState()?.HandledState.ToString(),
                 ActiveStateEnumType = runner.GetCurrentState()?.HandledState.GetType().AssemblyQualifiedName
             };
        }
        
        /// <summary>
        /// Binds saved data back to the component. (Needed for ISavableComponent interface).
        /// </summary>
        public void Bind(ISaveable data)
        {
             if (data is TiNpcActiveStateData tiData)
             {
                  // This is used when loading the scene. We update the local component state
                  // but the heavy lifting of loading the position/state into TiNpcData happens later.
                  this.instanceGuid = tiData.Id;
                  this.tiNpcDataStringId = tiData.TiNpcStringId;
                  Debug.Log($"TiNpcSavableComponent ({gameObject.name}) bound. Restored GUID {instanceGuid.ToHexString()} linked to TI ID {tiNpcDataStringId}. Active State info ignored here (handled by TiNpcManager on activation).");
             }
             // Other data types are irrelevant to this component.
        }
    }
    
    // --- NEW: Custom Data Structure for TI Active State ---
    [Serializable]
    public class TiNpcActiveStateData : ISaveable {
        public SerializableGuid Id { get; set; }
        public string TiNpcStringId;
        public string ActiveStateEnumKey;
        public string ActiveStateEnumType;
    }
}

// --- END OF FILE TiNpcSavableComponent.cs ---