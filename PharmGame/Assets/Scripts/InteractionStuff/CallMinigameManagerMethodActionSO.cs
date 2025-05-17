using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse
using Systems.Minigame; // Assuming MinigameManager is here

namespace Systems.GameStates
{
    /// <summary>
    /// State action to call specific methods on the MinigameManager.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Call Minigame Manager Method")]
    public class CallMinigameManagerMethodActionSO : StateAction
    {
        public enum MinigameManagerMethod
        {
            Start,
            Reset
        }

        [Tooltip("The method to call on the MinigameManager.")]
        public MinigameManagerMethod methodToCall;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            if (MinigameManager.Instance != null)
            {
                switch (methodToCall)
                {
                    case MinigameManagerMethod.Start:
                        // Need data from the response to start the minigame
                        if (response is StartMinigameResponse startMinigameResponse)
                        {
                            MinigameManager.Instance.StartMinigame(startMinigameResponse.ItemsToScan, startMinigameResponse.CashRegisterInteractable); // Assumes StartMinigame takes these parameters
                            // Debug.Log("CallMinigameManagerMethodAction: Called StartMinigame."); // Optional debug
                        }
                        else
                        {
                            Debug.LogWarning($"CallMinigameManagerMethodActionSO: Cannot call Start method. InteractionResponse was not a StartMinigameResponse.");
                        }
                        break;
                    case MinigameManagerMethod.Reset:
                        MinigameManager.Instance.ResetMinigame(); // Assumes this method exists
                        // Debug.Log("CallMinigameManagerMethodAction: Called ResetMinigame."); // Optional debug
                        break;
                }
            }
            else
            {
                Debug.LogError("CallMinigameManagerMethodActionSO: MinigameManager Instance is null! Cannot call method.");
            }
        }
    }
}