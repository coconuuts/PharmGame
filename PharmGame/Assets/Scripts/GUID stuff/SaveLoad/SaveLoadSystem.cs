using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Systems.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using Systems.SaveLoad;
using Game.NPC.TI; 
using Game.NPC;    
using Systems.Economy;
using Systems.SceneManagement;
using Systems.UI;
using Systems.Player;

namespace Systems.Persistence {
    [Serializable] public class GameData : ISaveable
    { 
        public SerializableGuid Id { get; set; } = SerializableGuid.Empty;
        public string Name;
        public string CharacterName;
        public string CurrentLevelName;
        public string LastSaveDate;
        public int SaveSlotIndex = 0;
        public PlayerData playerData;
        public List<InventoryData> inventories;
        public List<TiNpcData> tiNpcDataList;
        public List<Game.NPC.TransientNpcData> transientNpcs;
        public List<InteractableObjectData> worldInteractables;
        public PrescriptionManagerData prescriptionSystemData;
        public PlayerPrescriptionData playerPrescriptionData;

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
            LastSaveDate = DateTime.Now.ToString();
            worldInteractables = new List<InteractableObjectData>();
            prescriptionSystemData = new PrescriptionManagerData();
            playerPrescriptionData = new PlayerPrescriptionData();
            
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
        private bool isNewGameTransition = false;

        IDataService dataService;
        bool isGameplayActive = false;
        private Texture2D currentScreenshot;

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
            if (gameData == null) gameData = new GameData();

            // Check if we started in a gameplay scene
            string currentScene = SceneManager.GetActiveScene().name;
            isGameplayActive = (currentScene != "MainMenu" && currentScene != "Bootstrapper");
            
            // Subscribe to SceneLoader events if available
            if (SceneLoader.Instance != null) {
                SceneLoader.Instance.manager.OnSceneGroupLoaded += OnSceneGroupLoaded;
            }
        }

        void Update() {
            // Track real-time played while in gameplay scenes (not Menu)
            if (isGameplayActive && gameData != null) {
                gameData.TotalPlayTimeSeconds += Time.unscaledDeltaTime;
            }
        }

        void OnEnable() {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (SceneLoader.Instance != null) {
                SceneLoader.Instance.manager.OnSceneGroupLoaded -= OnSceneGroupLoaded;
            }
        }

        // Called when the entire group (Environment + Gameplay) is finished loading
        void OnSceneGroupLoaded()
        {
            Debug.Log("SaveLoadSystem: Scene Group Loaded. Restoring Game State...");
            RestoreGameState();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isGameplayActive = (scene.name != "MainMenu" && scene.name != "Bootstrapper");

            if (scene.name == "MainMenu") return;

            // If SceneLoader is present, we defer binding until OnSceneGroupLoaded fires.
            // This prevents binding the Player position before the Environment (Floor) is loaded,
            // which causes the player to fall and reset to spawn.
            if (SceneLoader.Instance != null) return;

            // Fallback for direct SceneManager usage (development/testing single scenes)
            Debug.Log($"SaveLoadSystem: Scene '{scene.name}' loaded (Single). Starting Data Binding Sequence...");
            RestoreGameState();
        }

        void RestoreGameState()
        {
            Debug.Log("SaveLoadSystem: Restoring Game State...");

            bool wasNewGame = isNewGameTransition;

            // --- SPAWN POINT LOGIC START ---
            if (isNewGameTransition)
            {
                PlayerSpawnPoint spawnPoint = FindFirstObjectByType<PlayerSpawnPoint>();
                
                if (spawnPoint != null)
                {
                    Debug.Log($"SaveLoadSystem: Found Spawn Point at {spawnPoint.transform.position}. Moving Player Data.");
                    gameData.playerData.position = spawnPoint.transform.position;
                    gameData.playerData.rotation = spawnPoint.transform.rotation;
                }
                else
                {
                    Debug.LogWarning("SaveLoadSystem: New Game started, but no PlayerSpawnPoint found in the scene! Using default/scene position.");
                }

                isNewGameTransition = false; // Reset the flag so future scene loads don't teleport the player
            }

            // SYSTEM LEVEL BINDINGS 
            // Time must be first to set lighting/skybox before the screen fades in
            Bind<TimeManager, GameData>(gameData);
            
            // Economy updates the UI and Wallet SO immediately
            Bind<EconomyManager, GameData>(gameData);
            
            // Upgrades unlock recipes/shelves before we spawn physical objects
            Bind<UpgradeManager, GameData>(gameData);

            // WORLD STATE BINDINGS
            Bind<PlayerEntity, PlayerData>(gameData.playerData);

            Bind<Game.Prescriptions.PrescriptionManager, PrescriptionManagerData>(gameData.prescriptionSystemData);
            Bind<PlayerPrescriptionTracker, PlayerPrescriptionData>(gameData.playerPrescriptionData);

            // Bind Generic World Interactables (Light Switches, Cash Register States)
            if (gameData.worldInteractables == null) gameData.worldInteractables = new List<InteractableObjectData>();
            
            var allSavables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                              .OfType<ISavableComponent>();

            foreach (var component in allSavables)
            {
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
                    };
                    gameData.inventories.Add(invData);
                }
                invComponent.Bind(invData);
            }

            // --- Restore TINPCs ---
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
                // Warning suppressed as this is expected in some scenes
                // Debug.LogWarning("SaveLoadSystem: TransientNpcPersistenceBridge not found. Transient NPCs will not be restored.");
            }
            
            Debug.Log("SaveLoadSystem: Data binding sequence complete.");

            if (wasNewGame)
            {
                Debug.Log("SaveLoadSystem: Triggering initial New Game autosave.");
                AutosaveGame();
            }
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

        public void NewGame(bool autoLoadScene = true) {
            ResetGameData();
            isNewGameTransition = true; 
            
            if (autoLoadScene)
            {
                // This direct load is fine for debug buttons
                SceneManager.LoadScene(gameData.CurrentLevelName);
            }
        }
        
        void Bind<T, TData>(TData data) where T : MonoBehaviour, IBind<TData> where TData : ISaveable, new() {
            var entity = FindFirstObjectByType<T>();
            if (entity != null) {
                if (data == null) data = new TData { Id = entity.Id };
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

        public void AutosaveGame()
        {
            gameData.Id = SerializableGuid.NewGuid();
            SaveGame("Autosave");
        }
        
        public void SaveGame(string saveType = "Save")
        {
            StartCoroutine(SaveGameRoutine(saveType));
        }

        private IEnumerator SaveGameRoutine(string saveType)
        {
            gameData.Name = $"{saveType} - {GetFormattedRealPlaytime()}";
            gameData.LastSaveDate = DateTime.Now.ToString("g");

            yield return new WaitForEndOfFrame();
            
            currentScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
            
            Debug.Log($"SaveLoadSystem: Saving game '{gameData.Name}'...");
            
            gameData.inventories.Clear(); 
            gameData.worldInteractables.Clear();
            gameData.tiNpcDataList.Clear();
            if (gameData.transientNpcs == null) gameData.transientNpcs = new List<Game.NPC.TransientNpcData>();
            gameData.transientNpcs.Clear();

            var allSceneMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var savableComponents = allSceneMonoBehaviours.OfType<ISavableComponent>().ToList();

            foreach (var component in savableComponents) {
                ISaveable data = component.CreateSaveData();
                if (data != null) {
                    if (data is InventoryData invData) gameData.inventories.Add(invData);
                    else if (data is InteractableObjectData interactableData) gameData.worldInteractables.Add(interactableData);
                    else if (data is PrescriptionManagerData pmData) gameData.prescriptionSystemData = pmData;
                    else if (data is PlayerPrescriptionData ppData) gameData.playerPrescriptionData = ppData;
                }
            }
            
            Bind<TimeManager, GameData>(gameData);
            Bind<EconomyManager, GameData>(gameData);
            Bind<UpgradeManager, GameData>(gameData);
            Bind<PlayerEntity, PlayerData>(gameData.playerData); 
            Bind<Game.Prescriptions.PrescriptionManager, PrescriptionManagerData>(gameData.prescriptionSystemData);

            if (TiNpcPersistenceBridge.Instance != null) gameData.tiNpcDataList = TiNpcPersistenceBridge.Instance.GetAllTiNpcData();
            else {
                 var bridge = FindFirstObjectByType<TiNpcPersistenceBridge>();
                 if (bridge != null) gameData.tiNpcDataList = bridge.GetAllTiNpcData();
            }

            if (TransientNpcPersistenceBridge.Instance != null) gameData.transientNpcs = TransientNpcPersistenceBridge.Instance.GetAllTransientData();

            dataService.Save(gameData);

            if (currentScreenshot != null)
            {
                dataService.SaveScreenshot(gameData.Id.ToHexString(), currentScreenshot);
                Destroy(currentScreenshot); 
            }

            Debug.Log("SaveLoadSystem: Save Complete.");
        }

        public void QuickLoad()
        {
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
            isNewGameTransition = false;

            if (String.IsNullOrWhiteSpace(gameData.CurrentLevelName)) gameData.CurrentLevelName = "SampleScene";
            if (gameData.inventories == null) gameData.inventories = new List<InventoryData>();
            
            if (gameData.tiNpcDataList == null) gameData.tiNpcDataList = new List<TiNpcData>();

            SceneLoader loader = FindFirstObjectByType<SceneLoader>();

            if (loader != null) {
                loader.LoadSceneGroup(gameData.CurrentLevelName);
            }
            else {
                SceneManager.LoadScene(gameData.CurrentLevelName);
            }
        }

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

            foreach (var saveId in allSaves)
            {
                GameData header = GetSaveDataReadOnly(saveId);
                if (header != null && header.SaveSlotIndex == slotIndex)
                {
                    savesToDelete.Add(saveId);
                }
            }

            foreach (string id in savesToDelete)
            {
                Debug.Log($"SaveLoadSystem: Deleting save {id} for slot {slotIndex}");
                dataService.Delete(id);
            }
        }

        public Texture2D GetScreenshot(string saveId)
        {
            return dataService.LoadScreenshot(saveId);
        }

        public void ReloadGame() => LoadGame(gameData.Name);
        public void DeleteGame(string gameName) => dataService.Delete(gameName);
    }
}