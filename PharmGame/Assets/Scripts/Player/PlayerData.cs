using System;
using Systems.Persistence;
using Systems.Inventory;
using UnityEngine;

namespace Systems.SaveLoad
{
    [Serializable]
    public class PlayerData : ISaveable
    {
        [field: SerializeField] public SerializableGuid Id { get; set; }
        public Vector3 position;
        public Quaternion rotation;
    }
}
