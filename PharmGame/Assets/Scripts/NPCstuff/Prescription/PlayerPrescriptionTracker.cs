// --- START OF FILE PlayerPrescriptionTracker.cs ---

using UnityEngine;
using Game.Prescriptions; // Needed for PrescriptionOrder
using System; // Needed for Action, Nullable

namespace Systems.Player // Place in a suitable namespace for player components
{
    /// <summary>
    /// Component on the player GameObject to track the currently active prescription order
    /// the player is attempting to fulfill.
    /// MODIFIED: Added an event to signal when the active order changes. // <-- Added note
    /// </summary>
    public class PlayerPrescriptionTracker : MonoBehaviour
    {
        // --- NEW EVENT ---
        /// <summary>
        /// Event triggered when the player's active prescription order changes.
        /// Provides the new active order (or null if cleared).
        /// </summary>
        public static event Action<PrescriptionOrder?> OnActiveOrderChanged;
        // --- END NEW EVENT ---


        [Tooltip("The prescription order the player is currently trying to fulfill. Null if no active order.")]
        [SerializeField] // Serialize for debugging in inspector
        private PrescriptionOrder? activePrescriptionOrder; // Use nullable struct to represent no order

        /// <summary>
        /// Gets the currently active prescription order the player is trying to fulfill.
        /// Returns null if no order is active.
        /// </summary>
        public PrescriptionOrder? ActivePrescriptionOrder => activePrescriptionOrder;

        /// <summary>
        /// Sets the active prescription order for the player.
        /// MODIFIED: Publishes the OnActiveOrderChanged event. // <-- Added note
        /// </summary>
        /// <param name="order">The order to set.</param>
        public void SetActiveOrder(PrescriptionOrder order)
        {
            activePrescriptionOrder = order;
            Debug.Log($"PlayerPrescriptionTracker ({gameObject.name}): Active prescription order set: {order.ToString()}", this);

            // --- NEW: Publish the event ---
            OnActiveOrderChanged?.Invoke(activePrescriptionOrder); // Use ?.Invoke for null safety
            // --- END NEW ---
        }

        /// <summary>
        /// Clears the active prescription order from the player.
        /// MODIFIED: Publishes the OnActiveOrderChanged event. // <-- Added note
        /// </summary>
        public void ClearActiveOrder()
        {
            activePrescriptionOrder = null;
            Debug.Log($"PlayerPrescriptionTracker ({gameObject.name}): Active prescription order cleared.", this);

            // --- NEW: Publish the event ---
            OnActiveOrderChanged?.Invoke(activePrescriptionOrder); // Use ?.Invoke for null safety
            // --- END NEW ---
        }

        // Optional: Add OnDestroy/OnDisable cleanup if needed, though likely not critical here.
        // Events are static, so they persist. Unsubscribing is important for listeners,
        // but the publisher itself doesn't strictly need cleanup unless it's the ONLY
        // thing holding references, which is unlikely for a player component.
        // However, if this were a non-persistent object, clearing the event on Destroy might be wise.
    }
}