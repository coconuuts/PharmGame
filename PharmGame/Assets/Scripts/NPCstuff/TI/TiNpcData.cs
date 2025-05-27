using UnityEngine;
using System;
using Game.NPC.States;
using Systems.Inventory;
using Game.NPC.Types;

namespace Game.NPC.TI
{
    /// <summary>
    /// Represents the persistent data for a True Identity NPC, independent of their GameObject.
    /// Includes fields needed for off-screen simulation.
    /// </summary>
    [System.Serializable]
    public class TiNpcData
    {
        [Tooltip("A unique identifier for this TI NPC.")]
        [SerializeField] private string id; // Unique ID for lookup

        [Header("Core Persistent Data")]
        [Tooltip("The NPC's designated home position.")]
        [SerializeField] private Vector3 homePosition;
        [Tooltip("The NPC's designated home rotation.")]
        [SerializeField] private Quaternion homeRotation;

        [Tooltip("The NPC's current world position.")]
        [SerializeField] private Vector3 currentWorldPosition;
        [Tooltip("The NPC's current world rotation.")]
        [SerializeField] private Quaternion currentWorldRotation;

        // Storing state using strings to allow serialization across different enum types
        [Tooltip("The string name of the NPC's current state enum value.")]
        [SerializeField] private string currentStateEnumKey;
        [Tooltip("The assembly qualified name of the NPC's current state enum type.")]
        [SerializeField] private string currentStateEnumType;

        // --- PHASE 4, SUBSTEP 1: Add Simulation Data Fields ---
        [Header("Simulation Data (Managed Off-screen)")]
        [Tooltip("The target position for off-screen movement simulation (e.g., patrol point, exit). Null if no target.")]
        [SerializeField] public Vector3? simulatedTargetPosition; // Made public for direct access by simulation logic

        [Tooltip("A timer used for off-screen simulation of states like waiting, browsing, etc.")]
        [SerializeField] public float simulatedStateTimer; // Made public for direct access by simulation logic
        [System.NonSerialized] public GameObject NpcGameObject;
        // --- END PHASE 4 Fields ---


        [Header("Runtime Data (Not Saved Persistently)")]
        [Tooltip("True if this NPC currently has an active GameObject representation in the scene.")]
        [SerializeField] public bool isActiveGameObject; // Indicates if the data is currently linked to a pooled GameObject


        // --- Public Properties ---
        public string Id => id;
        public Vector3 HomePosition => homePosition;
        public Quaternion HomeRotation => homeRotation;

        // World position and rotation are writable as they change dynamically (simulated or live)
        public Vector3 CurrentWorldPosition { get => currentWorldPosition; set => currentWorldPosition = value; }
        public Quaternion CurrentWorldRotation { get => currentWorldRotation; set => currentWorldRotation = value; }

        // State keys are writable as they change dynamically (simulated or live)
        public string CurrentStateEnumKey { get => currentStateEnumKey; set => currentStateEnumKey = value; }
        public string CurrentStateEnumType { get => currentStateEnumType; set => currentStateEnumType = value; }

        // Activation status is writable
        public bool IsActiveGameObject { get => isActiveGameObject; set => isActiveGameObject = value; }


        /// <summary>
        /// Attempts to parse the stored state strings into a runtime System.Enum value.
        /// Returns null if parsing fails.
        /// </summary>
        public System.Enum CurrentStateEnum
        {
            get
            {
                if (string.IsNullOrEmpty(currentStateEnumKey) || string.IsNullOrEmpty(currentStateEnumType)) return null;

                try
                {
                    Type enumType = Type.GetType(currentStateEnumType);
                    if (enumType == null || !enumType.IsEnum)
                    {
                        // Debug.LogError($"TiNpcData ({id}): Failed to get Enum Type '{currentStateEnumType}' for state '{currentStateEnumKey}'."); // Too noisy if many NPCs
                        return null;
                    }
                    return (System.Enum)Enum.Parse(enumType, currentStateEnumKey);
                }
                catch (Exception e)
                {
                    Debug.LogError($"TiNpcData ({id}): Failed to parse enum '{currentStateEnumKey}' of type '{currentStateEnumType}': {e.Message}");
                    return null;
                }
            }
        }


        /// <summary>
        /// Constructor for creating a new TiNpcData instance.
        /// </summary>
        public TiNpcData(string id, Vector3 homePosition, Quaternion homeRotation)
        {
            this.id = id;
            this.homePosition = homePosition;
            this.homeRotation = homeRotation;

            // Initialize current state to home location and a default state
            this.currentWorldPosition = homePosition;
            this.currentWorldRotation = homeRotation;

            this.currentStateEnumKey = null; // Set on load/simulation
            this.currentStateEnumType = null; // Set on load/simulation

            Debug.Log($"DEBUG TiNpcData ({id}): Constructor called (InstanceID: {this.GetHashCode()}). Initializing isActiveGameObject=false, NpcGameObject=null.", NpcGameObject);
            this.isActiveGameObject = false; // Starts without a GameObject
            this.NpcGameObject = null;

            // Initialize simulation fields
            this.simulatedTargetPosition = null;
            this.simulatedStateTimer = 0f;
        }

        /// <summary>
        /// Helper to set the current state using a System.Enum.
        /// </summary>
        public void SetCurrentState(System.Enum stateEnum)
        {
            if (stateEnum == null)
            {
                // --- DEBUG: Log state clear ---
                Debug.Log($"DEBUG TiNpcData ({id}): Setting CurrentStateEnum to null (InstanceID: {this.GetHashCode()}).", NpcGameObject);
                // --- END DEBUG ---
                currentStateEnumKey = null;
                currentStateEnumType = null;
                return;
            }

            Debug.Log($"DEBUG TiNpcData ({id}): Setting CurrentStateEnum to '{stateEnum.GetType().Name}.{stateEnum.ToString()}' (InstanceID: {this.GetHashCode()}).", NpcGameObject);
            currentStateEnumKey = stateEnum.ToString();
            currentStateEnumType = stateEnum.GetType().AssemblyQualifiedName; // Use AssemblyQualifiedName for robust loading
        }

        // Optional: Add a method to explicitly link/unlink GameObject for clarity
        public void LinkGameObject(GameObject go)
        {
            Debug.Log($"DEBUG TiNpcData ({id}): Linking GameObject '{go?.name ?? "NULL"}' (InstanceID: {this.GetHashCode()}). Setting NpcGameObject and isActiveGameObject=true.", NpcGameObject);
            this.NpcGameObject = go;
            this.isActiveGameObject = true;
        }

        public void UnlinkGameObject()
        {
            Debug.Log($"DEBUG TiNpcData ({id}): Unlinking GameObject '{this.NpcGameObject?.name ?? "NULL"}' (InstanceID: {this.GetHashCode()}). Setting NpcGameObject=null and isActiveGameObject=false.", this.NpcGameObject);
            this.NpcGameObject = null;
            this.isActiveGameObject = false;
        }
         public override int GetHashCode()
         {
             // Simple hash code based on ID (assuming ID is unique)
             return id.GetHashCode();
         }
         // Need Equals as well if overriding GetHashCode and using in collections that might check equality
          public override bool Equals(object obj)
          {
              if (obj == null || GetType() != obj.GetType())
              {
                  return false;
              }

              TiNpcData other = (TiNpcData)obj;
              return id == other.id; // Assuming ID is the unique identifier
          }
    }
}