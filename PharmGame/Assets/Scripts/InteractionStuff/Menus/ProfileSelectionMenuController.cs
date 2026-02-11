using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Systems.Persistence;
using System;

namespace Systems.UI
{
    public class ProfileSelectionMenuController : MonoBehaviour
    {
        [Header("UI Structure")]
        [SerializeField] private GameObject menuRootObject;
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private Button backButton;

        [Header("Prefab")]
        [SerializeField] private MainMenuProfileSlot slotPrefab;

        [Header("Configuration")]
        [SerializeField] private int numberOfSlots = 5;

        // Event for MainMenu to listen to
        // Parameters: slotIndex, isNewGame, latestSaveId (if not new)
        public event Action<int, bool, string> OnProfileSelected;
        public event Action OnBackClicked;

        private List<MainMenuProfileSlot> instantiatedSlots = new List<MainMenuProfileSlot>();

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
        }

        public void OpenMenu()
        {
            UIAnimationManager.Instance.OpenPanel(menuRootObject);
            RefreshSlots();
        }

        public void CloseMenu()
        {
            UIAnimationManager.Instance.ClosePanel(menuRootObject);
        }

        private void RefreshSlots()
        {
            // 1. Clear existing slots
            foreach (var slot in instantiatedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            instantiatedSlots.Clear();

            if (!SaveLoadSystem.HasInstance) return;

            // 2. Create new slots
            for (int i = 0; i < numberOfSlots; i++)
            {
                MainMenuProfileSlot newSlot = Instantiate(slotPrefab, slotsContainer);
                
                string latestSaveId = SaveLoadSystem.Instance.GetLatestSaveIdForSlot(i);
                GameData header = null;
                
                if (!string.IsNullOrEmpty(latestSaveId))
                {
                    header = SaveLoadSystem.Instance.GetSaveDataReadOnly(latestSaveId);
                }

                newSlot.Initialize(i, header, 
                    (index) => OnSlotClicked(index, latestSaveId), 
                    OnSlotDeleteClicked 
                );

                instantiatedSlots.Add(newSlot); 
            }
        }

        private void OnSlotClicked(int slotIndex, string latestSaveId)
        {
            bool isNewGame = string.IsNullOrEmpty(latestSaveId);
            OnProfileSelected?.Invoke(slotIndex, isNewGame, latestSaveId);
        }

        private void OnSlotDeleteClicked(int slotIndex)
        {
            // 1. Get the SimpleModalManager (Assuming we are in MainMenu scene)
            if (SimpleModalManager.Instance == null)
            {
                Debug.LogError("ProfileSelectionMenu: SimpleModalManager not found!");
                return;
            }

            // 2. Get data for friendly display name
            string latestSaveId = SaveLoadSystem.Instance.GetLatestSaveIdForSlot(slotIndex);
            string characterName = "this character";
            
            if (!string.IsNullOrEmpty(latestSaveId))
            {
                var data = SaveLoadSystem.Instance.GetSaveDataReadOnly(latestSaveId);
                if (data != null) characterName = data.CharacterName;
            }

            // 3. Show Confirmation Modal
            SimpleModalManager.Instance.ShowConfirmationModal(
                $"Are you sure you want to delete all saves for {characterName}? This cannot be undone.",
                () => // On Yes
                {
                    if (SaveLoadSystem.HasInstance)
                    {
                        // Perform the batch delete
                        SaveLoadSystem.Instance.DeleteAllSavesForSlot(slotIndex);
                        // Refresh the UI to show the slot as empty
                        RefreshSlots();
                    }
                },
                null
            );
        }
    }
}