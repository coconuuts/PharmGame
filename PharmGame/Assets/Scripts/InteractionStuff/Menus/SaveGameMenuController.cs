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

        private void Start()
        {
            // Setup Listeners
            saveButton.onClick.AddListener(OnSaveClicked);
            deleteButton.onClick.AddListener(OnDeleteClicked);
            
            if (closeButton != null) 
                closeButton.onClick.AddListener(CloseMenu);
        }

        public void OpenMenu()
        {
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
                    saveNameInput.interactable = false; 
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

            foreach (string id in saveIds)
            {
                // Peek at the file
                GameData header = SaveLoadSystem.Instance.GetSaveDataReadOnly(id);
                if (header != null)
                {
                    SaveSlotUI newSlot = Instantiate(saveSlotPrefab, saveListContent);
                    // Pass Display Name (header.Name) AND File ID (id)
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

            // We enforce the name to be the Total Playtime, ignoring user input for now.
            // SaveLoadSystem.SaveGame() handles setting the name to GetFormattedRealPlaytime()
            // but we can also set it here for clarity or if we want to change logic later.
            string nameToSave = SaveLoadSystem.Instance.GetFormattedRealPlaytime();

            // 1. Set the Display Name
            SaveLoadSystem.Instance.gameData.Name = nameToSave;
            
            // 2. Generate a unique ID (Always new file for now, or handle overwrite logic if desired)
            // If you want to overwrite the selected slot, you would reuse the ID.
            // For now, let's treat every save as a new file (standard for this type of system unless explicit overwrite UI exists)
            SaveLoadSystem.Instance.gameData.Id = SerializableGuid.NewGuid();

            // 3. Save
            SaveLoadSystem.Instance.SaveGame();

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

            SaveLoadSystem.Instance.DeleteGame(currentSelectedSaveName);
            RefreshSaveList();
            DeselectAll();
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