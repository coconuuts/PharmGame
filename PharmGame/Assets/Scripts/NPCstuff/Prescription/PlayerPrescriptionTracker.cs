// --- START OF FILE PlayerPrescriptionTracker.cs ---

using UnityEngine;
using Game.Prescriptions; // Needed for PrescriptionOrder // <-- Added using directive

namespace Systems.Player // Place in a suitable namespace for player components
{
    /// <summary>
    /// Component on the player GameObject to track the currently active prescription order
    /// the player is attempting to fulfill.
    /// </summary>
    public class PlayerPrescriptionTracker : MonoBehaviour
    {
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
        /// </summary>
        /// <param name="order">The order to set.</param>
        public void SetActiveOrder(PrescriptionOrder order)
        {
            activePrescriptionOrder = order;
            Debug.Log($"PlayerPrescriptionTracker ({gameObject.name}): Active prescription order set: {order.ToString()}", this);
        }

        /// <summary>
        /// Clears the active prescription order from the player.
        /// </summary>
        public void ClearActiveOrder()
        {
            activePrescriptionOrder = null;
            Debug.Log($"PlayerPrescriptionTracker ({gameObject.name}): Active prescription order cleared.", this);
        }

        // Optional: Add OnDestroy/OnDisable cleanup if needed, though likely not critical here.
    }
}
// --- END OF FILE PlayerPrescriptionTracker.cs ---