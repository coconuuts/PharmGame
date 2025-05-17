using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse

namespace Systems.GameStates
{
    /// <summary>
    /// State action to set the cursor lock mode and visibility.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Set Cursor State")]
    public class SetCursorStateActionSO : StateAction
    {
        [Tooltip("The desired cursor lock mode.")]
        public CursorLockMode targetLockMode = CursorLockMode.None;

        [Tooltip("The desired cursor visibility.")]
        public bool targetVisible = true;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            Cursor.lockState = targetLockMode;
            Cursor.visible = targetVisible;
            // Debug.Log($"SetCursorStateAction: Set lock mode to {targetLockMode} and visible to {targetVisible}"); // Optional debug
        }
    }
}