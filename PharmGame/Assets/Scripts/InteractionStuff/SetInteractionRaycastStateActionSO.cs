using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse
// Assuming PlayerInteractionManager is in Systems.Player namespace
using Systems.Player; // Adjust this namespace if PlayerInteractionManager is elsewhere

namespace Systems.GameStates
{
    /// <summary>
    /// State action to enable or disable the player's interaction raycast.
    /// Assumes the PlayerInteractionManager script has public methods EnableRaycast() and DisableRaycast().
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Set Interaction Raycast State")]
    public class SetInteractionRaycastStateActionSO : StateAction
    {
        [Tooltip("Should the interaction raycast be enabled in this state?")]
        public bool enableRaycast = true;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            // Access the PlayerInteractionManager component via the MenuManager reference
            if (manager != null && manager.player != null)
            {
                PlayerInteractionManager interactionManager = manager.player.GetComponent<PlayerInteractionManager>();
                if (interactionManager != null)
                {
                    if (enableRaycast)
                    {
                        interactionManager.EnableRaycast(); // Assumes this method exists
                        // Debug.Log($"SetInteractionRaycastStateAction: Interaction raycast enabled."); // Optional debug
                    }
                    else
                    {
                        interactionManager.DisableRaycast(); // Assumes this method exists
                        // Debug.Log($"SetInteractionRaycastStateAction: Interaction raycast disabled."); // Optional debug
                    }
                }
                else
                {
                    Debug.LogWarning($"SetInteractionRaycastStateActionSO: Player GameObject does not have a PlayerInteractionManager component.", manager.player);
                }
            }
            else
            {
                Debug.LogWarning($"SetInteractionRaycastStateActionSO: MenuManager or player reference is null.");
            }
        }
    }
}