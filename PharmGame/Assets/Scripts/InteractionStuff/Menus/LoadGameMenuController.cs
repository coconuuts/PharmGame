using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Systems.Persistence;
using Systems.GameStates; 
using System; // Added for Action

namespace Systems.UI
{
    public class LoadGameMenuController : MonoBehaviour
    {
        [Header("UI Structure")]
        [SerializeField] private GameObject menuRootObject; 
        [SerializeField] private Transform saveListContent; 
        [SerializeField] private SaveDetailsUI saveDetailsPanel;
        
        [Header("External References")]
        [Tooltip("The menu to show when this one closes (e.g., Pause Menu or Main Menu Buttons).")]
        [SerializeField] private GameObject previousMenuPanel; 
        
        [Header("Prefabs")]
        [SerializeField] private SaveSlotUI saveSlotPrefab;
        
        [Header("Buttons")]
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button closeButton; 

        // New Event to notify listeners (like MainMenu) that we are done
        public event Action OnMenuClosed;

        private string currentSelectedSaveId;
        private List<SaveSlotUI> instantiatedSlots = new List<SaveSlotUI>();

        private void Awake()
        {
            if (loadButton != null) loadButton.interactable = false;
            if (deleteButton != null) deleteButton.interactable = false;
        }

        private void Start()
        {
            if (loadButton != null) loadButton.onClick.AddListener(OnLoadClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
            if (closeButton != null) 
                closeButton.onClick.AddListener(() => CloseMenu(true));

            UpdateButtonsState();
        }

        private void OnEnable()
        {
             UpdateButtonsState();
        }

        public void OpenMenu()
        {
            // Hide the previous menu immediately
            if (previousMenuPanel != null) previousMenuPanel.SetActive(false);
            
            // Register Escape key ONLY if MenuManager exists
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape += HandleEscapeInput;
            }

            RefreshSaveList();
            DeselectAll();

            // Animate Open
            if (UIAnimationManager.Instance != null)
            {
                UIAnimationManager.Instance.OpenPanel(menuRootObject);
            }
            else
            {
                menuRootObject.SetActive(true);
            }
        }

        public void CloseMenu(bool transitionToParent = true)
        {
            // Unregister Escape key
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape -= HandleEscapeInput;
            }

            // Animate Close
            if (UIAnimationManager.Instance != null)
            {
                UIAnimationManager.Instance.ClosePanel(menuRootObject, onComplete: () => 
                {
                    DeselectAll();
                    OnMenuClosed?.Invoke();
                });

                // Animate Parent OPEN (Simultaneously)
                if (transitionToParent && previousMenuPanel != null) 
                {
                    UIAnimationManager.Instance.OpenPanel(previousMenuPanel);
                }
            }
            else
            {
                menuRootObject.SetActive(false);
                DeselectAll();
                if (transitionToParent && previousMenuPanel != null) 
                {
                    previousMenuPanel.SetActive(true);
                }
                OnMenuClosed?.Invoke();
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

            IEnumerable<string> saveIds = SaveLoadSystem.Instance.GetAllSaves();

            // --- Filter by Slot Index ---
            int currentSlot = SaveLoadSystem.Instance.gameData.SaveSlotIndex;

            foreach (string id in saveIds)
            {
                GameData header = SaveLoadSystem.Instance.GetSaveDataReadOnly(id);
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
            currentSelectedSaveId = saveId;
            foreach (var slot in instantiatedSlots) slot.SetSelected(slot == clickedSlot);
            UpdateButtonsState();

            if (saveDetailsPanel != null && SaveLoadSystem.HasInstance)
            {
                GameData data = SaveLoadSystem.Instance.GetSaveDataReadOnly(saveId);
                Texture2D screenshot = SaveLoadSystem.Instance.GetScreenshot(saveId);
                saveDetailsPanel.SetData(data, screenshot);
            }
        }

        private void OnLoadClicked()
        {
            if (string.IsNullOrEmpty(currentSelectedSaveId)) return;
            if (!SaveLoadSystem.HasInstance) return;

            SaveLoadSystem.Instance.LoadGame(currentSelectedSaveId);

            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.ClosePauseMenu();
            }
        }

        private void OnDeleteClicked()
        {
            if (string.IsNullOrEmpty(currentSelectedSaveId)) return;
            if (!SaveLoadSystem.HasInstance) return;

            IModalManager modalManager = null;

            if (MenuManager.Instance != null) 
            {
                modalManager = MenuManager.Instance;
            }
            else if (SimpleModalManager.Instance != null)
            {
                modalManager = SimpleModalManager.Instance;
            }

            if (modalManager == null) 
            {
                Debug.LogError("LoadGameMenuController: No IModalManager found.");
                return;
            }
            
            string saveIdToDelete = currentSelectedSaveId;
            string displayName = "this save";

            var data = SaveLoadSystem.Instance.GetSaveDataReadOnly(saveIdToDelete);
            if (data != null) displayName = $"'{data.Name}'";

            modalManager.ShowConfirmationModal(
                $"Are you sure you want to delete {displayName}?",
                () => 
                {
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
            currentSelectedSaveId = null;
            foreach (var slot in instantiatedSlots) slot.SetSelected(false);
            UpdateButtonsState();

            if (saveDetailsPanel != null) saveDetailsPanel.Clear();
        }

        private void UpdateButtonsState()
        {
            bool hasSelection = !string.IsNullOrEmpty(currentSelectedSaveId);
            if (loadButton != null) loadButton.interactable = hasSelection;
            if (deleteButton != null) deleteButton.interactable = hasSelection;
        }
    }
}