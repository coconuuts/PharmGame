using UnityEngine;

namespace VisualStorage // You might create a new namespace for this system
{
    /// <summary>
    /// Represents a specific 3D position and rotation on a shelf where an item prefab can be displayed.
    /// </summary>
    public class ShelfSlot : MonoBehaviour
    {
        [Tooltip("The row index of this slot within its shelf grid (0-based).")]
        [SerializeField] private int rowIndex;
        [Tooltip("The column index of this slot within its shelf grid (0-based).")]
        [SerializeField] private int columnIndex;

        // Public properties to easily access coordinates
        public int RowIndex => rowIndex;
        public int ColumnIndex => columnIndex;

        /// <summary>
        /// The Transform of this shelf slot, defining the position and rotation for an item prefab.
        /// </summary>
        public Transform SlotTransform => this.transform;

        /// <summary>
        /// True if this slot is currently occupied by a visual item prefab.
        /// </summary>
        public bool IsOccupied { get; private set; } = false;

        /// <summary>
        /// The GameObject prefab currently occupying this slot, if any.
        /// </summary>
        public GameObject CurrentItemPrefab { get; private set; } = null;

        // You might want a reference to the Shelf this slot belongs to
        // public Shelf ParentShelf { get; set; } // Can be set by the Shelf script

        /// <summary>
        /// Marks this slot as occupied and stores a reference to the item prefab placed here.
        /// </summary>
        /// <param name="itemPrefab">The GameObject prefab instance that is occupying this slot.</param>
        public void Occupy(GameObject itemPrefab)
        {
            if (itemPrefab == null)
            {
                Debug.LogWarning($"ShelfSlot ({gameObject.name}): Attempted to occupy with a null item prefab.", this);
                return;
            }
            if (IsOccupied)
            {
                 Debug.LogWarning($"ShelfSlot ({gameObject.name}): Attempted to occupy slot that is already occupied.", this);
                 // Decide how to handle this: log error, replace, etc. For now, we'll just log.
                 // Vacate(); // Option: Vacate the old one first
            }

            IsOccupied = true;
            CurrentItemPrefab = itemPrefab;
             Debug.Log($"ShelfSlot ({gameObject.name}): Occupied by prefab '{itemPrefab.name}'.");
        }

        /// <summary>
        /// Marks this slot as vacant and clears the reference to the item prefab.
        /// Does NOT destroy the prefab GameObject itself.
        /// </summary>
        public void Vacate()
        {
             if (!IsOccupied && CurrentItemPrefab == null)
             {
                  // Debug.LogWarning($"ShelfSlot ({gameObject.name}): Attempted to vacate slot that is already vacant."); // Too noisy
                  return;
             }

            IsOccupied = false;
            CurrentItemPrefab = null; // Clear the reference
             // The StorageObjectVisualizer is responsible for destroying the actual GameObject.
             Debug.Log($"ShelfSlot ({gameObject.name}): Vacated.");
        }

        // Optional: Add validation in OnValidate for row/column values if you have max limits per shelf.

        // You might add Gizmos in OnDrawGizmos to visualize the slot in the scene view.
        private void OnDrawGizmos()
        {
            // Draw a small cube or sphere to represent the slot's position and rotation
            Gizmos.color = IsOccupied ? Color.red : Color.green; // Red if occupied, Green if available
            Gizmos.matrix = transform.localToWorldMatrix; // Apply the slot's transform
            Gizmos.DrawCube(Vector3.zero, Vector3.one * 0.1f); // Draw a small cube at the slot's origin

            // Draw an arrow to show the forward direction (where the item will face)
            Gizmos.color = Color.blue; // Blue for forward direction
            Gizmos.DrawRay(Vector3.zero, Vector3.forward * 0.2f); // Draw a ray forward
        }
    }
}