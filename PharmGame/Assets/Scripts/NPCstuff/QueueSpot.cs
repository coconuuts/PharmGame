// --- START OF FILE QueueSpot.cs ---

using UnityEngine;
using Game.NPC; // Needed for NpcStateMachineRunner reference
// QueueType enum might also be needed here, but it's simple enough it can live elsewhere if desired.
// Assuming QueueType.cs exists and is accessible.

namespace CustomerManagement // Or a shared Game.Data namespace if preferred
{
    /// <summary>
    /// Represents a single spot in a queue, holding its physical location, index, type,
    /// and the NPC currently occupying it.
    /// </summary>
    [System.Serializable] // Keep serializable for potential inspector lists if needed
    public class QueueSpot // Using a class allows 'null' for currentOccupant
    {
        [Tooltip("The Transform point for this spot.")]
        public Transform spotTransform;

        [Tooltip("The index of this spot within its queue list.")]
        public int spotIndex;

        [Tooltip("The type of queue this spot belongs to (Main or Secondary).")]
        public QueueType queueType; // Assuming QueueType enum is accessible

        [System.NonSerialized] // Don't serialize runtime data like object references
        [Tooltip("The NpcStateMachineRunner currently occupying this spot, or null if free.")]
        public Game.NPC.NpcStateMachineRunner currentOccupant = null; // Reference to the NPC at this spot

        /// <summary>
        /// Constructor to initialize the static data for the spot.
        /// </summary>
        public QueueSpot(Transform transform, int index, QueueType type)
        {
            spotTransform = transform;
            spotIndex = index;
            queueType = type;
            // currentOccupant defaults to null
        }

        /// <summary>
        /// Checks if this queue spot is currently occupied by an NPC.
        /// </summary>
        public bool IsOccupied => currentOccupant != null;

        // Optional: Add methods for assigning/clearing occupant with logging if desired,
        // but direct assignment in CustomerManager is also fine.
        // public void AssignOccupant(Game.NPC.NpcStateMachineRunner runner) { ... }
        // public void ClearOccupant() { ... }
    }
}