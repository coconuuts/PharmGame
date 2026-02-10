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

        // NEW: Public property to check visibility
        public bool IsModalActive => modalUIRoot != null && modalUIRoot.activeSelf;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (modalUIRoot != null) modalUIRoot.SetActive(false);
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
                modalWindowController.Configure(response);
                modalUIRoot.SetActive(true);
            }
        }

        private void CloseModal()
        {
            if (modalUIRoot != null) modalUIRoot.SetActive(false);
        }
    }
}