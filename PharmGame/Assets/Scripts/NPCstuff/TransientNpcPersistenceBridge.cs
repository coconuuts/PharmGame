using UnityEngine;
using System.Collections.Generic;
using CustomerManagement.Tracking;

namespace Game.NPC
{
    /// <summary>
    /// Bridge between SaveLoadSystem and TransientNpcTracker.
    /// </summary>
    public class TransientNpcPersistenceBridge : MonoBehaviour
    {
        public static TransientNpcPersistenceBridge Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public List<TransientNpcSnapshotData> GetSnapshots()
        {
             if (TransientNpcTracker.Instance != null)
             {
                  return TransientNpcTracker.Instance.TakeSnapshot();
             }
             return new List<TransientNpcSnapshotData>();
        }

        public void LoadSnapshots(List<TransientNpcSnapshotData> data)
        {
             if (TransientNpcTracker.Instance != null)
             {
                  TransientNpcTracker.Instance.RestoreSnapshots(data);
             }
        }
    }
}