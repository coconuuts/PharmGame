using UnityEngine;
using Systems.Interaction; // Needed to access InteractionResponse types

namespace Systems.GameStates // Place in the same namespace as MenuManager and State Actions
{
    /// <summary>
    /// Handles InteractionResponse objects that represent simple actions
    /// and do not trigger a change in the main game state.
    /// </summary>
    public class SimpleActionDispatcher
    {
        /// <summary>
        /// Dispatches a simple interaction response to the appropriate handler logic.
        /// </summary>
        /// <param name="response">The InteractionResponse object to dispatch.</param>
        public void Dispatch(InteractionResponse response)
        {
            if (response == null)
            {
                Debug.LogWarning("SimpleActionDispatcher: Received a null response. Ignoring.");
                return;
            }

            // Use 'is' to check the specific type of InteractionResponse
            if (response is ToggleLightResponse toggleLightResponse)
            {
                Debug.Log("SimpleActionDispatcher: Handling ToggleLightResponse.");
                // Execute the action directly from the response data
                toggleLightResponse.LightSwitch?.ToggleLightState();
            }
            // Add more 'else if' blocks here for other simple, stateless interaction response types
            // else if (response is PlaySoundResponse playSoundResponse) { /* ... handle playing a sound ... */ }
            // else if (response is GrantItemResponse grantItemResponse) { /* ... handle giving player an item ... */ }
            else
            {
                // Log a warning if the response type is not handled by this dispatcher
                Debug.LogWarning($"SimpleActionDispatcher: Received unhandled simple InteractionResponse type: {response.GetType().Name}. Ensure this type is handled either here or by MenuManager's state transitions.");
            }
        }
    }
}