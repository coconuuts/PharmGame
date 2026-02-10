using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Systems.Persistence;
using Systems.GameStates; // Needed for MenuManager
using System.Linq;
using Systems.Inventory;

namespace Systems.UI
{
    public class SaveGameMenuController : MonoBehaviour
    {
        [Header("UI Structure")]
        [SerializeField] private GameObject menuRootObject; // The Save Menu Panel
        [Tooltip("The Content object of the ScrollView. Must have FlexibleGridLayout attached.")]
        [SerializeField] private Transform saveListContent; 
        
        [Header("External References")]
        [Tooltip("Assign the main Pause Menu GameObject here so it can be hidden/shown.")]
        [SerializeField] private GameObject pauseMenuPanel; 
        
        [Header("Prefabs")]
        [SerializeField] private SaveSlotUI saveSlotPrefab;
        
        [Header("Inputs & Buttons")]
        [SerializeField] private TMP_InputField saveNameInput;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button closeButton; // The "Back" button

        // Internal State
        private string currentSelectedSaveName;
        private List<SaveSlotUI> instantiatedSlots = new List<SaveSlotUI>();

        private void Awake()
        {
            if (deleteButton != null) deleteButton.interactable = false;
        }
        
        private void Start()
        {
            // Setup Listeners
            saveButton.onClick.AddListener(OnSaveClicked);
            deleteButton.onClick.AddListener(OnDeleteClicked);
            
            if (closeButton != null) 
                closeButton.onClick.AddListener(CloseMenu);

            UpdateDeleteButtonState();
        }

        private void OnEnable()
        {
            UpdateDeleteButtonState();
        }

        private void OnDisable()
        {
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape -= HandleEscapeInput;
            }
        }

        public void OpenMenu()
        {
            UpdateDeleteButtonState();

            // 1. Swap Visibility: Show Save Menu, Hide Pause Menu
            menuRootObject.SetActive(true);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            
            // 2. Register to intercept Escape key
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape += HandleEscapeInput;
            }

            // 3. Auto-fill input with the playtime to be saved (User feedback)
            if (SaveLoadSystem.HasInstance)
            {
                // We show the time that will be used for the name
                if (saveNameInput != null) 
                {
                    saveNameInput.text = SaveLoadSystem.Instance.GetFormattedRealPlaytime();
                    saveNameInput.readOnly = true;
                }
            }

            // 4. Populate
            RefreshSaveList();
            DeselectAll();
        }

        public void CloseMenu()
        {
            // 1. Swap Visibility: Hide Save Menu, Show Pause Menu
            menuRootObject.SetActive(false);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);

            // 2. Unregister Escape key interception
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape -= HandleEscapeInput;
            }

            DeselectAll();
        }

        private bool HandleEscapeInput()
        {
            CloseMenu();
            return true;
        }

        private void RefreshSaveList()
        {
            foreach (var slot in instantiatedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            instantiatedSlots.Clear();

            if (!SaveLoadSystem.HasInstance) return;

            // GetAllSaves now returns File IDs (filenames)
            IEnumerable<string> saveIds = SaveLoadSystem.Instance.GetAllSaves();

            // --- Filter by Slot Index ---
            int currentSlot = SaveLoadSystem.Instance.gameData.SaveSlotIndex;

            foreach (string id in saveIds)
            {
                GameData header = SaveLoadSystem.Instance.GetSaveDataReadOnly(id);
                // Only show saves that belong to the current slot
                // NOTE: Old saves (index 0) might appear in Slot 1 (index 0).
                if (header != null && header.SaveSlotIndex == currentSlot)
                {
                    SaveSlotUI newSlot = Instantiate(saveSlotPrefab, saveListContent);
                    newSlot.Initialize(header.Name, id, OnSlotClicked);
                    instantiatedSlots.Add(newSlot);
                }
            }

            if (saveListContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(saveListContent.GetComponent<RectTransform>());
            }
        }

        private void OnSlotClicked(string saveName, SaveSlotUI clickedSlot)
        {
            currentSelectedSaveName = saveName;
            
            foreach (var slot in instantiatedSlots)
            {
                slot.SetSelected(slot == clickedSlot);
            }

            if (saveNameInput != null && SaveLoadSystem.HasInstance) 
            {
                saveNameInput.text = SaveLoadSystem.Instance.GetFormattedRealPlaytime();
            }
            UpdateDeleteButtonState();
        }

        private void OnSaveClicked()
        {
            if (!SaveLoadSystem.HasInstance) return;

            // 1. Generate a unique ID (Always new file for now)
            SaveLoadSystem.Instance.gameData.Id = SerializableGuid.NewGuid();

            // 2. Save using the "Save" prefix to get "Save - HH:mm"
            SaveLoadSystem.Instance.SaveGame("Save");

            CloseMenu(); 
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.ClosePauseMenu(); 
            }
        }
        private void OnDeleteClicked()
        {
            if (string.IsNullOrEmpty(currentSelectedSaveName)) return;
            if (!SaveLoadSystem.HasInstance) return;
            if (MenuManager.Instance == null) return;

            string saveIdToDelete = currentSelectedSaveName;
            string displayName = "this save";

            // Try to get a friendly name for the modal
            var data = SaveLoadSystem.Instance.GetSaveDataReadOnly(saveIdToDelete);
            if (data != null) displayName = $"'{data.Name}'";

            // Open Confirmation Modal
            MenuManager.Instance.ShowConfirmationModal(
                $"Are you sure you want to delete {displayName}?",
                () => 
                {
                    // YES Callback: Perform the delete
                    if (SaveLoadSystem.HasInstance)
                    {
                        SaveLoadSystem.Instance.DeleteGame(saveIdToDelete);
                        RefreshSaveList();
                        DeselectAll();
                    }
                },
                () => 
                {
                    DeselectAll();
                }
            );
        }

        private void DeselectAll()
        {
            currentSelectedSaveName = null;
            if (saveNameInput != null && SaveLoadSystem.HasInstance) 
            {
                 saveNameInput.text = SaveLoadSystem.Instance.GetFormattedRealPlaytime();
            }
            foreach (var slot in instantiatedSlots) slot.SetSelected(false);
            UpdateDeleteButtonState();
        }

        private void UpdateDeleteButtonState()
        {
            deleteButton.interactable = !string.IsNullOrEmpty(currentSelectedSaveName);
        }
    }
}