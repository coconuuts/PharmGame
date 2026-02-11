using UnityEngine;
using System;

namespace Systems.UI
{
    /// <summary>
    /// A lightweight manager for the Main Menu scene that handles Modals 
    /// without the complexity of GameStates or Interactions.
    /// </summary>
    public class SimpleModalManager : MonoBehaviour, IModalManager
    {
        public static SimpleModalManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private ModalWindowUI modalWindowController;
        [SerializeField] private GameObject modalUIRoot;
        [Tooltip("The full-screen background image that blocks clicks. Does not animate.")]
        [SerializeField] private GameObject modalBackgroundOverlay;

        // Public property to check visibility
        public bool IsModalActive => modalUIRoot != null && modalUIRoot.activeSelf;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (modalUIRoot != null) modalUIRoot.SetActive(false);
            if (modalBackgroundOverlay != null) modalBackgroundOverlay.SetActive(false);
        }

        public void ShowInfoModal(string text, Action onOkay = null)
        {
            Action wrappedAction = () => {
                onOkay?.Invoke();
                CloseModal();
            };

            var response = new ModalResponse(text, wrappedAction);
            ActivateModal(response);
        }

        public void ShowConfirmationModal(string text, Action onYes, Action onCancel = null)
        {
            Action wrappedYes = () => {
                onYes?.Invoke();
                CloseModal();
            };

            Action wrappedCancel = () => {
                onCancel?.Invoke();
                CloseModal();
            };

            var response = new ModalResponse(text, wrappedYes, wrappedCancel);
            ActivateModal(response);
        }

        private void ActivateModal(ModalResponse response)
        {
            if (modalWindowController != null && modalUIRoot != null)
            {
                // Configure content first
                modalWindowController.Configure(response);

                // Activate Overlay Immediately 
                if (modalBackgroundOverlay != null)
                {
                    modalBackgroundOverlay.SetActive(true);
                }

                // Then animate
                if (UIAnimationManager.Instance != null)
                {
                    UIAnimationManager.Instance.OpenPanel(modalUIRoot);
                }
                else
                {
                    modalUIRoot.SetActive(true);
                }
            }
        }

        private void CloseModal()
        {
            if (modalUIRoot != null)
            {
                if (UIAnimationManager.Instance != null)
                {
                    UIAnimationManager.Instance.ClosePanel(modalUIRoot);
                }
                else
                {
                    modalUIRoot.SetActive(false);
                }
                if (modalBackgroundOverlay != null)
                        {
                            modalBackgroundOverlay.SetActive(false);
                        }
            }
        }
    }
}