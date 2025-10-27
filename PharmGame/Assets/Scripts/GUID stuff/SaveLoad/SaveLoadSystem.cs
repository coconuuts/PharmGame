using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using Systems.SaveLoad;

namespace Systems.Persistence {
    [Serializable] public class GameData { 
        public string Name;
        public string CurrentLevelName;
        public PlayerData playerData;
        public List<InventoryData> inventories;
    }
        
    public interface ISaveable  {
        SerializableGuid Id { get; set; }
    }
    
    public interface IBind<TData> where TData : ISaveable {
        SerializableGuid Id { get; set; }
        void Bind(TData data);
    }
    
    public class SaveLoadSystem : PersistentSingleton<SaveLoadSystem> {
        [SerializeField] public GameData gameData;

        IDataService dataService;

        protected override void Awake() {
            base.Awake();
            dataService = new FileDataService(new JsonSerializer());

            // Ensure gameData and its inventories list are initialized
            if (gameData == null) {
                gameData = new GameData();
            }
            if (gameData.inventories == null) {
                gameData.inventories = new List<InventoryData>();
            }
        }
        
        void Start() => NewGame(); // Could change to LoadGame("LastSave") to auto-load

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu") return;

            Debug.Log($"SaveLoadSystem: Scene '{scene.name}' loaded. Attempting to bind data.");

            Bind<PlayerEntity, PlayerData>(gameData.playerData);

            // Find all Inventory components in the newly loaded scene
            var inventoriesInScene = FindObjectsByType<Systems.Inventory.Inventory>(FindObjectsSortMode.None);
            Debug.Log($"SaveLoadSystem: Found {inventoriesInScene.Length} Inventory components in scene.");

            // Ensure gameData.inventories is initialized (e.g., if loading a corrupted save or a new game)
            if (gameData.inventories == null)
            {
                gameData.inventories = new List<InventoryData>();
            }

            foreach (var invComponent in inventoriesInScene)
            {
                // Find corresponding InventoryData from loaded gameData
                InventoryData invData = gameData.inventories.FirstOrDefault(d => d.Id == invComponent.Id);

                if (invData == null)
                {
                    Debug.LogWarning($"SaveLoadSystem: No saved data found for Inventory '{invComponent.gameObject.name}' (ID: {invComponent.Id}). Initializing with empty/default data for it. (This is normal for new components or if it wasn't saved before).", invComponent.gameObject);
                    // If no data found, create a *new* empty InventoryData for this component for the *next* save.
                    // The Bind method will then initialize with empty data.
                    invData = new InventoryData
                    {
                        Id = invComponent.Id,
                        allowedLabels = new List<ItemLabel>(invComponent.AllowedLabels), // Use current defaults
                        allowAllIfListEmpty = invComponent.AllowAllIfListEmpty,
                    };
                    gameData.inventories.Add(invData); // Add to the list to be managed for next save
                }

                // Bind the data to the component
                invComponent.Bind(invData);
            }

            // TODO: Add binding for other savable components (e.g., player, world objects)
            // Example:
            // Bind<PlayerCharacter, PlayerData>(gameData.playerData);
            // Bind<WorldObject, WorldObjectData>(gameData.worldObjects);
        }
        
        // Helper to convert an Item instance to ItemData for saving
        private ItemData ConvertItemToItemData(Item item) {
            if (item == null || item.details == null) return null;

            return new ItemData {
                Id = item.Id,
                ItemDetailsId = item.details.Id,
                quantity = item.quantity,
                health = item.health,
                usageEventsSinceLastLoss = item.usageEventsSinceLastLoss,
                currentMagazineHealth = item.currentMagazineHealth,
                totalReserveHealth = item.totalReserveHealth,
                isReloading = item.isReloading,
                reloadStartTime = item.reloadStartTime,
                patientNameTag = item.patientNameTag
            };
        }

        // Helper to create InventoryData from an Inventory component for saving
        private InventoryData CreateInventoryDataFromComponent(Systems.Inventory.Inventory invComponent) 
        {
            InventoryData invData = new InventoryData {
                Id = invComponent.Id,
                allowedLabels = new List<ItemLabel>(invComponent.AllowedLabels), // Copy the list
                allowAllIfListEmpty = invComponent.AllowAllIfListEmpty,
                items = new List<ItemData>()
            };

            if (invComponent.InventoryState != null) {
                // Only save items from physical slots, not the ghost slot.
                // The ghost slot (Length - 1) is transient and should not be persisted.
                for (int i = 0; i < invComponent.Combiner.PhysicalSlotCount; i++) {
                    Item item = invComponent.InventoryState[i];
                    invData.items.Add(ConvertItemToItemData(item));
                }
            }
            return invData;
        }
        
        void Bind<T, TData>(TData data) where T : MonoBehaviour, IBind<TData> where TData : ISaveable, new() {
            var entity = FindObjectsByType<T>(FindObjectsSortMode.None).FirstOrDefault();
            if (entity != null) {
                if (data == null) {
                    data = new TData { Id = entity.Id };
                }
                entity.Bind(data);
            }
        }

        void Bind<T, TData>(List<TData> datas) where T: MonoBehaviour, IBind<TData> where TData : ISaveable, new() {
            var entities = FindObjectsByType<T>(FindObjectsSortMode.None);

            foreach(var entity in entities) {
                var data = datas.FirstOrDefault(d=> d.Id == entity.Id);
                if (data == null) {
                    data = new TData { Id = entity.Id };
                    datas.Add(data); 
                }
                entity.Bind(data);
            }
        }

        public void NewGame() {
            Debug.Log("SaveLoadSystem: Starting New Game.");
            gameData = new GameData {
                Name = "Game",
                CurrentLevelName = "SampleScene",
                playerData = new PlayerData(),
                inventories = new List<InventoryData>() // Initialize for a new game
            };
            // The scene will be loaded, and OnSceneLoaded will then initialize/bind inventories.
            SceneManager.LoadScene(gameData.CurrentLevelName);
        }
        
        public void SaveGame() {
            Debug.Log($"SaveLoadSystem: Saving game '{gameData.Name}'.");
            
            // Rebuild the inventory list from current scene state
            gameData.inventories.Clear(); // Clear existing saved data

            // Find all active Inventory components in the scene and convert their state to InventoryData
            var inventoriesInScene = FindObjectsByType<Systems.Inventory.Inventory>(FindObjectsSortMode.None);
            foreach (var invComponent in inventoriesInScene) {
                gameData.inventories.Add(CreateInventoryDataFromComponent(invComponent));
            }
            dataService.Save(gameData);
            Debug.Log($"SaveLoadSystem: Game '{gameData.Name}' saved successfully.");
        }

        public void LoadGame(string gameName) {
            Debug.Log($"SaveLoadSystem: Loading game '{gameName}'.");
            gameData = dataService.Load(gameName);

            if (String.IsNullOrWhiteSpace(gameData.CurrentLevelName)) {
                gameData.CurrentLevelName = "SampleScene";
            }
            
            // Ensure inventories list is initialized, even if empty in save file
            if (gameData.inventories == null) {
                gameData.inventories = new List<InventoryData>();
            }

            // The scene will be loaded, and OnSceneLoaded will handle binding the loaded data.
            SceneManager.LoadScene(gameData.CurrentLevelName);
            Debug.Log($"SaveLoadSystem: Game '{gameName}' loaded. Scene '{gameData.CurrentLevelName}' is loading.");
        }
        
        public void ReloadGame() => LoadGame(gameData.Name);

        public void DeleteGame(string gameName) => dataService.Delete(gameName);
    }
}