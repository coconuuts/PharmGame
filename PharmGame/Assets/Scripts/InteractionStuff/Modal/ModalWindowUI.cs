using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Systems.GameStates; // Added to access MenuManager

namespace Systems.UI
{
    public class ModalWindowUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI bodyTextLabel;
        [SerializeField] private GameObject buttonContainer;
        
        [Header("Buttons")]
        [SerializeField] private Button okayButton;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button cancelButton;

        private Action onConfirmCallback;
        private Action onCancelCallback;
        
        // Track whether this is a confirmation (Yes/Cancel) or Info (Okay) modal
        private bool isConfirmation; 

        private void Awake()
        {
            // Register listeners
            if (okayButton) okayButton.onClick.AddListener(OnConfirmClicked);
            if (yesButton) yesButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton) cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnEnable()
        {
            // Register to MenuManager's Escape handler if available (Gameplay Scene)
            // This ensures the Modal handles Escape before the MenuManager tries to open the Pause Menu
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape += HandleMenuManagerEscape;
            }
        }

        private void OnDisable()
        {
            // Unregister to prevent errors
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape -= HandleMenuManagerEscape;
            }
        }

        private void Update()
        {
            // Handle Enter key (Always acts as the positive/okay action)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnConfirmClicked();
            }

            // Handle Escape key
            // If MenuManager exists, it handles the input via the HandleMenuManagerEscape callback above.
            // We only check manually here if MenuManager is null (e.g., in the Main Menu scene).
            if (MenuManager.Instance == null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    PerformCancelAction();
                }
            }
        }

        /// <summary>
        /// Handler called by MenuManager when Escape is pressed.
        /// Returns true to indicate the input was consumed.
        /// </summary>
        private bool HandleMenuManagerEscape()
        {
            PerformCancelAction();
            return true;
        }

        /// <summary>
        /// Executes the appropriate action for "Cancel/Back" based on the modal type.
        /// </summary>
        private void PerformCancelAction()
        {
            if (isConfirmation)
            {
                // For Confirmation: Escape triggers "Cancel" (No)
                OnCancelClicked();
            }
            else
            {
                // For Info: Escape triggers "Okay" (Close)
                OnConfirmClicked();
            }
        }

        public void Configure(ModalResponse data)
        {
            if (data == null) return;

            // Cache the mode so we know how to handle inputs
            isConfirmation = data.IsConfirmation;

            // 1. Set Text
            if (bodyTextLabel != null) bodyTextLabel.text = data.BodyText;

            // 2. Set Callbacks
            onConfirmCallback = data.OnConfirm;
            onCancelCallback = data.OnCancel;

            // 3. Toggle Buttons based on mode
            if (data.IsConfirmation)
            {
                // Confirmation Mode: Show Yes/Cancel, Hide Okay
                if (okayButton) okayButton.gameObject.SetActive(false);
                if (yesButton) yesButton.gameObject.SetActive(true);
                if (cancelButton) cancelButton.gameObject.SetActive(true);
            }
            else
            {
                // Info Mode: Show Okay, Hide Yes/Cancel
                if (okayButton) okayButton.gameObject.SetActive(true);
                if (yesButton) yesButton.gameObject.SetActive(false);
                if (cancelButton) cancelButton.gameObject.SetActive(false);
            }
        }

        private void OnConfirmClicked()
        {
            onConfirmCallback?.Invoke();
        }

        private void OnCancelClicked()
        {
            onCancelCallback?.Invoke();
        }
    }
}