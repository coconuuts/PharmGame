using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

namespace Systems.UI
{
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI saveNameText;
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage; 

        [Header("Visual Config")]
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.5f); 
        [SerializeField] private Color selectedColor = new Color(0.5f, 1f, 0.5f, 1f); 

        // Store both the visual name and the actual file ID
        private string _displayName;
        private string _saveId;
        private Action<string, SaveSlotUI> _onSlotClicked;

        // Getter for the ID
        public string SaveId => _saveId;
        public string DisplayName => _displayName;

        /// <summary>
        /// Sets up the slot with data and a callback.
        /// </summary>
        public void Initialize(string displayName, string saveId, Action<string, SaveSlotUI> callback)
        {
            _displayName = displayName;
            _saveId = saveId;
            _onSlotClicked = callback;

            if (saveNameText != null) saveNameText.text = _displayName;
            
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);

            SetSelected(false);
        }

        private void OnClick()
        {
            // Pass the ID back to the controller
            _onSlotClicked?.Invoke(_saveId, this);
        }

        public void SetSelected(bool isSelected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }
        }
    }
}