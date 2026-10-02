using System;
using System.Collections.Generic;
using System.IO;
using Game.NPC.TI;
using Systems.Inventory;

namespace Systems.Persistence
{
    public class BinarySerializer : ISerializer
    {
        public byte[] Serialize<T>(T obj)
        {
            if (obj is not GameData gameData)
                throw new NotSupportedException($"Type {typeof(T)} is not configured for binary serialization.");

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // Metadata Header
            writer.Write(gameData.Id);
            writer.WriteNullableString(gameData.Name);
            writer.WriteNullableString(gameData.CharacterName);
            writer.WriteNullableString(gameData.CurrentLevelName);
            writer.WriteNullableString(gameData.LastSaveDate);
            writer.Write(gameData.SaveSlotIndex);

            // Global State & Progression
            writer.Write(gameData.PlayerCleanMoney);
            writer.Write(gameData.PlayerDirtyMoney);
            writer.Write(gameData.CurrentDay);
            writer.Write(gameData.TimeTicks);
            writer.Write(gameData.TotalPlayTimeSeconds);

            // Unlocked Upgrade IDs
            int upgradeCount = gameData.UnlockedUpgradeIds?.Count ?? 0;
            writer.Write(upgradeCount);
            for (int i = 0; i < upgradeCount; i++)
            {
                writer.WriteNullableString(gameData.UnlockedUpgradeIds[i]);
            }

            // Sub-structures
            writer.Write(gameData.playerData);
            writer.Write(gameData.playerPrescriptionData);
            writer.Write(gameData.prescriptionSystemData);

            // World Interactables
            int interactablesCount = gameData.worldInteractables?.Count ?? 0;
            writer.Write(interactablesCount);
            for (int i = 0; i < interactablesCount; i++)
            {
                writer.Write(gameData.worldInteractables[i]);
            }

            // Inventories
            int invCount = gameData.inventories?.Count ?? 0;
            writer.Write(invCount);
            for (int i = 0; i < invCount; i++)
            {
                writer.Write(gameData.inventories[i]);
            }

            // TI NPCs
            int tiCount = gameData.tiNpcDataList?.Count ?? 0;
            writer.Write(tiCount);
            for (int i = 0; i < tiCount; i++)
            {
                writer.Write(gameData.tiNpcDataList[i]);
            }

            return memoryStream.ToArray();
        }

        public T Deserialize<T>(byte[] bytes)
        {
            if (typeof(T) != typeof(GameData))
                throw new NotSupportedException($"Type {typeof(T)} is not configured for binary deserialization.");

            using var memoryStream = new MemoryStream(bytes);
            using var reader = new BinaryReader(memoryStream);

            var gameData = new GameData
            {
                Id = reader.ReadSerializableGuid(),
                Name = reader.ReadNullableString(),
                CharacterName = reader.ReadNullableString(),
                CurrentLevelName = reader.ReadNullableString(),
                LastSaveDate = reader.ReadNullableString(),
                SaveSlotIndex = reader.ReadInt32(),
                PlayerCleanMoney = reader.ReadSingle(),
                PlayerDirtyMoney = reader.ReadSingle(),
                CurrentDay = reader.ReadInt32(),
                TimeTicks = reader.ReadInt64(),
                TotalPlayTimeSeconds = reader.ReadSingle()
            };

            int upgradeCount = reader.ReadInt32();
            gameData.UnlockedUpgradeIds = new List<string>(upgradeCount);
            for (int i = 0; i < upgradeCount; i++)
            {
                gameData.UnlockedUpgradeIds.Add(reader.ReadNullableString());
            }

            gameData.playerData = reader.ReadPlayerData();
            gameData.playerPrescriptionData = reader.ReadPlayerPrescriptionData();
            gameData.prescriptionSystemData = reader.ReadPrescriptionManagerData();

            int interactablesCount = reader.ReadInt32();
            gameData.worldInteractables = new List<InteractableObjectData>(interactablesCount);
            for (int i = 0; i < interactablesCount; i++)
            {
                gameData.worldInteractables.Add(reader.ReadInteractableObjectData());
            }

            int invCount = reader.ReadInt32();
            gameData.inventories = new List<InventoryData>(invCount);
            for (int i = 0; i < invCount; i++)
            {
                gameData.inventories.Add(reader.ReadInventoryData());
            }

            int tiCount = reader.ReadInt32();
            gameData.tiNpcDataList = new List<TiNpcData>(tiCount);
            for (int i = 0; i < tiCount; i++)
            {
                gameData.tiNpcDataList.Add(reader.ReadTiNpcData());
            }

            return (T)(object)gameData;
        }
    }
}