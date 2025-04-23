using UnityEngine;
using System;

namespace Utils.Pooling // Same pooling namespace
{
    /// <summary>
    /// Configuration settings for a single GameObject pool.
    /// </summary>
    [Serializable] // Make it serializable so it appears in the Inspector
    public struct GameObjectPoolConfig
    {
        [Tooltip("The prefab GameObject to pool.")]
        public GameObject prefab;

        [Tooltip("The initial number of instances to create when the pool is initialized.")]
        public int initialSize;

        [Tooltip("The maximum number of instances this pool can hold (0 for no limit).")]
        public int maxSize;

        [Tooltip("Can the pool create new instances if requested when empty and maxSize is not reached?")]
        public bool canGrow;

        // You could add other configuration options here, like parent transform override, etc.
    }

    // Example if you needed a Component pool config later:
    // [Serializable]
    // public struct ComponentPoolConfig<T> where T : Component
    // {
    //     public GameObject prefab;
    //     public int initialSize;
    //     public int maxSize;
    //     public bool canGrow;
    // }
}