// Systems/Interaction/MultiInteractableManager.cs
// Place this script on GameObjects that have multiple IInteractable components.

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Systems.Interaction; // Make sure this using is present

namespace Systems.Interaction // Use the Interaction namespace
{
    /// <summary>
    /// Manages multiple IInteractable components on a single GameObject,
    /// allowing external scripts (like PlayerInteractionManager) to query
    /// which IInteractable is currently active.
    /// </summary>
    public class MultiInteractableManager : MonoBehaviour
    {
        private List<IInteractable> _allInteractables;
        private IInteractable _currentActiveInteractable;

        [Tooltip("Optionally specify the type name of the initial active interactable (e.g., 'OpenNPCInventory').")]
        [SerializeField]
        private string initialActiveInteractableTypeName = ""; // Use type name as a string

        // Public property for PlayerInteractionManager to access
        public IInteractable CurrentActiveInteractable => _currentActiveInteractable;

        private void Awake()
        {
            // Find all components on this GameObject that implement IInteractable
            // and are also MonoBehaviours (since IInteractable is an interface)
            _allInteractables = GetComponents<MonoBehaviour>()
                                .OfType<IInteractable>()
                                .ToList();

            if (_allInteractables.Count == 0)
            {
                Debug.LogWarning($"MultiInteractableManager on {gameObject.name}: No IInteractable components found.", this);
                enabled = false; // No need to run if nothing to manage
                return;
            }

            Debug.Log($"MultiInteractableManager on {gameObject.name}: Found {_allInteractables.Count} IInteractable components.", this);

            // Set the initial active interactable if specified
            if (!string.IsNullOrEmpty(initialActiveInteractableTypeName))
            {
                 // Attempt to find the component by type name
                 var initialType = _allInteractables.FirstOrDefault(
                     interactable => interactable.GetType().Name == initialActiveInteractableTypeName
                 );

                 if (initialType != null)
                 {
                     // Use the internal SetActiveInteractable to handle initial setup correctly
                     InternalSetActiveInteractable(initialType);
                 }
                 else
                 {
                     Debug.LogWarning($"MultiInteractableManager on {gameObject.name}: Initial active interactable type '{initialActiveInteractableTypeName}' not found. Setting first found interactable as active.", this);
                     InternalSetActiveInteractable(_allInteractables[0]); // Default to the first one found
                 }
            }
            else
            {
                // If no initial type specified, set the first one found as active by default
                 InternalSetActiveInteractable(_allInteractables[0]);
            }

            // Ensure only the initially active component is enabled if we are managing component enabled state
             foreach(var interactable in _allInteractables)
             {
                  if (interactable is MonoBehaviour mono)
                  {
                       mono.enabled = (interactable == _currentActiveInteractable);
                  }
             }
        }

        /// <summary>
        /// Internal method to set the specified interactable component as the currently active one.
        /// Handles prompt deactivation and component enabling/disabling.
        /// </summary>
        /// <param name="interactable">The IInteractable component to make active.</param>
        private void InternalSetActiveInteractable(IInteractable interactable)
        {
            if (interactable == null)
            {
                Debug.LogWarning($"MultiInteractableManager on {gameObject.name}: Attempted to set a null interactable as active.", this);
                // Deactivate prompt and disable component for the old one if it exists
                 if (_currentActiveInteractable != null)
                 {
                     _currentActiveInteractable.DeactivatePrompt();
                     if (_currentActiveInteractable is MonoBehaviour currentMono)
                     {
                          currentMono.enabled = false;
                     }
                 }
                 _currentActiveInteractable = null;
                 Debug.Log($"MultiInteractableManager on {gameObject.name}: Active interactable set to null.", this);
                return;
            }

            // Check if the interactable is one of the ones managed by this component
            if (!_allInteractables.Contains(interactable))
            {
                Debug.LogWarning($"MultiInteractableManager on {gameObject.name}: Attempted to set an interactable as active that is not managed by this component: {interactable.GetType().Name}", this);
                return;
            }

            if (_currentActiveInteractable != interactable)
            {
                Debug.Log($"MultiInteractableManager on {gameObject.name}: Setting active interactable to {interactable.GetType().Name}.", this);

                // Deactivate prompt on the old active one, if any
                if (_currentActiveInteractable != null)
                {
                    _currentActiveInteractable.DeactivatePrompt();
                    // Disable the component itself
                    if (_currentActiveInteractable is MonoBehaviour oldMono)
                    {
                         oldMono.enabled = false;
                    }
                }

                // Set the new active one
                _currentActiveInteractable = interactable;

                // Enable the component itself
                 if (_currentActiveInteractable is MonoBehaviour newMono)
                 {
                      newMono.enabled = true;
                 }

                // Note: Prompt activation is handled by PlayerInteractionManager's raycast logic
                // when it detects this object and gets the new active interactable.
            }
        }


        /// <summary>
        /// Sets the specified interactable component as the currently active one.
        /// Automatically deactivates the prompt on the previously active one.
        /// Note: PlayerInteractionManager handles prompt activation based on raycast.
        /// </summary>
        /// <param name="interactable">The IInteractable component to make active.</param>
        public void SetActiveInteractable(IInteractable interactable)
        {
            InternalSetActiveInteractable(interactable);
        }


        /// <summary>
        /// Sets the interactable component of the specified type as the currently active one.
        /// </summary>
        /// <typeparam name="T">The type of the IInteractable component to make active.</typeparam>
        public void SetActiveInteractable<T>() where T : MonoBehaviour, IInteractable // Constraint added
        {
            var interactableToSet = _allInteractables.OfType<T>().FirstOrDefault();

            if (interactableToSet != null)
            {
                InternalSetActiveInteractable(interactableToSet);
            }
            else
            {
                Debug.LogWarning($"MultiInteractableManager on {gameObject.name}: Attempted to set active interactable of type {typeof(T).Name}, but no such component was found on this GameObject.", this);
            }
        }

        /// <summary>
        /// Sets the interactable component by its exact type name as the currently active one.
        /// </summary>
        /// <param name="typeName">The exact type name of the IInteractable component (e.g., "OpenInventory", "EnterComputer").</param>
        public void SetActiveInteractable(string typeName)
        {
             var interactableToSet = _allInteractables.FirstOrDefault(i => i.GetType().Name == typeName);
             if (interactableToSet != null)
             {
                  InternalSetActiveInteractable(interactableToSet);
             }
             else
             {
                  Debug.LogWarning($"MultiInteractableManager on {gameObject.name}: Attempted to set active interactable by type name '{typeName}', but no component with that type name was found on this GameObject.", this);
             }
        }

        /// <summary>
        /// Deactivates the prompt on the current active interactable and sets the active interactable to null.
        /// </summary>
        public void DeactivateCurrentInteractable()
        {
             InternalSetActiveInteractable(null); // Setting null effectively deactivates the current one
        }


        // Optional: Provide a list of found interactable types in the inspector
        [ContextMenu("List Found Interactable Types")]
        private void ListFoundInteractableTypes()
        {
            if (_allInteractables == null || _allInteractables.Count == 0)
            {
                Debug.Log($"MultiInteractableManager on {gameObject.name}: No IInteractable components found in Awake.", this);
                return;
            }
            Debug.Log($"MultiInteractableManager on {gameObject.name}: Found the following IInteractable types:");
            foreach (var interactable in _allInteractables)
            {
                Debug.Log($"- {interactable.GetType().Name}");
            }
        }

         private void OnDestroy()
         {
              // Ensure prompt is deactivated if this object is destroyed while active
              if (_currentActiveInteractable != null)
              {
                   _currentActiveInteractable.DeactivatePrompt();
              }
         }
    }
}