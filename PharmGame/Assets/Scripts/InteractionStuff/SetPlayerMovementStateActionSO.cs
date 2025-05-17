using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse
// Assuming PlayerMovement is in Systems.Player namespace
using Systems.Player; // Adjust this namespace if PlayerMovement is elsewhere

namespace Systems.GameStates
{
    /// <summary>
    /// State action to enable or disable player movement.
    /// Assumes the PlayerMovement script has a public method like SetMovementEnabled(bool).
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Set Player Movement State")]
    public class SetPlayerMovementStateActionSO : StateAction
    {
        [Tooltip("Should player movement be enabled in this state?")]
        public bool enableMovement = true;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            // Access the PlayerMovement component via the MenuManager reference
            if (manager != null && manager.player != null)
            {
                PlayerMovement playerMovement = manager.player.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    // Assuming PlayerMovement has a method to enable/disable movement
                    playerMovement.SetMovementEnabled(enableMovement); // You might need to add this method to your PlayerMovement script
                    // Debug.Log($"SetPlayerMovementStateAction: Player movement set to {enableMovement}"); // Optional debug
                }
                else
                {
                    Debug.LogWarning($"SetPlayerMovementStateActionSO: Player GameObject does not have a PlayerMovement component.", manager.player);
                }
            }
            else
            {
                Debug.LogWarning($"SetPlayerMovementStateActionSO: MenuManager or player reference is null.");
            }
        }
    }
}