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
using Systems.Economy;

namespace Systems.Persistence {
    [Serializable] public class GameData : ISaveable
    { 
        public SerializableGuid Id { get; set; } = SerializableGuid.Empty;
        public string Name;
        public string CurrentLevelName;
        public PlayerData playerData;
        public List<InventoryData> inventories;
        public List<TiNpcData> tiNpcDataList;
        public List<TransientNpcSnapshotData> transientNpcSnapshots;
        public List<InteractableObjectData> worldInteractables;

        // Global Variables
        public float PlayerCleanMoney;
        public float PlayerDirtyMoney;
        public int CurrentDay;
        public long TimeTicks;

        // Progression 
        public List<string> UnlockedUpgradeIds; 

        // Constructor to ensure defaults
        public GameData()
        {
            Name = "New Game";
            CurrentLevelName = "SampleScene";
            PlayerCleanMoney = 0;
            PlayerDirtyMoney = 0;
            CurrentDay = 1;
            TimeTicks = 0; 
            worldInteractables = new List<InteractableObjectData>();
            
            UnlockedUpgradeIds = new List<string>();
            playerData = new PlayerData();
            inventories = new List<InventoryData>();
            tiNpcDataList = new List<TiNpcData>();
            transientNpcSnapshots = new List<TransientNpcSnapshotData>();
        }
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

    [Serializable]
    public class InteractableObjectData : ISaveable
    {
        [SerializeField] private SerializableGuid _id;
        
        public SerializableGuid Id { 
            get => _id; 
            set => _id = value; 
        }
        public bool IsStateOn;
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
        
        void Start() 
        {
            // If we have no data (first run), ensure we have a valid empty container.
            // We do NOT call NewGame() here because it reloads the scene and breaks references.
            if (gameData == null) gameData = new GameData();
            
            // In a real build, you would call NewGame() from a Main Menu button.
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu") return;

            Debug.Log($"SaveLoadSystem: Scene '{scene.name}' loaded. Starting Data Binding Sequence...");

            // SYSTEM LEVEL BINDINGS 
            // Time must be first to set lighting/skybox before the screen fades in
            Bind<TimeManager, GameData>(gameData);
            
            // Economy updates the UI and Wallet SO immediately
            Bind<EconomyManager, GameData>(gameData);
            
            // Upgrades unlock recipes/shelves before we spawn physical objects
            Bind<UpgradeManager, GameData>(gameData);

            // WORLD STATE BINDINGS
            // Bind the Player's position and stats
            Bind<PlayerEntity, PlayerData>(gameData.playerData);

            // Bind Generic World Interactables (Light Switches, Cash Register States)
            if (gameData.worldInteractables == null) gameData.worldInteractables = new List<InteractableObjectData>();
            
            var allSavables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                              .OfType<ISavableComponent>();

            foreach (var component in allSavables)
            {
                // We handle Inventories specifically below, so skip them here if needed,
                // OR ensure InteractableObjectData doesn't conflict. 
                // For now, we only look for InteractableObjectData matches.
                InteractableObjectData data = gameData.worldInteractables.FirstOrDefault(d => d.Id == component.Id);
                if (data != null)
                {
                    component.Bind(data);
                }
            }

            // Bind Generic Inventories
            var inventoriesInScene = FindObjectsByType<Systems.Inventory.Inventory>(FindObjectsSortMode.None);
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();

            foreach (var invComponent in inventoriesInScene)
            {
                InventoryData invData = gameData.inventories.FirstOrDefault(d => d.Id == invComponent.Id);
                if (invData == null)
                {
                    // If no data exists, create fresh data for this inventory
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

            // NPC RESTORATION 
            // Restore TI NPCs (Persistent Staff/Unique chars)
            if (TiNpcPersistenceBridge.Instance != null)
            {
                TiNpcPersistenceBridge.Instance.LoadAllTiNpcData(gameData.tiNpcDataList);
            }
            else
            {
                var bridge = FindFirstObjectByType<TiNpcPersistenceBridge>();
                if (bridge != null) bridge.LoadAllTiNpcData(gameData.tiNpcDataList);
            }

            // Restore Transient NPCs (Customers)
            if (TransientNpcPersistenceBridge.Instance != null)
            {
                TransientNpcPersistenceBridge.Instance.LoadSnapshots(gameData.transientNpcSnapshots);
            }
            else
            {
                var bridge = FindFirstObjectByType<TransientNpcPersistenceBridge>();
                if (bridge != null) bridge.LoadSnapshots(gameData.transientNpcSnapshots);
            }
            
            Debug.Log("SaveLoadSystem: Data binding sequence complete.");
        }

        public void NewGame() {
            Debug.Log("SaveLoadSystem: Initializing New Game...");
            
            // 1. Create Fresh Data
            gameData = new GameData {
                Name = "New Game",
                CurrentLevelName = "SampleScene", // Ensure this matches your actual gameplay scene name
                
                // Defaults
                PlayerCleanMoney = 50f, // Give player some starting cash?
                PlayerDirtyMoney = 0f,
                CurrentDay = 1,
                TimeTicks = 0, // TimeManager will see 0 and likely use its default "Start Hour"
                
                // Empty Lists
                UnlockedUpgradeIds = new List<string>(),
                worldInteractables = new List<InteractableObjectData>(),
                playerData = new PlayerData(),
                inventories = new List<InventoryData>(),
                tiNpcDataList = new List<TiNpcData>(),
                transientNpcSnapshots = new List<TransientNpcSnapshotData>()
            };

            // 2. Load the Scene
            // This triggers OnSceneLoaded, which will Bind() this fresh data to all managers,
            // effectively resetting them (e.g. EconomyManager will set Wallet to 50).
            SceneManager.LoadScene(gameData.CurrentLevelName);
        }
        
        void Bind<T, TData>(TData data) where T : MonoBehaviour, IBind<TData> where TData : ISaveable, new() {
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
        
        public void SaveGame() 
        {
            Debug.Log($"SaveLoadSystem: Saving game '{gameData.Name}'...");
            
            // 1. Clear generic lists
            gameData.inventories.Clear(); 
            gameData.worldInteractables.Clear();
            
            // 2. Clear NPC lists to prevent duplication
            gameData.tiNpcDataList.Clear();
            gameData.transientNpcSnapshots.Clear();
            
            // 3. Find and Iterate Savable Components
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
                    else if (data is InteractableObjectData interactableData) {
                        gameData.worldInteractables.Add(interactableData);
                    }
                    // Add other types here
                }
            }
            
            // 4. Bind Singletons
            Bind<TimeManager, GameData>(gameData);
            Bind<EconomyManager, GameData>(gameData);
            Bind<UpgradeManager, GameData>(gameData);
            Bind<PlayerEntity, PlayerData>(gameData.playerData); 

            // NPC SAVING
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