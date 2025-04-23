using UnityEngine;

namespace Utils.Pooling // Ensure this matches your pooling namespace
{
    /// <summary>
    /// Helper component added to pooled GameObjects to track their original prefab.
    /// Internal to the pooling system.
    /// </summary>
    internal class PooledObjectInfo : MonoBehaviour
    {
        public GameObject OriginalPrefab { get; set; }
        // Could add other info here if needed by the pooling system internally
    }
}