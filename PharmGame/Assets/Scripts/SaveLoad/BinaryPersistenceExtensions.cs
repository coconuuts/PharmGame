using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Systems.Inventory;
using Systems.SaveLoad;
using Systems.Player;
using Systems.Persistence;
using Game.NPC.TI;
using Game.NPC.Decisions;
using Game.Navigation;
using Game.Prescriptions;
using Game.Utilities;
using CustomerManagement;

namespace Systems.Persistence
{
    public static class BinaryPersistenceExtensions
    {
        // ==========================================
        // PRIMITIVES & UNITY STRUCTS
        // ==========================================
        public static void Write(this BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static void Write(this BinaryWriter writer, Quaternion q)
        {
            writer.Write(q.x);
            writer.Write(q.y);
            writer.Write(q.z);
            writer.Write(q.w);
        }

        public static Quaternion ReadQuaternion(this BinaryReader reader)
        {
            return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static void WriteNullableString(this BinaryWriter writer, string value)
        {
            bool hasValue = value != null;
            writer.Write(hasValue);
            if (hasValue) writer.Write(value);
        }

        public static string ReadNullableString(this BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }

        public static void WriteNullableVector3(this BinaryWriter writer, Vector3? v)
        {
            writer.Write(v.HasValue);
            if (v.HasValue) writer.Write(v.Value);
        }

        public static Vector3? ReadNullableVector3(this BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadVector3() : (Vector3?)null;
        }

        // ==========================================
        // PLAYER DATA
        // ==========================================
        public static void Write(this BinaryWriter writer, PlayerData data)
        {
            if (data == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            writer.Write(data.Id);
            writer.Write(data.position);
            writer.Write(data.rotation);
            writer.Write(data.cameraPitch);
        }

        public static PlayerData ReadPlayerData(this BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return new PlayerData
            {
                Id = reader.ReadSerializableGuid(),
                position = reader.ReadVector3(),
                rotation = reader.ReadQuaternion(),
                cameraPitch = reader.ReadSingle()
            };
        }

        // ==========================================
        // INVENTORY DATA
        // ==========================================
        public static void Write(this BinaryWriter writer, ItemData item)
        {
            if (item == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            writer.Write(item.Id);
            writer.Write(item.ItemDetailsId);
            writer.Write(item.quantity);
            writer.Write(item.health);
            writer.Write(item.usageEventsSinceLastLoss);
            writer.Write(item.currentMagazineHealth);
            writer.Write(item.totalReserveHealth);
            writer.Write(item.isReloading);
            writer.Write(item.reloadStartTime);
            writer.WriteNullableString(item.patientNameTag);
        }

        public static ItemData ReadItemData(this BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return new ItemData
            {
                Id = reader.ReadSerializableGuid(),
                ItemDetailsId = reader.ReadSerializableGuid(),
                quantity = reader.ReadInt32(),
                health = reader.ReadInt32(),
                usageEventsSinceLastLoss = reader.ReadInt32(),
                currentMagazineHealth = reader.ReadInt32(),
                totalReserveHealth = reader.ReadInt32(),
                isReloading = reader.ReadBoolean(),
                reloadStartTime = reader.ReadSingle(),
                patientNameTag = reader.ReadNullableString()
            };
        }

        public static void Write(this BinaryWriter writer, InventoryData inv)
        {
            if (inv == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            writer.Write(inv.Id);
            int count = inv.items?.Count ?? 0;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                writer.Write(inv.items[i]);
            }
        }

        public static InventoryData ReadInventoryData(this BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            var inv = new InventoryData { Id = reader.ReadSerializableGuid() };
            int count = reader.ReadInt32();
            inv.items = new List<ItemData>(count);
            for (int i = 0; i < count; i++)
            {
                inv.items.Add(reader.ReadItemData());
            }
            return inv;
        }

        // ==========================================
        // INTERACTABLE DATA
        // ==========================================
        public static void Write(this BinaryWriter writer, InteractableObjectData interactable)
        {
            writer.Write(interactable.Id);
            writer.Write(interactable.IsStateOn);
        }

        public static InteractableObjectData ReadInteractableObjectData(this BinaryReader reader)
        {
            return new InteractableObjectData
            {
                Id = reader.ReadSerializableGuid(),
                IsStateOn = reader.ReadBoolean()
            };
        }

        // ==========================================
        // PRESCRIPTION DATA
        // ==========================================
        public static void Write(this BinaryWriter writer, PrescriptionOrder order)
        {
            writer.WriteNullableString(order.patientName);
            writer.WriteNullableString(order.prescribedDrug);
            writer.Write(order.dosePerDay);
            writer.Write(order.lengthOfTreatmentDays);
            writer.Write(order.illegalOrder);
            writer.Write(order.moneyWorth);
        }

        public static PrescriptionOrder ReadPrescriptionOrder(this BinaryReader reader)
        {
            return new PrescriptionOrder
            {
                patientName = reader.ReadNullableString(),
                prescribedDrug = reader.ReadNullableString(),
                dosePerDay = reader.ReadInt32(),
                lengthOfTreatmentDays = reader.ReadInt32(),
                illegalOrder = reader.ReadBoolean(),
                moneyWorth = reader.ReadSingle()
            };
        }

        public static void Write(this BinaryWriter writer, PlayerPrescriptionData data)
        {
            if (data == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            writer.Write(data.Id);
            writer.Write(data.HasActiveOrder);
            if (data.HasActiveOrder)
            {
                writer.Write(data.ActiveOrder);
            }
        }

        public static PlayerPrescriptionData ReadPlayerPrescriptionData(this BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            var data = new PlayerPrescriptionData
            {
                Id = reader.ReadSerializableGuid(),
                HasActiveOrder = reader.ReadBoolean()
            };
            if (data.HasActiveOrder)
            {
                data.ActiveOrder = reader.ReadPrescriptionOrder();
            }
            return data;
        }

        public static void Write(this BinaryWriter writer, PrescriptionManagerData data)
        {
            if (data == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            writer.Write(data.Id);
            writer.Write(data.OrdersGeneratedToday);
            int unassignedCount = data.UnassignedOrders?.Count ?? 0;
            writer.Write(unassignedCount);
            for (int i = 0; i < unassignedCount; i++)
            {
                writer.Write(data.UnassignedOrders[i]);
            }
            int readyCount = data.ReadyOrders?.Count ?? 0;
            writer.Write(readyCount);
            for (int i = 0; i < readyCount; i++)
            {
                writer.WriteNullableString(data.ReadyOrders[i]);
            }
        }

        public static PrescriptionManagerData ReadPrescriptionManagerData(this BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            var data = new PrescriptionManagerData
            {
                Id = reader.ReadSerializableGuid(),
                OrdersGeneratedToday = reader.ReadBoolean()
            };
            int unassignedCount = reader.ReadInt32();
            data.UnassignedOrders = new List<PrescriptionOrder>(unassignedCount);
            for (int i = 0; i < unassignedCount; i++)
            {
                data.UnassignedOrders.Add(reader.ReadPrescriptionOrder());
            }
            int readyCount = reader.ReadInt32();
            data.ReadyOrders = new List<string>(readyCount);
            for (int i = 0; i < readyCount; i++)
            {
                data.ReadyOrders.Add(reader.ReadNullableString());
            }
            return data;
        }

        // ==========================================
        // TIME RANGE
        // ==========================================
        public static void Write(this BinaryWriter writer, TimeRange range)
        {
            // Use public PascalCase properties to access time fields
            writer.Write(range.StartHour);
            writer.Write(range.StartMinute);
            writer.Write(range.EndHour);
            writer.Write(range.EndMinute);
        }

        public static TimeRange ReadTimeRange(this BinaryReader reader)
        {
            return new TimeRange(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()
            );
        }

        // ==========================================
        // DECISION OPTION
        // ==========================================
        public static void Write(this BinaryWriter writer, DecisionOption option)
        {
            writer.WriteNullableString(option.TargetStateEnumKey);
            writer.WriteNullableString(option.TargetStateEnumType);
            writer.WriteNullableString(option.PathAsset != null ? option.PathAsset.PathID : null);
            writer.Write(option.StartIndex);
            writer.Write(option.FollowReverse);
        }

        public static DecisionOption ReadDecisionOption(this BinaryReader reader)
        {
            string targetStateKey = reader.ReadNullableString();
            string targetStateType = reader.ReadNullableString();
            string pathId = reader.ReadNullableString();
            int startIndex = reader.ReadInt32();
            bool followReverse = reader.ReadBoolean();

            PathSO pathAsset = null;
            if (!string.IsNullOrEmpty(pathId))
            {
                pathAsset = Resources.Load<PathSO>(pathId);
            }

            return new DecisionOption(targetStateKey, targetStateType, pathAsset, startIndex, followReverse);
        }

        // ==========================================
        // TI NPC DATA
        // ==========================================
        public static void Write(this BinaryWriter writer, TiNpcData npc)
        {
            if (npc == null)
            {
                writer.Write(false);
                return;
            }
            writer.Write(true);
            writer.WriteNullableString(npc.Id);
            writer.Write(npc.HomePosition);
            writer.Write(npc.HomeRotation);
            writer.Write(npc.CurrentWorldPosition);
            writer.Write(npc.CurrentWorldRotation);
            writer.WriteNullableString(npc.CurrentStateEnumKey);
            writer.WriteNullableString(npc.CurrentStateEnumType);

            // Saved Items
            int invCount = npc.savedInventoryItems?.Count ?? 0;
            writer.Write(invCount);
            for (int i = 0; i < invCount; i++)
            {
                writer.Write(npc.savedInventoryItems[i]);
            }

            // Schedule & Status
            writer.Write(npc.startDay);
            writer.Write(npc.endDay);
            writer.Write(npc.canStartDay);
            writer.Write(npc.savedBrowseLocationIndex);
            writer.Write(npc.savedQueueIndex);
            writer.Write((int)npc.savedQueueType);

            // Unique Decision Options
            var decisions = npc.uniqueDecisionOptions?.entries;
            int decisionCount = decisions?.Count ?? 0;
            writer.Write(decisionCount);
            for (int i = 0; i < decisionCount; i++)
            {
                writer.WriteNullableString(decisions[i].decisionPointID);
                writer.Write(decisions[i].decisionOption);
            }

            // Simulation Fields
            writer.WriteNullableVector3(npc.simulatedTargetPosition);
            writer.Write(npc.simulatedStateTimer);

            // Interruption Fields
            writer.Write(npc.isInterrupted);
            writer.WriteNullableString(npc.interruptedStateEnumKey);
            writer.WriteNullableString(npc.interruptedStateEnumType);
            writer.Write(npc.wasInterruptedFromPath);
            writer.WriteNullableString(npc.interruptedPathID);
            writer.Write(npc.interruptedWaypointIndex);
            writer.Write(npc.interruptedFollowReverse);

            // Path Simulation
            writer.WriteNullableString(npc.simulatedPathID);
            writer.Write(npc.simulatedWaypointIndex);
            writer.Write(npc.simulatedFollowReverse);
            writer.Write(npc.isFollowingPathBasic);

            // Cashier Simulation
            writer.WriteNullableString(npc.simulatedProcessingCustomerTiId);
            writer.Write(npc.simulatedTransactionValue);
            writer.Write(npc.simulatedProcessingTimeRemaining);

            // Day Start Behaviors
            writer.Write(npc.usePathForDayStart);
            writer.WriteNullableString(npc.DayStartActiveStateEnumKey);
            writer.WriteNullableString(npc.DayStartActiveStateEnumType);
            writer.WriteNullableString(npc.DayStartPathID);
            writer.Write(npc.DayStartStartIndex);
            writer.Write(npc.DayStartFollowReverse);

            // Prescriptions
            writer.Write(npc.pendingPrescription);
            writer.Write(npc.assignedOrder);
        }

        public static TiNpcData ReadTiNpcData(this BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;

            string id = reader.ReadNullableString();
            Vector3 homePos = reader.ReadVector3();
            Quaternion homeRot = reader.ReadQuaternion();

            var npc = new TiNpcData(id, homePos, homeRot, null)
            {
                CurrentWorldPosition = reader.ReadVector3(),
                CurrentWorldRotation = reader.ReadQuaternion(),
                CurrentStateEnumKey = reader.ReadNullableString(),
                CurrentStateEnumType = reader.ReadNullableString()
            };

            int invCount = reader.ReadInt32();
            npc.savedInventoryItems = new List<ItemData>(invCount);
            for (int i = 0; i < invCount; i++)
            {
                npc.savedInventoryItems.Add(reader.ReadItemData());
            }

            npc.startDay = reader.ReadTimeRange();
            npc.endDay = reader.ReadTimeRange();
            npc.canStartDay = reader.ReadBoolean();
            npc.savedBrowseLocationIndex = reader.ReadInt32();
            npc.savedQueueIndex = reader.ReadInt32();
            npc.savedQueueType = (QueueType)reader.ReadInt32();

            int decisionCount = reader.ReadInt32();
            npc.uniqueDecisionOptions = new SerializableDecisionOptionDictionary();
            for (int i = 0; i < decisionCount; i++)
            {
                npc.uniqueDecisionOptions.entries.Add(new SerializableDecisionOptionDictionary.KeyValuePair
                {
                    decisionPointID = reader.ReadNullableString(),
                    decisionOption = reader.ReadDecisionOption()
                });
            }

            npc.simulatedTargetPosition = reader.ReadNullableVector3();
            npc.simulatedStateTimer = reader.ReadSingle();

            npc.isInterrupted = reader.ReadBoolean();
            npc.interruptedStateEnumKey = reader.ReadNullableString();
            npc.interruptedStateEnumType = reader.ReadNullableString();
            npc.wasInterruptedFromPath = reader.ReadBoolean();
            npc.interruptedPathID = reader.ReadNullableString();
            npc.interruptedWaypointIndex = reader.ReadInt32();
            npc.interruptedFollowReverse = reader.ReadBoolean();

            npc.simulatedPathID = reader.ReadNullableString();
            npc.simulatedWaypointIndex = reader.ReadInt32();
            npc.simulatedFollowReverse = reader.ReadBoolean();
            npc.isFollowingPathBasic = reader.ReadBoolean();

            npc.simulatedProcessingCustomerTiId = reader.ReadNullableString();
            npc.simulatedTransactionValue = reader.ReadSingle();
            npc.simulatedProcessingTimeRemaining = reader.ReadSingle();

            npc.usePathForDayStart = reader.ReadBoolean();
            npc.dayStartActiveStateEnumKey = reader.ReadNullableString();
            npc.dayStartActiveStateEnumType = reader.ReadNullableString();
            npc.dayStartPathID = reader.ReadNullableString();
            npc.dayStartStartIndex = reader.ReadInt32();
            npc.dayStartFollowReverse = reader.ReadBoolean();

            npc.pendingPrescription = reader.ReadBoolean();
            npc.assignedOrder = reader.ReadPrescriptionOrder();

            return npc;
        }
    }
}