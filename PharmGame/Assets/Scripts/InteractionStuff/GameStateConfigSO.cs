using UnityEngine;
using System.Collections.Generic;
using Systems.Interaction;
// We need to include the MenuManager's namespace to reference its GameState enum
// Make sure the namespace below matches where your MenuManager script is located
// If MenuManager is just in the global namespace, you might need to adjust this.
#pragma warning disable CS0105 // Duplicate using directive
using Systems.GameStates; // This namespace is for THIS script and StateAction
#pragma warning restore CS0105 // Duplicate using directive


// Assuming MenuManager is in the global namespace or Systems.Interaction namespace based on your provided code
// If MenuManager is in Systems.Interaction, add: using MenuManager = Systems.Interaction.MenuManager;
// Or if MenuManager is in Systems.GameStates, it's already covered by the current namespace.
// Let's assume MenuManager is in Systems.GameStates for consistency with this new structure.
using MenuManager = Systems.GameStates.MenuManager; // Use alias if needed, or remove if in same namespace

namespace Systems.GameStates
{
    /// <summary>
    /// Scriptable Object to configure the actions performed when entering and exiting a specific game state.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Game State Config")]
    public class GameStateConfigSO : ScriptableObject
    {
        [Tooltip("The game state that this configuration applies to.")]
        public MenuManager.GameState gameState; // Reference the enum from MenuManager

        [Tooltip("List of actions to execute when entering this state.")]
        public List<StateAction> entryActions = new List<StateAction>();

        [Tooltip("List of actions to execute when exiting this state.")]
        public List<StateAction> exitActions = new List<StateAction>();

        /// <summary>
        /// Executes all entry actions defined for this state config.
        /// </summary>
        /// <param name="response">The InteractionResponse that triggered the state change.</param>
        /// <param name="manager">Reference to the MenuManager.</param>
        public void ExecuteEntryActions(InteractionResponse response, MenuManager manager)
        {
            foreach (var action in entryActions)
            {
                if (action != null)
                {
                    action.Execute(response, manager);
                }
            }
        }

        /// <summary>
        /// Executes all exit actions defined for this state config.
        /// </summary>
        /// <param name="response">The InteractionResponse that triggered the state change.</param>
        /// <param name="manager">Reference to the MenuManager.</param>
        public void ExecuteExitActions(InteractionResponse response, MenuManager manager)
        {
            foreach (var action in exitActions)
            {
                if (action != null)
                {
                    action.Execute(response, manager);
                }
            }
        }
    }
}