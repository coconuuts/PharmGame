// --- START OF FILE GridManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using Game.NPC.TI; // Needed for TiNpcData
using System; // Needed for Math.Floor

namespace Game.Spatial
{
    /// <summary>
    /// Manages a 3D grid for spatial partitioning of TI Npcs (or other grid items).
    /// Allows efficient querying of items within a given radius.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static GridManager Instance { get; private set; }

        [Header("Grid Configuration")]
        [Tooltip("The world origin point (bottom-left-back corner) of the grid.")]
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;
        [Tooltip("The size of each grid cell in world units.")]
        [SerializeField] internal float cellSize = 5f;
        [Tooltip("The number of cells in X, Y, and Z dimensions.")]
        [SerializeField] private Vector3Int gridDimensions = new Vector3Int(10, 5, 10); // Example dimensions

        // Internal storage: Dictionary mapping grid cell coordinates to a set of items in that cell
        private Dictionary<Vector3Int, HashSet<TiNpcData>> grid = new Dictionary<Vector3Int, HashSet<TiNpcData>>();

        // Optional: Reverse mapping for fast cell lookup by item (useful for updates/removals)
        // We can store the current grid coordinates within TiNpcData if we want, but for now,
        // let's calculate it dynamically based on position or require the caller to provide the old position.
        // Using old position in UpdateItemPosition is simpler and avoids modifying TiNpcData for grid internal state.

        [Header("Debug Visualization")]
        [Tooltip("Enable drawing the grid in the Scene view.")]
        [SerializeField] private bool drawGridGizmos = true;
        [Tooltip("Color of the grid lines.")]
        [SerializeField] private Color gridColor = Color.yellow;


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // Consider if this manager should persist
            }
            else
            {
                Debug.LogWarning("GridManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Basic validation
            if (cellSize <= 0)
            {
                Debug.LogError("GridManager: Cell size must be greater than zero! Setting to 1.", this);
                cellSize = 1f;
            }
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0 || gridDimensions.z <= 0)
            {
                Debug.LogError("GridManager: Grid dimensions must be positive! Setting to 1,1,1.", this);
                gridDimensions = new Vector3Int(1, 1, 1);
            }


            Debug.Log($"GridManager: Awake completed. Grid size: {gridDimensions.x}x{gridDimensions.y}x{gridDimensions.z}, Cell size: {cellSize}.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Clear the grid contents when the manager is destroyed
                grid.Clear();
                Instance = null;
                Debug.Log("GridManager: OnDestroy completed. Grid cleared.");
            }
        }

        // --- Debug Gizmos ---
        // private void OnDrawGizmos()
        // {
        //     if (!drawGridGizmos || !isActiveAndEnabled) return;

        //     Gizmos.color = gridColor;

        //     // Draw the bounds of the grid
        //     Vector3 gridSize = new Vector3(gridDimensions.x * cellSize, gridDimensions.y * cellSize, gridDimensions.z * cellSize);
        //     Gizmos.DrawWireCube(gridOrigin + gridSize * 0.5f, gridSize);

        //     // Draw grid lines (simplified - drawing all lines can be expensive)
        //     // Drawing lines on the bottom plane (Y=gridOrigin.y)
        //      for (int x = 0; x <= gridDimensions.x; x++)
        //      {
        //          Vector3 start = gridOrigin + new Vector3(x * cellSize, 0, 0);
        //          Vector3 end = gridOrigin + new Vector3(x * cellSize, 0, gridDimensions.z * cellSize);
        //          Gizmos.DrawLine(start, end);
        //      }
        //      for (int z = 0; z <= gridDimensions.z; z++)
        //      {
        //          Vector3 start = gridOrigin + new Vector3(0, 0, z * cellSize);
        //          Vector3 end = gridOrigin + new Vector3(gridDimensions.x * cellSize, 0, z * cellSize);
        //          Gizmos.DrawLine(start, end);
        //      }

        //      // Draw vertical lines at corners (simplified)
        //      for (int x = 0; x <= gridDimensions.x; x+=gridDimensions.x) // Only corners
        //      {
        //          for (int z = 0; z <= gridDimensions.z; z+=gridDimensions.z) // Only corners
        //          {
        //               Vector3 start = gridOrigin + new Vector3(x * cellSize, 0, z * cellSize);
        //               Vector3 end = gridOrigin + new Vector3(x * cellSize, gridDimensions.y * cellSize, z * cellSize);
        //               Gizmos.DrawLine(start, end);
        //          }
        //      }

        //      // Drawing internal lines for large grids is very slow.
        //      // Consider drawing a slice near the player in a future iteration if needed.
        // }


        // --- Core Grid Methods ---

        /// <summary>
        /// Converts a world position to grid cell coordinates.
        /// Returns coordinates outside dimensions if position is outside grid bounds.
        /// </summary>
        public Vector3Int WorldToGridCoords(Vector3 worldPosition)
        {
            // Offset by origin
            Vector3 localPos = worldPosition - gridOrigin;

            // Calculate cell indices
            int x = (int)Math.Floor(localPos.x / cellSize);
            int y = (int)Math.Floor(localPos.y / cellSize);
            int z = (int)Math.Floor(localPos.z / cellSize);

            return new Vector3Int(x, y, z);
        }

        /// <summary>
        /// Checks if grid coordinates are within the defined grid dimensions.
        /// </summary>
        private bool IsCoordsValid(Vector3Int coords)
        {
            return coords.x >= 0 && coords.x < gridDimensions.x &&
                   coords.y >= 0 && coords.y < gridDimensions.y &&
                   coords.z >= 0 && coords.z < gridDimensions.z;
        }

        /// <summary>
        /// Adds an item (TiNpcData) to the grid at the specified world position.
        /// </summary>
        public void AddItem(TiNpcData npcData, Vector3 worldPosition)
        {
            if (npcData == null)
            {
                Debug.LogWarning("GridManager: Attempted to add null npcData to the grid.", this);
                return;
            }

            Vector3Int coords = WorldToGridCoords(worldPosition);

            if (!IsCoordsValid(coords))
            {
                // Optionally handle items outside the grid bounds if they need to be queryable.
                // For now, we'll skip adding items outside the grid, assuming NPCs stay within bounds or
                // are deactivated outside the grid (which will be handled by ProximityManager).
                Debug.LogWarning($"GridManager: Attempted to add item '{npcData.Id}' at world position {worldPosition} (coords {coords}) outside grid bounds. Skipping add.", this);
                // Note: If items can legitimately exist outside the grid and need to be queried,
                // you might need a separate list for 'out-of-bounds' items, or a more complex grid structure.
                return;
            }

            // Get or create the HashSet for the cell
            if (!grid.TryGetValue(coords, out HashSet<TiNpcData> cellItems))
            {
                cellItems = new HashSet<TiNpcData>();
                grid[coords] = cellItems;
            }

            // Add the item to the cell's HashSet. HashSet handles duplicates.
            if (cellItems.Add(npcData))
            {
                 // Optional: Debug.Log($"GridManager: Added item '{npcData.Id}' to grid cell {coords}.", this);
            }
            else
            {
                 // Optional: Debug.LogWarning($"GridManager: Item '{npcData.Id}' already exists in grid cell {coords}. Add skipped.", this);
            }
        }

         /// <summary>
         /// Removes an item (TiNpcData) from the grid at the specified world position.
         /// Assumes the item was previously added correctly.
         /// </summary>
         public void RemoveItem(TiNpcData npcData, Vector3 worldPosition)
         {
             if (npcData == null) return; // Cannot remove null

             Vector3Int coords = WorldToGridCoords(worldPosition);

             if (!IsCoordsValid(coords))
             {
                 // If the item was outside the grid when added (and we allowed that),
                 // or if the item moved outside, we need a different removal strategy
                 // (e.g., checking the 'out-of-bounds' list).
                 // For now, if it's outside valid coords, assume it wasn't in a valid cell.
                 // Debug.LogWarning($"GridManager: Attempted to remove item '{npcData.Id}' at world position {worldPosition} (coords {coords}) outside grid bounds. Assuming not in a valid grid cell.", this);
                 return;
             }

             if (grid.TryGetValue(coords, out HashSet<TiNpcData> cellItems))
             {
                 if (cellItems.Remove(npcData))
                 {
                     // Optional: Debug.Log($"GridManager: Removed item '{npcData.Id}' from grid cell {coords}.", this);
                     // Clean up empty HashSets to save memory
                     if (cellItems.Count == 0)
                     {
                         grid.Remove(coords);
                          // Optional: Debug.Log($"GridManager: Removed empty cell {coords} from dictionary.", this);
                     }
                 }
                 else
                 {
                      // Optional: Debug.LogWarning($"GridManager: Attempted to remove item '{npcData.Id}' from grid cell {coords}, but it was not found in that cell.", this);
                 }
             }
             else
             {
                  // Optional: Debug.LogWarning($"GridManager: Attempted to remove item '{npcData.Id}' from grid cell {coords}, but the cell itself was not found in the dictionary.", this);
             }
         }


        /// <summary>
        /// Updates the grid position of an item that has moved from an old world position to a new one.
        /// This is more efficient than Remove + Add if the cell hasn't changed.
        /// </summary>
        public void UpdateItemPosition(TiNpcData npcData, Vector3 oldWorldPosition, Vector3 newWorldPosition)
        {
             if (npcData == null) return;

             Vector3Int oldCoords = WorldToGridCoords(oldWorldPosition);
             Vector3Int newCoords = WorldToGridCoords(newWorldPosition);

             if (oldCoords == newCoords)
             {
                  // Item is still in the same grid cell, no need to update the grid structure.
                  // Optional: Debug.Log($"GridManager: Item '{npcData.Id}' position updated, but remains in the same cell {newCoords}.", this);
                  return;
             }

             // Remove from old cell (if valid)
             if (IsCoordsValid(oldCoords) && grid.TryGetValue(oldCoords, out HashSet<TiNpcData> oldCellItems))
             {
                 if (oldCellItems.Remove(npcData))
                 {
                     if (oldCellItems.Count == 0)
                     {
                         grid.Remove(oldCoords);
                     }
                     // Optional: Debug.Log($"GridManager: Removed item '{npcData.Id}' from old cell {oldCoords}.", this);
                 }
             } else {
                 // Optional: Debug.LogWarning($"GridManager: Could not remove item '{npcData.Id}' from old cell {oldCoords} during update (cell invalid or item not found). Assuming it wasn't there.", this);
             }


             // Add to new cell (if valid)
             if (IsCoordsValid(newCoords))
             {
                 if (!grid.TryGetValue(newCoords, out HashSet<TiNpcData> newCellItems))
                 {
                     newCellItems = new HashSet<TiNpcData>();
                     grid[newCoords] = newCellItems;
                 }

                 if (newCellItems.Add(npcData))
                 {
                      // Optional: Debug.Log($"GridManager: Added item '{npcData.Id}' to new cell {newCoords}.", this);
                 } else {
                      // Optional: Debug.LogWarning($"GridManager: Item '{npcData.Id}' already existed in new cell {newCoords} during update.", this);
                 }
             } else {
                  Debug.LogWarning($"GridManager: Item '{npcData.Id}' moved to world position {newWorldPosition} (coords {newCoords}) outside grid bounds during update. Not added to grid.", this);
                   // Item is now effectively 'lost' to the grid query. ProximityManager will need to handle this.
                   // Maybe the ProximityManager queries a larger radius than the grid bounds or has a fallback check.
             }
        }


        /// <summary>
        /// Queries items within a sphere defined by center and radius.
        /// Returns a list of unique TiNpcData instances found.
        /// </summary>
        /// <param name="center">The center of the sphere in world coordinates.</param>
        /// <param name="radius">The radius of the sphere.</param>
        /// <returns>A list of unique TiNpcData objects found within the radius.</returns>
        public List<TiNpcData> QueryItemsInRadius(Vector3 center, float radius)
        {
            List<TiNpcData> results = new List<TiNpcData>();
            HashSet<TiNpcData> uniqueResults = new HashSet<TiNpcData>(); // Use HashSet to ensure uniqueness

            // Determine the range of grid cells that could potentially intersect the sphere
            Vector3Int minCoords = WorldToGridCoords(center - Vector3.one * radius);
            Vector3Int maxCoords = WorldToGridCoords(center + Vector3.one * radius);

            // Clamp the cell range to be within grid dimensions
            Vector3Int startCoords = new Vector3Int(
                Mathf.Max(0, minCoords.x),
                Mathf.Max(0, minCoords.y),
                Mathf.Max(0, minCoords.z)
            );
            Vector3Int endCoords = new Vector3Int(
                Mathf.Min(gridDimensions.x - 1, maxCoords.x),
                Mathf.Min(gridDimensions.y - 1, maxCoords.y),
                Mathf.Min(gridDimensions.z - 1, maxCoords.z)
            );

            float radiusSq = radius * radius;

            // Iterate through all relevant grid cells
            for (int x = startCoords.x; x <= endCoords.x; x++)
            {
                for (int y = startCoords.y; y <= endCoords.y; y++)
                {
                    for (int z = startCoords.z; z <= endCoords.z; z++)
                    {
                        Vector3Int currentCoords = new Vector3Int(x, y, z);

                        // Check if the cell contains any items
                        if (grid.TryGetValue(currentCoords, out HashSet<TiNpcData> cellItems))
                        {
                            // Check each item in the cell for actual distance
                            foreach (var npcData in cellItems)
                            {
                                // Determine the item's actual world position
                                // If the NPC is active, use its GameObject position
                                // If inactive, use its data's stored position
                                Vector3 itemPosition = npcData.IsActiveGameObject && npcData.NpcGameObject != null ?
                                                        npcData.NpcGameObject.transform.position :
                                                        npcData.CurrentWorldPosition;

                                float distanceToItemSq = (itemPosition - center).sqrMagnitude;

                                if (distanceToItemSq <= radiusSq)
                                {
                                    // Add to unique results set
                                    if (uniqueResults.Add(npcData))
                                    {
                                        results.Add(npcData); // Also add to the list for the return value
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // The list 'results' now contains all unique TiNpcData within the specified radius.
            return results;
        }

         /// <summary>
         /// Gets the total number of items currently registered in the grid.
         /// Note: This iterates through the grid dictionary, not constant time.
         /// </summary>
         public int GetItemCount()
         {
              int count = 0;
              foreach(var cellItems in grid.Values)
              {
                   count += cellItems.Count;
              }
              return count;
         }

         /// <summary>
         /// Removes all items from the grid and clears the grid dictionary.
         /// </summary>
         public void ClearGrid()
         {
              grid.Clear();
              Debug.Log("GridManager: Grid cleared.", this);
         }

    }
}

// --- END OF FILE GridManager.cs ---