// --- START OF FILE PlayerPrescriptionTracker.cs ---

using UnityEngine;
using Game.Prescriptions; // Needed for PrescriptionOrder
using System; // Needed for Action, Nullable

namespace Systems.Player // Place in a suitable namespace for player components
{
    /// <summary>
    /// Component on the player GameObject to track the currently active prescription order
    /// the player is attempting to fulfill. Now implemented as a singleton for fast access.
    /// </summary>
    public class PlayerPrescriptionTracker : MonoBehaviour
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

            // Publish the event
            OnActiveOrderChanged?.Invoke(activePrescriptionOrder); // Use ?.Invoke for null safety
        }
    }
}