using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Systems.Persistence;
using System;

namespace Systems.UI
{
    public class MainMenuProfileSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI slotNumberText;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI playTimeText;
        [SerializeField] private TextMeshProUGUI emptyText; // Text to show if empty (e.g., "New Game")
        [SerializeField] private Button button;
        [SerializeField] private Button deleteButton;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color emptyColor = Color.gray;

        private int _slotIndex;
        private Action<int> _onClick;
        private Action<int> _onDelete;

        public void Initialize(int slotIndex, GameData data, Action<int> onClick, Action<int> onDelete)
        {
            _slotIndex = slotIndex;
            _onClick = onClick;
            _onDelete = onDelete;

            // Update Slot Number Label
            if (slotNumberText != null) slotNumberText.text = $"Slot {slotIndex + 1}";

            if (data != null)
            {
                // Occupied Slot
                if (characterNameText != null) 
                {
                    characterNameText.text = data.CharacterName;
                    characterNameText.gameObject.SetActive(true);
                }
                
                if (playTimeText != null)
                {
                    TimeSpan t = TimeSpan.FromSeconds(data.TotalPlayTimeSeconds);
                    playTimeText.text = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
                    playTimeText.gameObject.SetActive(true);
                }

                if (emptyText != null) emptyText.gameObject.SetActive(false);

                if (deleteButton != null)
                {
                    deleteButton.gameObject.SetActive(true);
                    deleteButton.onClick.RemoveAllListeners();
                    deleteButton.onClick.AddListener(() => _onDelete?.Invoke(_slotIndex));
                }
            }
            else
            {
                // Empty Slot
                if (characterNameText != null) characterNameText.gameObject.SetActive(false);
                if (playTimeText != null) playTimeText.gameObject.SetActive(false);
                if (emptyText != null) 
                {
                    emptyText.text = "New Game";
                    emptyText.gameObject.SetActive(true);
                }
                if (deleteButton != null) deleteButton.gameObject.SetActive(false);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(_slotIndex));
            }
        }
    }
}