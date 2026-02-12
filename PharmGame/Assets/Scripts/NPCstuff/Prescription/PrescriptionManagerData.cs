using UnityEngine;
using Game.Prescriptions;
using System;
using System.Collections.Generic;
using Systems.Inventory;

namespace Systems.Persistence {
    [Serializable]
        public class PrescriptionManagerData : ISaveable
        {
            [SerializeField] private SerializableGuid _id;
            public SerializableGuid Id { get => _id; set => _id = value; }

            public List<Game.Prescriptions.PrescriptionOrder> UnassignedOrders = new List<Game.Prescriptions.PrescriptionOrder>();
            public List<string> ReadyOrders = new List<string>(); // Persist the HashSet as a List
            public bool OrdersGeneratedToday;
        }
    }