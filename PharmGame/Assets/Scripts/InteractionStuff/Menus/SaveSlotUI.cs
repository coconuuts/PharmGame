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

        // Internal variables to handle Button state overrides
        private ColorBlock _defaultButtonColors;
        private bool _hasCapturedColors;

        // Getter for the ID
        public string SaveId => _saveId;
        public string DisplayName => _displayName;

        private void Awake()
        {
            // Capture the original button colors so we can restore them later
            if (button != null)
            {
                _defaultButtonColors = button.colors;
                _hasCapturedColors = true;
            }
        }

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
            // 1. Apply immediate color change
            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }

            // 2. Override the Button's transition colors.
            // This prevents the button from reverting to "Normal" color (losing highlight)
            // when it loses focus (e.g., when the Modal window opens).
            if (button != null && _hasCapturedColors)
            {
                if (isSelected)
                {
                    ColorBlock selectedBlock = _defaultButtonColors;
                    // Force all states to the selected color so interaction/focus loss doesn't change it
                    selectedBlock.normalColor = selectedColor;
                    selectedBlock.highlightedColor = selectedColor;
                    selectedBlock.selectedColor = selectedColor;
                    selectedBlock.pressedColor = selectedColor;
                    button.colors = selectedBlock;
                }
                else
                {
                    // Restore original button behavior
                    button.colors = _defaultButtonColors;
                }
            }
        }
    }
}