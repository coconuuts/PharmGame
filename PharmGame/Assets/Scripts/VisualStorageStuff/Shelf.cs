using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for Linq methods if used

namespace VisualStorage
{
    /// <summary>
    /// Represents a single shelf within a storage unit and manages its ShelfSlot components.
    /// </summary>
    public class Shelf : MonoBehaviour
    {
        [Tooltip("The row dimension of this shelf's grid (e.g., 2 for a 2x10 grid).")]
        [SerializeField] private int rows = 2;
        [Tooltip("The column dimension of this shelf's grid (e.g., 10 for a 2x10 grid).")]
        [SerializeField] private int columns = 10;

        [Tooltip("Drag all the ShelfSlot GameObjects belonging to this shelf here, ordered by filling priority (e.g., row by row, left to right).")]
        [SerializeField] private List<ShelfSlot> shelfSlots; // Assign in Inspector

        // Public properties to access dimensions and slots
        public int Rows => rows;
        public int Columns => columns;
        public List<ShelfSlot> ShelfSlots => shelfSlots; // Provides access to the ordered list

        private void Awake()
        {
            // Basic validation
            if (shelfSlots == null || shelfSlots.Count == 0)
            {
                Debug.LogWarning($"Shelf ({gameObject.name}): No ShelfSlot components assigned!", this);
            }
             else if (shelfSlots.Count != rows * columns)
             {
                 Debug.LogWarning($"Shelf ({gameObject.name}): Number of assigned ShelfSlots ({shelfSlots.Count}) does not match declared grid size ({rows}x{columns} = {rows * columns}).", this);
                 // Decide how to handle this mismatch - could resize the list, log error, etc.
             }
        }

        /// <summary>
        /// Gets a ShelfSlot by its grid coordinates.
        /// Returns null if coordinates are out of bounds or the slot is not assigned.
        /// </summary>
        public ShelfSlot GetSlot(int rowIndex, int columnIndex)
        {
             if (rowIndex < 0 || rowIndex >= rows || columnIndex < 0 || columnIndex >= columns)
             {
                 Debug.LogWarning($"Shelf ({gameObject.name}): Attempted to get slot with out-of-bounds coordinates ({rowIndex}, {columnIndex}). Grid size is {rows}x{columns}.");
                 return null;
             }

            // Calculate the index in the linear list based on grid coordinates and filling order.
            // Assumes row-by-row, left-to-right filling order for this calculation.
            // If your filling order is different, adjust this calculation.
            int index = rowIndex * columns + columnIndex;

            if (shelfSlots != null && index >= 0 && index < shelfSlots.Count)
            {
                return shelfSlots[index];
            }

             Debug.LogWarning($"Shelf ({gameObject.name}): Slot at index {index} for coordinates ({rowIndex}, {columnIndex}) is not assigned in the list.");
            return null;
        }

        // TODO: Add a method to clear all slots on this shelf (used by Visualizer when clearing).
        public void ClearAllSlots()
        {
             if (shelfSlots != null)
             {
                  foreach (var slot in shelfSlots)
                  {
                       if (slot != null)
                       {
                            // Note: This only vacates the slot state. The Visualizer must destroy the prefab.
                            slot.Vacate();
                       }
                  }
             }
             Debug.Log($"Shelf ({gameObject.name}): All slots vacated.");
        }
    }
}