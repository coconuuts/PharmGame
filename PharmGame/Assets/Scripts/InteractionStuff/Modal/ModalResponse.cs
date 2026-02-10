using System;
using Systems.Interaction;

namespace Systems.UI
{
    public class ModalResponse : InteractionResponse
    {
        public string BodyText { get; }
        public bool IsConfirmation { get; } // True = Yes/Cancel, False = Okay only
        public Action OnConfirm { get; }
        public Action OnCancel { get; }

        public ModalResponse(string bodyText, Action onConfirm = null)
        {
            BodyText = bodyText;
            IsConfirmation = false;
            OnConfirm = onConfirm;
            OnCancel = null;
        }

        public ModalResponse(string bodyText, Action onYes, Action onCancel)
        {
            BodyText = bodyText;
            IsConfirmation = true;
            OnConfirm = onYes;
            OnCancel = onCancel;
        }
    }
}