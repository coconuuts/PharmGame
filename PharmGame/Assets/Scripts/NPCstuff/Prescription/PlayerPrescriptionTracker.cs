// --- START OF FILE PlayerPrescriptionTracker.cs ---

using UnityEngine;
using Game.Prescriptions; // Needed for PrescriptionOrder
using System; // Needed for Action, Nullable
using Systems.Persistence;
using Systems.Inventory;

namespace Systems.Player // Place in a suitable namespace for player components
{
    [Serializable]
    public class PlayerPrescriptionData : ISaveable
    {
        [SerializeField] private SerializableGuid _id;
        public SerializableGuid Id { get => _id; set => _id = value; }
        
        public bool HasActiveOrder;
        public PrescriptionOrder ActiveOrder;
    }
    
    /// <summary>
    /// Component on the player GameObject to track the currently active prescription order
    /// the player is attempting to fulfill. Now implemented as a singleton for fast access.
    /// </summary>
    public class PlayerPrescriptionTracker : MonoBehaviour, ISavableComponent, IBind<PlayerPrescriptionData>
    {
        // --- REFACTORED: SINGLETON INSTANCE ---
        /// <summary>
        /// Provides a static, globally accessible reference to the single PlayerPrescriptionTracker instance.
        /// </summary>
        public static PlayerPrescriptionTracker Instance { get; private set; }

        // --- EVENT ---
        /// <summary>
        /// Event triggered when the player's active prescription order changes.
        /// Provides the new active order (or null if cleared).
        /// </summary>
        public static event Action<PrescriptionOrder?> OnActiveOrderChanged;

        [Tooltip("The prescription order the player is currently trying to fulfill. Null if no active order.")]
        [SerializeField] // Serialize for debugging in inspector
        private PrescriptionOrder? activePrescriptionOrder; // Use nullable struct to represent no order

        /// <summary>
        /// Gets the currently active prescription order the player is trying to fulfill.
        /// Returns null if no order is active.
        /// </summary>
        public PrescriptionOrder? ActivePrescriptionOrder => activePrescriptionOrder;


        private void Awake()
        {
            // --- SINGLETON INITIALIZATION LOGIC ---
            if (Instance == null)
            {
                Instance = this;
                // Optional: If the player persists across scenes, you might uncomment this.
                // DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Debug.LogWarning("Multiple PlayerPrescriptionTracker instances found. Destroying duplicate.", gameObject);
                Destroy(gameObject); // Destroy this duplicate instance
            }
        }

        /// <summary>
        /// Sets the active prescription order for the player.
        /// Publishes the OnActiveOrderChanged event.
        /// </summary>
        /// <param name="order">The order to set.</param>
        public void SetActiveOrder(PrescriptionOrder order)
        {
            activePrescriptionOrder = order;
            Debug.Log($"PlayerPrescriptionTracker ({gameObject.name}): Active prescription order set: {order.ToString()}", this);

            // Publish the event
            OnActiveOrderChanged?.Invoke(activePrescriptionOrder); // Use ?.Invoke for null safety
        }

        /// <summary>
        /// Clears the active prescription order from the player.
        /// Publishes the OnActiveOrderChanged event.
        /// </summary>
        public void ClearActiveOrder()
        {
            activePrescriptionOrder = null;
            Debug.Log($"PlayerPrescriptionTracker ({gameObject.name}): Active prescription order cleared.", this);

            if (PlayerUIPopups.Instance != null)
            {
                PlayerUIPopups.Instance.HidePopup("Prescription Order");
            }

            // Publish the event
            OnActiveOrderChanged?.Invoke(activePrescriptionOrder); // Use ?.Invoke for null safety
        }

        [Header("Save System")]
        [SerializeField] private SerializableGuid id;
        public SerializableGuid Id { get => id; set => id = value; }

        public ISaveable CreateSaveData()
        {
            var data = new PlayerPrescriptionData();
            data.Id = this.Id;

            if (activePrescriptionOrder.HasValue)
            {
                data.HasActiveOrder = true;
                data.ActiveOrder = activePrescriptionOrder.Value;
            }
            else
            {
                data.HasActiveOrder = false;
            }

            return data;
        }

        public void Bind(ISaveable data)
        {
            if (data is PlayerPrescriptionData saveData)
            {
                Bind(saveData);
            }
        }

        public void Bind(PlayerPrescriptionData data)
        {
            if (data.HasActiveOrder)
            {
                SetActiveOrder(data.ActiveOrder);

                // --- RESTORE UI ON LOAD ---
                if (PlayerUIPopups.Instance != null)
                {
                    Debug.Log($"PlayerPrescriptionTracker: Restoring Prescription UI for order: {data.ActiveOrder.patientName}");
                    PlayerUIPopups.Instance.ShowPopup("Prescription Order", data.ActiveOrder.ToString());
                }
                else
                {
                    Debug.LogWarning("PlayerPrescriptionTracker: Loaded active order but PlayerUIPopups.Instance was null. UI not shown.");
                }
            }
            else
            {
                ClearActiveOrder();
            }
        }
    }
}