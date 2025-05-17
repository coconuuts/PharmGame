using UnityEngine;
using Systems.Interaction; // Needed for IInteractable
using Systems.GameStates; // Needed for MenuManager
// Add any other namespaces for interactable types that might need resetting


namespace Systems.GameStates // Place in the same namespace as your other StateActions
{
    /// <summary>
    /// State action to call the ResetInteraction method on the currently
    /// active ComputerInteractable or CashRegisterInteractable stored in MenuManager.
    /// Assumes the interactable component implements a ResetInteraction() method.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Call Interactable Reset")]
    public class CallInteractableResetActionSO : StateAction
    {
        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            if (manager == null)
            {
                Debug.LogWarning("CallInteractableResetActionSO: MenuManager reference is null.", this);
                return;
            }

            // Try to get the currently active interactable from MenuManager's getters
            IInteractable interactableToReset = null;

            // Check for ComputerInteractable
            if (manager.CurrentComputerInteractable != null)
            {
                interactableToReset = manager.CurrentComputerInteractable;
                Debug.Log($"CallInteractableResetActionSO: Found active ComputerInteractable to reset: {((MonoBehaviour)interactableToReset).gameObject.name}", ((MonoBehaviour)interactableToReset).gameObject);
            }
            // Check for CashRegisterInteractable (Minigame)
            else if (manager.CurrentCashRegisterInteractable != null)
            {
                 interactableToReset = manager.CurrentCashRegisterInteractable;
                 Debug.Log($"CallInteractableResetActionSO: Found active CashRegisterInteractable to reset: {((MonoBehaviour)interactableToReset).gameObject.name}", ((MonoBehaviour)interactableToReset).gameObject);
            }
            // Add checks for other interactable types if they have a ResetInteraction method
            // else if (manager.CurrentSomeOtherInteractable != null) { ... }


            if (interactableToReset != null)
            {
                // Attempt to call ResetInteraction() using dynamic or casting
                // Using casting is safer and more explicit than dynamic
                if (interactableToReset is ComputerInteractable computer)
                {
                    computer.ResetInteraction();
                }
                else if (interactableToReset is CashRegisterInteractable cashRegister)
                {
                     cashRegister.ResetInteraction(); // Assumes CashRegisterInteractable also has ResetInteraction()
                }
                // Add casting for other interactable types if needed

                // If you had a common base class or interface with ResetInteraction(), you could cast to that:
                // if (interactableToReset is IResettable res) { res.ResetInteraction(); } // Assuming IResettable interface

                 Debug.Log($"CallInteractableResetActionSO: Called ResetInteraction on {interactableToReset.GetType().Name}.", ((MonoBehaviour)interactableToReset).gameObject);

            }
            else
            {
                 // This warning is expected if exiting states that don't involve these specific interactables
                 Debug.Log("CallInteractableResetActionSO: No active ComputerInteractable or CashRegisterInteractable found via MenuManager to reset.");
            }
        }
    }
}