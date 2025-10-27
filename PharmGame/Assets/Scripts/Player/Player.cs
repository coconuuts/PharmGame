using UnityEngine;
using Systems.Persistence;
using Systems.SaveLoad;
using Systems.Inventory;

    public class PlayerEntity : MonoBehaviour, IBind<PlayerData>
    {
        [field: SerializeField] public SerializableGuid Id { get; set; } = SerializableGuid.NewGuid();
        [SerializeField] PlayerData data;
        public void Bind(PlayerData data)
        {
            this.data = data;
            this.data.Id = Id;
            transform.position = data.position;
            transform.rotation = data.rotation;
        }

        void Update()
        {
            data.position = transform.position;
            data.rotation = transform.rotation;
        }
    }