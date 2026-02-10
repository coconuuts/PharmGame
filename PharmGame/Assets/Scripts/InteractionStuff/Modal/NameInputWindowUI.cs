using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Systems.UI
{
    public class NameInputWindowUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI errorLabel; 

        private Action<string> onConfirmCallback;
        private Action onCancelCallback;
        private bool isInitialized = false;

        // Public property to check if this window is currently open
        public bool IsActive => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!IsActive) return;

            // Check for Escape key to Cancel
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancelClicked();
            }

            // Check for Enter key to Confirm
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Only allow confirmation if the button is enabled (input is valid)
                if (confirmButton != null && confirmButton.interactable)
                {
                    OnConfirmClicked();
                }
            }
        }

        private void Initialize()
        {
            if (isInitialized) return;

            if (panelRoot == null) panelRoot = gameObject; 
            
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (nameInputField != null)
            {
                nameInputField.onValueChanged.RemoveAllListeners();
                nameInputField.onValueChanged.AddListener(OnInputValueChanged);
            }

            isInitialized = true;
        }

        public void Show(Action<string> onConfirm, Action onCancel)
        {
            Initialize();

            onConfirmCallback = onConfirm;
            onCancelCallback = onCancel;

            if (nameInputField != null) nameInputField.text = "";
            if (errorLabel != null) errorLabel.text = "";
            if (confirmButton != null) confirmButton.interactable = false; 

            if (panelRoot != null) panelRoot.SetActive(true);
            
            if (nameInputField != null)
            {
                nameInputField.Select(); 
                nameInputField.ActivateInputField();
            }
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnInputValueChanged(string newValue)
        {
            if (confirmButton != null)
            {
                bool isValid = !string.IsNullOrWhiteSpace(newValue);
                confirmButton.interactable = isValid;
            }
        }

        private void OnConfirmClicked()
        {
            if (nameInputField == null) return;

            string finalName = nameInputField.text.Trim();
            
            if (!string.IsNullOrEmpty(finalName))
            {
                onConfirmCallback?.Invoke(finalName);
                Hide();
            }
        }

        private void OnCancelClicked()
        {
            onCancelCallback?.Invoke();
            Hide();
        }
    }
}