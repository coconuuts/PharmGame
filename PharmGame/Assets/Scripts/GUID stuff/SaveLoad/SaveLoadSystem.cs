using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using Systems.SaveLoad;
using Game.NPC.TI; 
using Game.NPC;    
using Systems.Economy;
using Systems.SceneManagement;

namespace Systems.Persistence {
    [Serializable] public class GameData : ISaveable
    { 
        public SerializableGuid Id { get; set; } = SerializableGuid.Empty;
        public string Name;
        public string CharacterName;
        public string CurrentLevelName;
        public int SaveSlotIndex = 0;
        public PlayerData playerData;
        public List<InventoryData> inventories;
        public List<TiNpcData> tiNpcDataList;
        public List<Game.NPC.TransientNpcData> transientNpcs;
        public List<InteractableObjectData> worldInteractables;

        // Global Variables
        public float PlayerCleanMoney;
        public float PlayerDirtyMoney;
        public int CurrentDay;
        public long TimeTicks;

        // Progression 
        public float TotalPlayTimeSeconds;
        public List<string> UnlockedUpgradeIds; 

        // Constructor to ensure defaults
        public GameData()
        {
            Name = "New Game";
            CharacterName = "Player";
            CurrentLevelName = "SampleScene";
            SaveSlotIndex = 0;
            PlayerCleanMoney = 0;
            PlayerDirtyMoney = 0;
            CurrentDay = 1;
            TimeTicks = 0; 
            TotalPlayTimeSeconds = 0;
            worldInteractables = new List<InteractableObjectData>();
            
            UnlockedUpgradeIds = new List<string>();
            playerData = new PlayerData();
            inventories = new List<InventoryData>();
            tiNpcDataList = new List<TiNpcData>();
            transientNpcs = new List<Game.NPC.TransientNpcData>();
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
        bool isGameplayActive = false;

        protected override void Awake() {
            base.Awake();

            ItemDatabase.Initialize();

            dataService = new FileDataService(new JsonSerializer());

            if (gameData == null) gameData = new GameData();
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();
            if (gameData.tiNpcDataList == null) gameData.tiNpcDataList = new List<TiNpcData>();
        }
        
        void Start() 
        {
            // If we have no data (first run), ensure we have a valid empty container.
            // We do NOT call NewGame() here because it reloads the scene and breaks references.
            if (gameData == null) gameData = new GameData();

            // Check if we started in a gameplay scene (useful for development/testing directly in scene)
            string currentScene = SceneManager.GetActiveScene().name;
            isGameplayActive = (currentScene != "MainMenu" && currentScene != "Bootstrapper");
            
            // In a real build, you would call NewGame() from a Main Menu button.
        }

        void Update() {
            // Track real-time played while in gameplay scenes (not Menu)
            if (isGameplayActive && gameData != null) {
                gameData.TotalPlayTimeSeconds += Time.unscaledDeltaTime;
            }
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isGameplayActive = (scene.name != "MainMenu" && scene.name != "Bootstrapper");

            if (scene.name == "MainMenu") return;

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

            // --- Restore TINPCs ---
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

            // --- Restore Transient NPCs ---
            if (TransientNpcPersistenceBridge.Instance != null)
            {
                TransientNpcPersistenceBridge.Instance.LoadAllTransientData(gameData.transientNpcs);
            }
            else
            {
                Debug.LogWarning("SaveLoadSystem: TransientNpcPersistenceBridge not found. Transient NPCs will not be restored.");
            }
            
            Debug.Log("SaveLoadSystem: Data binding sequence complete.");
        }

        public void ResetGameData() {
            Debug.Log("SaveLoadSystem: Resetting Game Data...");
            
            // 1. Create Fresh Data
            gameData = new GameData {
                Name = "New Game",
                CharacterName = "Player",
                CurrentLevelName = "SampleScene", 
                SaveSlotIndex = 0,
                
                // Defaults
                PlayerCleanMoney = 50f, 
                PlayerDirtyMoney = 0f,
                CurrentDay = 1,
                TimeTicks = 0, 
                TotalPlayTimeSeconds = 0,
                
                // Empty Lists
                UnlockedUpgradeIds = new List<string>(),
                worldInteractables = new List<InteractableObjectData>(),
                playerData = new PlayerData(),
                inventories = new List<InventoryData>(),
                tiNpcDataList = new List<TiNpcData>(),
            };
        }

        public void NewGame() {
            ResetGameData();
            // This direct load is fine for debug buttons, but MainMenu will use SceneLoader instead
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

        /// <summary>
        /// Creates a new save file with the "Autosave" prefix.
        /// Generates a new ID to ensure it creates a separate entry in the history.
        /// </summary>
        public void AutosaveGame()
        {
            gameData.Id = SerializableGuid.NewGuid();
            SaveGame("Autosave");
        }
        
        public void SaveGame(string saveType = "Save")
        {
            gameData.Name = $"{saveType} - {GetFormattedRealPlaytime()}";

            Debug.Log($"SaveLoadSystem: Saving game '{gameData.Name}'...");
            
            // 1. Clear generic lists
            gameData.inventories.Clear(); 
            gameData.worldInteractables.Clear();
            
            // 2. Clear NPC lists to prevent duplication
            gameData.tiNpcDataList.Clear();

            if (gameData.transientNpcs == null) gameData.transientNpcs = new List<Game.NPC.TransientNpcData>();
            gameData.transientNpcs.Clear();
            
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
                 // Use FindFirstObjectByType
                 var bridge = FindFirstObjectByType<TiNpcPersistenceBridge>();
                 if (bridge != null) gameData.tiNpcDataList = bridge.GetAllTiNpcData();
            }

            // --- Save Transient NPCs ---
            if (TransientNpcPersistenceBridge.Instance != null)
            {
                gameData.transientNpcs = TransientNpcPersistenceBridge.Instance.GetAllTransientData();
            }

            // 7. Write to Disk
            dataService.Save(gameData);
            Debug.Log("SaveLoadSystem: Save Complete.");
        }

        /// <summary>
        /// Loads the most recent save file based on modification date.
        /// </summary>
        public void QuickLoad()
        {
            // ListSaves returns saves sorted by date (newest first)
            var mostRecentSave = GetLatestSaveIdForSlot(gameData.SaveSlotIndex);

            if (!string.IsNullOrEmpty(mostRecentSave))
            {
                Debug.Log($"SaveLoadSystem: Quickloading most recent save for Slot {gameData.SaveSlotIndex}: {mostRecentSave}");
                LoadGame(mostRecentSave);
            }
            else
            {
                Debug.LogWarning($"SaveLoadSystem: QuickLoad failed. No save files found for Slot {gameData.SaveSlotIndex}.");
            }
        }

        public void LoadGame(string gameName) {
            Debug.Log($"SaveLoadSystem: Loading '{gameName}'...");
            gameData = dataService.Load(gameName);

            if (String.IsNullOrWhiteSpace(gameData.CurrentLevelName)) gameData.CurrentLevelName = "SampleScene";
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();
            
            // Ensure lists exist
            if (gameData.tiNpcDataList == null) gameData.tiNpcDataList = new List<TiNpcData>();

            // Try to find the SceneLoader (which lives in the Bootstrapper scene)
            SceneLoader loader = FindFirstObjectByType<SceneLoader>();

            if (loader != null) {
                // Use the SceneLoader to load the group associated with this level
                // This preserves the Bootstrapper and shows the loading screen
                loader.LoadSceneGroup(gameData.CurrentLevelName);
            }
            else {
                // Fallback if SceneLoader isn't found (e.g. testing in isolation)
                // NOTE: This will unload the Bootstrapper if it exists but wasn't found
                SceneManager.LoadScene(gameData.CurrentLevelName);
            }
        }

        // Reads a save file and returns the data without making it the active game.
        // Useful for getting the Display Name for UI lists.
        public GameData GetSaveDataReadOnly(string saveId)
        {
            return dataService.Load(saveId);
        }

        public string GetFormattedRealPlaytime()
        {
            TimeSpan t = TimeSpan.FromSeconds(gameData.TotalPlayTimeSeconds);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
        }

        public IEnumerable<string> GetAllSaves() 
        {
        return dataService.ListSaves();
        }

        /// <summary>
        /// Finds the ID of the most recent save file associated with a specific slot index.
        /// Returns null if no save exists for that slot.
        /// </summary>
        public string GetLatestSaveIdForSlot(int slotIndex) {
            var allSaves = GetAllSaves();
            foreach (var saveId in allSaves) {
                GameData header = GetSaveDataReadOnly(saveId);
                if (header != null && header.SaveSlotIndex == slotIndex) {
                    return saveId;
                }
            }
            return null;
        }

        public void DeleteAllSavesForSlot(int slotIndex)
        {
            var allSaves = GetAllSaves();
            List<string> savesToDelete = new List<string>();

            // 1. Identify files to delete
            foreach (var saveId in allSaves)
            {
                GameData header = GetSaveDataReadOnly(saveId);
                if (header != null && header.SaveSlotIndex == slotIndex)
                {
                    savesToDelete.Add(saveId);
                }
            }

            // 2. Delete them
            foreach (string id in savesToDelete)
            {
                Debug.Log($"SaveLoadSystem: Deleting save {id} for slot {slotIndex}");
                dataService.Delete(id);
            }
        }

        public void ReloadGame() => LoadGame(gameData.Name);
        public void DeleteGame(string gameName) => dataService.Delete(gameName);
    }
}