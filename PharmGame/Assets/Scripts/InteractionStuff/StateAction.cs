using UnityEngine;
using Systems.Interaction; // Needed to access InteractionResponse

namespace Systems.GameStates // Suggest creating a dedicated namespace for state management
{
    /// <summary>
    /// Abstract base class for Scriptable Objects that define actions to be executed
    /// during game state transitions.
    /// </summary>
    public abstract class StateAction : ScriptableObject
    {
        /// <summary>
        /// Executes the defined action.
        /// </summary>
        /// <param name="response">The InteractionResponse that triggered the state change (can be null for internal changes).</param>
        /// <param name="manager">Reference to the MenuManager instance, allowing actions to interact with it if needed.</param>
        public abstract void Execute(InteractionResponse response, MenuManager manager);
    }
}