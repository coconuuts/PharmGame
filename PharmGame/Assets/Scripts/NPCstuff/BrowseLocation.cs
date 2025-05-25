using UnityEngine;
using Systems.Inventory;

namespace CustomerManagement
{
    /// <summary>
    /// Represents a location in the store where customers can browse,
    /// linked to the specific inventory at that location.
    /// </summary>
    [System.Serializable] // Make it serializable so it appears in the Inspector
    public struct BrowseLocation // --- CORRECTED NAME: BrowseLocation ---
    {
        [Tooltip("The Transform point where the customer will stand to browse.")]
        public Transform browsePoint;

        [Tooltip("The Inventory component associated with this Browse location (e.g., on the shelves).")]
        public Inventory inventory;

        // Optional: Could add other info like priority, item types found here, etc.
    }
}