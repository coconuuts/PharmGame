using UnityEngine;

namespace Game.NPC.Handlers
{
    /// <summary>
    /// Represents the calculated position and rotation for an NPC
    /// after a single movement tick. Used by handlers that separate
    /// logic position/rotation from visual presentation.
    /// </summary>
    public struct MovementTickResult
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public MovementTickResult(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }
}