using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse

namespace Systems.GameStates
{
    /// <summary>
    /// State action to set the Time.timeScale.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Set Time Scale")]
    public class SetTimeScaleActionSO : StateAction
    {
        [Tooltip("The target Time.timeScale value.")]
        public float targetTimeScale = 1f;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            Time.timeScale = targetTimeScale;
            // Debug.Log($"SetTimeScaleAction: Set Time Scale to {targetTimeScale}"); // Optional debug
        }
    }
}