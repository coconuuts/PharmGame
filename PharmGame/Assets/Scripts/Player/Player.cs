using UnityEngine;
using Systems.Persistence;
using Systems.SaveLoad;
using Systems.CameraControl;
using Systems.Inventory;

    public class PlayerEntity : MonoBehaviour, IBind<PlayerData>
    {
        [field: SerializeField] public SerializableGuid Id { get; set; } = SerializableGuid.NewGuid();
        [SerializeField] PlayerData data;

        public void Bind(PlayerData data)
        {
            this.data = data;
            this.data.Id = Id;

            // If don't disable the CC, the transform.position set might fail
            CharacterController cc = GetComponent<CharacterController>();
            bool wasEnabled = false;
            
            if (cc != null) 
            {
                wasEnabled = cc.enabled;
                cc.enabled = false;
            }

            transform.position = data.position;
            transform.rotation = data.rotation;

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetPitch(data.cameraPitch);
            }

            if (cc != null && wasEnabled) 
            {
                cc.enabled = true;
            }
        }

        void Update()
        {
            if (data != null)
            {
                data.position = transform.position;
                data.rotation = transform.rotation;

                if (CameraManager.Instance != null)
                {
                    data.cameraPitch = CameraManager.Instance.GetPitch();
                }
            }
        }
    }