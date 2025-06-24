// Systems/UI/IPanelActivatable.cs (Suggesting a UI namespace)
using UnityEngine;

namespace Systems.UI // Suggesting a namespace for UI-related interfaces/scripts
{
    /// <summary>
    /// Interface for components that need to react when their GameObject panel is activated by a TabManager or similar system.
    /// </summary>
    public interface IPanelActivatable
    {
        /// <summary>
        /// Called when the GameObject this component is attached to becomes the active panel.
        /// </summary>
        void OnPanelActivated();

        /// <summary>
        /// Called when the GameObject this component is attached to becomes an inactive panel.
        /// </summary>
        void OnPanelDeactivated();
    }
}