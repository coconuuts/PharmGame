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
        [SerializeField] private SaveDetailsUI saveDetailsPanel;

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
                closeButton.onClick.AddListener(() => CloseMenu(true));

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

            // 1. Swap Visibility: Hide Pause Menu Buttons immediately
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            
            // 2. Register to intercept Escape key
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape += HandleEscapeInput;
            }

            // 3. Auto-fill input with the playtime to be saved (User feedback)
            if (SaveLoadSystem.HasInstance)
            {
                if (saveNameInput != null) 
                {
                    saveNameInput.text = SaveLoadSystem.Instance.GetFormattedRealPlaytime();
                    saveNameInput.readOnly = true;
                }
            }

            // 4. Populate
            RefreshSaveList();
            DeselectAll();

            // 5. Animate Open
            if (UIAnimationManager.Instance != null)
            {
                UIAnimationManager.Instance.OpenPanel(menuRootObject);
            }
            else
            {
                menuRootObject.SetActive(true);
            }
        }

        /// <summary>
        /// Closes the Save Menu.
        /// </summary>
        /// <param name="transitionToParent">If true, shows the PauseMenuPanel after closing. If false, just closes (used when resuming game).</param>
        public void CloseMenu(bool transitionToParent = true)
        {
            // 1. Unregister Escape key interception immediately
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape -= HandleEscapeInput;
            }

            // 2. Animate Close
            if (UIAnimationManager.Instance != null)
            {
                UIAnimationManager.Instance.ClosePanel(menuRootObject, onComplete: () => 
                {
                    DeselectAll();
                });

                // 3. Animate Parent OPEN (Simultaneously)
                if (transitionToParent && pauseMenuPanel != null) 
                {
                    UIAnimationManager.Instance.OpenPanel(pauseMenuPanel);
                }
            }
            else
            {
                // Fallback if no animation manager
                menuRootObject.SetActive(false);
                DeselectAll();
                if (transitionToParent && pauseMenuPanel != null) 
                {
                    pauseMenuPanel.SetActive(true);
                }
            }
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

        private void OnSlotClicked(string saveId, SaveSlotUI clickedSlot)
        {
            currentSelectedSaveName = saveId;
            
            foreach (var slot in instantiatedSlots)
            {
                slot.SetSelected(slot == clickedSlot);
            }

            if (saveDetailsPanel != null && SaveLoadSystem.HasInstance)
            {
                // Load header data
                GameData data = SaveLoadSystem.Instance.GetSaveDataReadOnly(saveId);
                // Load screenshot
                Texture2D screenshot = SaveLoadSystem.Instance.GetScreenshot(saveId);
                
                saveDetailsPanel.SetData(data, screenshot);
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

            if (saveDetailsPanel != null) saveDetailsPanel.Clear();
        }

        private void UpdateDeleteButtonState()
        {
            deleteButton.interactable = !string.IsNullOrEmpty(currentSelectedSaveName);
        }
    }
}