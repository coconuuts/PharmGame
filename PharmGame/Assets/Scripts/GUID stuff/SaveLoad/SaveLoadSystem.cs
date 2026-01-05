using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using Systems.SaveLoad;
using Game.NPC.TI; 
using Game.NPC;    
using CustomerManagement.Tracking; 

namespace Systems.Persistence {
    [Serializable] public class GameData { 
        public string Name;
        public string CurrentLevelName;
        public PlayerData playerData;
        public List<InventoryData> inventories;
        public List<TiNpcData> tiNpcDataList;
        public List<TransientNpcSnapshotData> transientNpcSnapshots;
    }
        
    public interface ISaveable  {
        SerializableGuid Id { get; set; }
    }

    public interface ISavableComponent 
    {
        SerializableGuid Id { get; } 
        ISaveable CreateSaveData(); 
        void Bind(ISaveable data);
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

            if (gameData == null) gameData = new GameData();
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();
            if (gameData.tiNpcDataList == null) gameData.tiNpcDataList = new List<TiNpcData>();
            if (gameData.transientNpcSnapshots == null) gameData.transientNpcSnapshots = new List<TransientNpcSnapshotData>();
        }
        
        void Start() => NewGame(); 

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu") return;

            Debug.Log($"SaveLoadSystem: Scene '{scene.name}' loaded. Binding data...");

            // 1. Bind Player
            Bind<PlayerEntity, PlayerData>(gameData.playerData);

            // 2. Bind Generic Inventories
            // FIX: Use FindObjectsByType with SortMode.None
            var inventoriesInScene = FindObjectsByType<Systems.Inventory.Inventory>(FindObjectsSortMode.None);
            
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();

            foreach (var invComponent in inventoriesInScene)
            {
                InventoryData invData = gameData.inventories.FirstOrDefault(d => d.Id == invComponent.Id);
                if (invData == null)
                {
                    invData = new InventoryData
                    {
                        Id = invComponent.Id,
                        allowedLabels = new List<ItemLabel>(invComponent.AllowedLabels),
                        allowAllIfListEmpty = invComponent.AllowAllIfListEmpty,
                    };
                    gameData.inventories.Add(invData);
                }
                invComponent.Bind(invData);
            }

            // --- PHASE 4: NPC RESTORATION ---
            
            // 3. Restore TI NPCs
            if (TiNpcPersistenceBridge.Instance != null)
            {
                TiNpcPersistenceBridge.Instance.LoadAllTiNpcData(gameData.tiNpcDataList);
            }
            else
            {
                // FIX: Use FindFirstObjectByType
                var bridge = FindFirstObjectByType<TiNpcPersistenceBridge>();
                if (bridge != null) bridge.LoadAllTiNpcData(gameData.tiNpcDataList);
            }

            // 4. Restore Transient NPCs
            if (TransientNpcPersistenceBridge.Instance != null)
            {
                TransientNpcPersistenceBridge.Instance.LoadSnapshots(gameData.transientNpcSnapshots);
            }
            else
            {
                // FIX: Use FindFirstObjectByType
                var bridge = FindFirstObjectByType<TransientNpcPersistenceBridge>();
                if (bridge != null) bridge.LoadSnapshots(gameData.transientNpcSnapshots);
            }
            
            Debug.Log("SaveLoadSystem: Data binding complete.");
        }
        
        void Bind<T, TData>(TData data) where T : MonoBehaviour, IBind<TData> where TData : ISaveable, new() {
            // FIX: Use FindFirstObjectByType (more efficient than getting all and taking first)
            var entity = FindFirstObjectByType<T>();
            if (entity != null) {
                if (data == null) data = new TData { Id = entity.Id };
                entity.Bind(data);
            }
        }

        void Bind<T, TData>(List<TData> datas) where T: MonoBehaviour, IBind<TData> where TData : ISaveable, new() {
            // FIX: Use FindObjectsByType with SortMode.None
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
            Debug.Log("SaveLoadSystem: New Game.");
            gameData = new GameData {
                Name = "Game",
                CurrentLevelName = "SampleScene",
                playerData = new PlayerData(),
                inventories = new List<InventoryData>(),
                tiNpcDataList = new List<TiNpcData>(),
                transientNpcSnapshots = new List<TransientNpcSnapshotData>()
            };
            SceneManager.LoadScene(gameData.CurrentLevelName);
        }
        
        public void SaveGame() 
        {
            Debug.Log($"SaveLoadSystem: Saving game '{gameData.Name}'...");
            
            // 1. Clear generic lists
            gameData.inventories.Clear(); 
            
            // 2. Clear NPC lists to prevent duplication
            gameData.tiNpcDataList.Clear();
            gameData.transientNpcSnapshots.Clear();
            
            // 3. Find and Iterate Savable Components
            // FIX: Use FindObjectsByType with SortMode.None
            var allSceneMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
            var savableComponents = allSceneMonoBehaviours
                .Where(mb => mb is ISavableComponent)
                .Cast<ISavableComponent>()
                .ToList();

            foreach (var component in savableComponents) {
                ISaveable data = component.CreateSaveData();
                
                if (data != null)
                {
                    if (data is InventoryData invData) {
                        gameData.inventories.Add(invData);
                    }
                    // Add other types here
                }
            }
            
            // 4. Bind Singletons
            Bind<PlayerEntity, PlayerData>(gameData.playerData); 

            // --- PHASE 4: NPC SAVING ---

            // 5. Gather TI Data (All Active Flushed + Inactive Simulated)
            if (TiNpcPersistenceBridge.Instance != null)
            {
                gameData.tiNpcDataList = TiNpcPersistenceBridge.Instance.GetAllTiNpcData();
                Debug.Log($"SaveLoadSystem: Saved {gameData.tiNpcDataList.Count} TI NPCs.");
            }
            else
            {
                 // FIX: Use FindFirstObjectByType
                 var bridge = FindFirstObjectByType<TiNpcPersistenceBridge>();
                 if (bridge != null) gameData.tiNpcDataList = bridge.GetAllTiNpcData();
            }

            // 6. Gather Transient Snapshots
            if (TransientNpcPersistenceBridge.Instance != null)
            {
                gameData.transientNpcSnapshots = TransientNpcPersistenceBridge.Instance.GetSnapshots();
                Debug.Log($"SaveLoadSystem: Saved {gameData.transientNpcSnapshots.Count} Transient Snapshots.");
            }
            else
            {
                 // FIX: Use FindFirstObjectByType
                 var bridge = FindFirstObjectByType<TransientNpcPersistenceBridge>();
                 if (bridge != null) gameData.transientNpcSnapshots = bridge.GetSnapshots();
            }

            // 7. Write to Disk
            dataService.Save(gameData);
            Debug.Log("SaveLoadSystem: Save Complete.");
        }

        public void LoadGame(string gameName) {
            Debug.Log($"SaveLoadSystem: Loading '{gameName}'...");
            gameData = dataService.Load(gameName);

            if (String.IsNullOrWhiteSpace(gameData.CurrentLevelName)) gameData.CurrentLevelName = "SampleScene";
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();
            
            // Ensure lists exist
            if (gameData.tiNpcDataList == null) gameData.tiNpcDataList = new List<TiNpcData>();
            if (gameData.transientNpcSnapshots == null) gameData.transientNpcSnapshots = new List<TransientNpcSnapshotData>();

            SceneManager.LoadScene(gameData.CurrentLevelName);
        }
        
        public void ReloadGame() => LoadGame(gameData.Name);
        public void DeleteGame(string gameName) => dataService.Delete(gameName);
    }
}