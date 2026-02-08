using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Systems.Persistence;
using Systems.GameStates; 

namespace Systems.UI
{
    public class LoadGameMenuController : MonoBehaviour
    {
        [Header("UI Structure")]
        [SerializeField] private GameObject menuRootObject; 
        [SerializeField] private Transform saveListContent; 
        
        [Header("External References")]
        [Tooltip("The menu to show when this one closes (e.g., Pause Menu or Main Menu Buttons).")]
        [SerializeField] private GameObject previousMenuPanel; 
        
        [Header("Prefabs")]
        [SerializeField] private SaveSlotUI saveSlotPrefab;
        
        [Header("Buttons")]
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button closeButton; 

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
            if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);

            UpdateButtonsState();
        }

        private void OnEnable()
        {
             UpdateButtonsState();
        }

        public void OpenMenu()
        {
            if (menuRootObject != null) menuRootObject.SetActive(true);
            
            // Hide the previous menu (Pause Menu or Main Menu Buttons)
            if (previousMenuPanel != null) previousMenuPanel.SetActive(false);
            
            // Register Escape key ONLY if MenuManager exists (Gameplay Scene)
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape += HandleEscapeInput;
            }

            RefreshSaveList();
            DeselectAll();
        }

        public void CloseMenu()
        {
            if (menuRootObject != null) menuRootObject.SetActive(false);
            
            // Show the previous menu again
            if (previousMenuPanel != null) previousMenuPanel.SetActive(true);

            // Unregister Escape key if we used it
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

            IEnumerable<string> saveIds = SaveLoadSystem.Instance.GetAllSaves();

            foreach (string id in saveIds)
            {
                GameData header = SaveLoadSystem.Instance.GetSaveDataReadOnly(id);
                if (header != null)
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
        }

        private void OnLoadClicked()
        {
            if (string.IsNullOrEmpty(currentSelectedSaveId)) return;
            if (!SaveLoadSystem.HasInstance) return;

            // Loading initiates a scene change, so we don't strictly need to manage UI states after this
            SaveLoadSystem.Instance.LoadGame(currentSelectedSaveId);

            // Cleanup if we are in the gameplay scene
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.ClosePauseMenu();
            }
        }

        private void OnDeleteClicked()
        {
            if (string.IsNullOrEmpty(currentSelectedSaveId)) return;
            if (!SaveLoadSystem.HasInstance) return;

            SaveLoadSystem.Instance.DeleteGame(currentSelectedSaveId);
            RefreshSaveList();
            DeselectAll();
        }

        private void DeselectAll()
        {
            currentSelectedSaveId = null;
            foreach (var slot in instantiatedSlots) slot.SetSelected(false);
            UpdateButtonsState();
        }

        private void UpdateButtonsState()
        {
            bool hasSelection = !string.IsNullOrEmpty(currentSelectedSaveId);
            if (loadButton != null) loadButton.interactable = hasSelection;
            if (deleteButton != null) deleteButton.interactable = hasSelection;
        }
    }
}