using UnityEngine;
using System.Collections.Generic;

namespace Utils.Pooling // Same pooling namespace
{
    /// <summary>
    /// ScriptableObject asset to define the initial configuration for object pools.
    /// </summary>
    [CreateAssetMenu(fileName = "PoolingConfig", menuName = "Pooling/Pooling Configuration", order = 0)]
    public class PoolingConfigSO : ScriptableObject
    {
        [Tooltip("List of GameObject pools to initialize at startup.")]
        public List<GameObjectPoolConfig> gameObjectPoolConfigs;

        // You could add a list for ComponentPoolConfig here later if needed
        // public List<ComponentPoolConfig> componentPoolConfigs;
    }
}