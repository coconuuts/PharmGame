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

        // [Header("Debug Visualization")]
        // [Tooltip("Enable drawing the grid in the Scene view.")]
        // [SerializeField] private bool drawGridGizmos = true;
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
        /// Helper to clamp coordinates to the valid grid range.
        /// Used to gracefully handle items slightly out of bounds.
        /// </summary>
        private Vector3Int ClampCoords(Vector3Int coords)
        {
            return new Vector3Int(
                Mathf.Clamp(coords.x, 0, gridDimensions.x - 1),
                Mathf.Clamp(coords.y, 0, gridDimensions.y - 1),
                Mathf.Clamp(coords.z, 0, gridDimensions.z - 1)
            );
        }

        /// <summary>
        /// Adds an item (TiNpcData) to the grid at the specified world position.
        /// Clamps position to valid bounds if outside.
        /// </summary>
        public void AddItem(TiNpcData npcData, Vector3 worldPosition)
        {
            if (npcData == null)
            {
                Debug.LogWarning("GridManager: Attempted to add null npcData to the grid.", this);
                return;
            }

            Vector3Int coords = WorldToGridCoords(worldPosition);

            // FIX: If coords are invalid, clamp them instead of rejecting the item.
            if (!IsCoordsValid(coords))
            {
                Vector3Int clamped = ClampCoords(coords);
                // Optional: Log only if the deviation is significant to avoid spam
                if (Vector3Int.Distance(coords, clamped) > 1) 
                {
                    Debug.LogWarning($"GridManager: Item '{npcData.Id}' at {worldPosition} (coords {coords}) is significantly out of bounds. Clamped to {clamped}.", this);
                }
                coords = clamped;
            }

            if (!grid.TryGetValue(coords, out HashSet<TiNpcData> cellItems))
            {
                cellItems = new HashSet<TiNpcData>();
                grid[coords] = cellItems;
            }

            if (cellItems.Add(npcData))
            {
                 // Added successfully
            }
        }

         /// <summary>
         /// Removes an item (TiNpcData) from the grid.
         /// Checks both exact and clamped coordinates to ensure removal.
         /// </summary>
         public void RemoveItem(TiNpcData npcData, Vector3 worldPosition)
         {
             if (npcData == null) return;

             Vector3Int coords = WorldToGridCoords(worldPosition);

             // Try removing from the exact calculated cell first
             if (IsCoordsValid(coords) && RemoveFromCell(coords, npcData))
             {
                 return; // Found and removed
             }

             // If not found or invalid, try the clamped cell (in case it was added via clamping)
             Vector3Int clamped = ClampCoords(coords);
             if (clamped != coords)
             {
                 RemoveFromCell(clamped, npcData);
             }
         }

         // Helper for removing from a specific cell
         private bool RemoveFromCell(Vector3Int coords, TiNpcData npcData)
         {
             if (grid.TryGetValue(coords, out HashSet<TiNpcData> cellItems))
             {
                 if (cellItems.Remove(npcData))
                 {
                     if (cellItems.Count == 0)
                     {
                         grid.Remove(coords);
                     }
                     return true;
                 }
             }
             return false;
         }


        /// <summary>
        /// Updates the grid position of an item.
        /// Handles out-of-bounds movement by clamping.
        /// </summary>
        public void UpdateItemPosition(TiNpcData npcData, Vector3 oldWorldPosition, Vector3 newWorldPosition)
        {
             if (npcData == null) return;

             Vector3Int oldCoords = WorldToGridCoords(oldWorldPosition);
             Vector3Int newCoords = WorldToGridCoords(newWorldPosition);

             // Clamp both to ensure consistent tracking
             if (!IsCoordsValid(oldCoords)) oldCoords = ClampCoords(oldCoords);
             if (!IsCoordsValid(newCoords)) newCoords = ClampCoords(newCoords);

             if (oldCoords == newCoords) return; // Still in same cell (or same clamped edge cell)

             // Remove from old
             RemoveFromCell(oldCoords, npcData);

             // Add to new
             if (!grid.TryGetValue(newCoords, out HashSet<TiNpcData> newCellItems))
             {
                 newCellItems = new HashSet<TiNpcData>();
                 grid[newCoords] = newCellItems;
             }
             newCellItems.Add(npcData);
        }


        /// <summary>
        /// Queries items within a sphere defined by center and radius.
        /// </summary>
        public List<TiNpcData> QueryItemsInRadius(Vector3 center, float radius)
        {
            List<TiNpcData> results = new List<TiNpcData>();
            HashSet<TiNpcData> uniqueResults = new HashSet<TiNpcData>(); 

            Vector3Int minCoords = WorldToGridCoords(center - Vector3.one * radius);
            Vector3Int maxCoords = WorldToGridCoords(center + Vector3.one * radius);

            // Clamp search range to valid grid
            Vector3Int startCoords = ClampCoords(minCoords);
            Vector3Int endCoords = ClampCoords(maxCoords);

            float radiusSq = radius * radius;

            for (int x = startCoords.x; x <= endCoords.x; x++)
            {
                for (int y = startCoords.y; y <= endCoords.y; y++)
                {
                    for (int z = startCoords.z; z <= endCoords.z; z++)
                    {
                        Vector3Int currentCoords = new Vector3Int(x, y, z);

                        if (grid.TryGetValue(currentCoords, out HashSet<TiNpcData> cellItems))
                        {
                            foreach (var npcData in cellItems)
                            {
                                // Actual distance check ensures we only get relevant items, 
                                // even if they were clamped to this cell from far away.
                                Vector3 itemPosition = npcData.IsActiveGameObject && npcData.NpcGameObject != null ?
                                                        npcData.NpcGameObject.transform.position :
                                                        npcData.CurrentWorldPosition;

                                float distanceToItemSq = (itemPosition - center).sqrMagnitude;

                                if (distanceToItemSq <= radiusSq)
                                {
                                    if (uniqueResults.Add(npcData))
                                    {
                                        results.Add(npcData); 
                                    }
                                }
                            }
                        }
                    }
                }
            }
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