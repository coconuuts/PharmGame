using System;

namespace Systems.UI
{
    public interface IModalManager
    {
        void ShowConfirmationModal(string text, Action onYes, Action onCancel = null);
        void ShowInfoModal(string text, Action onOkay = null);
    }
}