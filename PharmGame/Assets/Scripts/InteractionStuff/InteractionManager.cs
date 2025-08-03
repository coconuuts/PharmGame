// --- START OF FILE InteractionManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using Systems.Interaction; // Needed for IInteractable
using System; // Needed for Type
using System.Linq; // Needed for LINQ operations

namespace Systems.Interaction // Place in the Interaction namespace
{
    /// <summary>
    /// Singleton manager responsible for registering and controlling the enabled state
    /// of IInteractable components across the scene.
    /// </summary>
    public class InteractionManager : MonoBehaviour
    {
        public static InteractionManager Instance { get; private set; }

        // Dictionary to store registered interactables, mapped by their GameObject
        // Using a List<IInteractable> allows for multiple interactables on the same object.
        private Dictionary<GameObject, List<IInteractable>> registeredInteractables = new Dictionary<GameObject, List<IInteractable>>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Optional: Make persistent if needed across scenes
                // DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning("InteractionManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
             // Ensure all prompts are deactivated if the manager is destroyed
             foreach(var list in registeredInteractables.Values)
             {
                  foreach(var interactable in list)
                  {
                       if (interactable is MonoBehaviour mono && mono.enabled)
                       {
                             interactable.DeactivatePrompt();
                       }
                  }
             }
        }

        /// <summary>
        /// Registers an IInteractable component with the manager.
        /// Should be called by the IInteractable component's Awake method.
        /// </summary>
        /// <param name="interactable">The IInteractable component to register.</param>
        public void RegisterInteractable(IInteractable interactable)
        {
            if (interactable == null)
            {
                Debug.LogWarning("InteractionManager: Attempted to register a null interactable.", this);
                return;
            }

            if (!(interactable is MonoBehaviour monoInteractable))
            {
                Debug.LogError($"InteractionManager: Attempted to register an IInteractable ({interactable.GetType().Name}) that is not a MonoBehaviour. Only MonoBehaviour IInteractables can be managed.", interactable as UnityEngine.Object);
                return;
            }

            GameObject go = monoInteractable.gameObject;

            if (!registeredInteractables.ContainsKey(go))
            {
                registeredInteractables.Add(go, new List<IInteractable>());
            }

            if (!registeredInteractables[go].Contains(interactable))
            {
                registeredInteractables[go].Add(interactable);
                // Initially disable the component. Its enabled state will be managed externally.
                monoInteractable.enabled = false;
                Debug.Log($"InteractionManager: Registered interactable {interactable.GetType().Name} on GameObject {go.name}. Initially disabled.", go);

                // --- Check the enableOnStart flag and enable if true ---
                bool shouldEnableOnStart = false;
                // Use reflection to access the private 'enableOnStart' field.
                var enableOnStartField = monoInteractable.GetType().GetField("enableOnStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (enableOnStartField != null && enableOnStartField.FieldType == typeof(bool))
                {
                    // Field found, get its value.
                    shouldEnableOnStart = (bool)enableOnStartField.GetValue(monoInteractable);
                }
                else
                {
                    // This warning helps catch interactables that are missing the flag.
                    Debug.Log($"InteractionManager: Registered interactable {interactable.GetType().Name} on {go.name} does not have a 'bool enableOnStart' field. It will remain disabled by default.", go);
                }

                if (shouldEnableOnStart)
                {
                    // Use the new non-generic helper method to enable this specific interactable,
                    // which will also disable any other registered interactables on the same GameObject.
                    Debug.Log($"InteractionManager: {interactable.GetType().Name} on {go.name} has enableOnStart = true. Enabling it.", go);
                    EnableOnlyInteractableComponent(go, interactable);
                }
            }
            else
            {
                Debug.LogWarning($"InteractionManager: Interactable {interactable.GetType().Name} on GameObject {go.name} is already registered.", go);
            }
        }

        /// <summary>
        /// Unregisters an IInteractable component from the manager.
        /// Should be called by the IInteractable component's OnDestroy method.
        /// </summary>
        /// <param name="interactable">The IInteractable component to unregister.</param>
        public void UnregisterInteractable(IInteractable interactable)
        {
            if (interactable == null) return; // Ignore null
            if (!(interactable is MonoBehaviour monoInteractable)) return; // Only manage MonoBehaviours

            GameObject go = monoInteractable.gameObject;

            if (registeredInteractables.ContainsKey(go))
            {
                if (registeredInteractables[go].Remove(interactable))
                {
                    Debug.Log($"InteractionManager: Unregistered interactable {interactable.GetType().Name} on GameObject {go.name}.", go);
                }

                if (registeredInteractables[go].Count == 0)
                {
                    registeredInteractables.Remove(go);
                }
            }
        }

        /// <summary>
        /// Enables the MonoBehaviour component of a specific IInteractable type on a given GameObject.
        /// Automatically disables any other registered IInteractable components on the same GameObject.
        /// </summary>
        /// <typeparam name="T">The type of the IInteractable component to enable.</typeparam>
        /// <param name="targetObject">The GameObject the interactable component is attached to.</param>
        /// <returns>The enabled IInteractable component instance, or null if not found/registered.</returns>
        public T EnableOnlyInteractableComponent<T>(GameObject targetObject) where T : MonoBehaviour, IInteractable
        {
             if (targetObject == null)
             {
                 Debug.LogWarning("InteractionManager: Attempted to enable interactable on a null GameObject.", this);
                 return null;
             }

             T foundInteractable = null;

             if (registeredInteractables.TryGetValue(targetObject, out List<IInteractable> interactablesOnObject))
             {
                  foundInteractable = interactablesOnObject.OfType<T>().FirstOrDefault();

                  if (foundInteractable != null)
                  {
                        // Use the helper to do the actual enabling/disabling
                        EnableOnlyInteractableComponent(targetObject, foundInteractable);
                  }
                  else
                  {
                       Debug.LogWarning($"InteractionManager: Cannot find registered interactable of type {typeof(T).Name} on GameObject {targetObject.name}.", targetObject);
                  }
             }
             else
             {
                  Debug.LogWarning($"InteractionManager: GameObject {targetObject.name} has no registered interactables.", targetObject);
             }

             return foundInteractable;
        }

        /// <summary>
        /// Non-generic helper to enable a specific registered IInteractable component on a GameObject.
        /// Used internally by RegisterInteractable for enableOnStart and by the generic version.
        /// Disables all other registered IInteractable components on the same GameObject.
        /// </summary>
        private void EnableOnlyInteractableComponent(GameObject targetObject, IInteractable interactableToEnable)
        {
             if (targetObject == null || interactableToEnable == null) return;

             if (registeredInteractables.TryGetValue(targetObject, out List<IInteractable> interactablesOnObject))
             {
                  if (interactablesOnObject.Contains(interactableToEnable))
                  {
                       Debug.Log($"InteractionManager: Enabling specific registered interactable {interactableToEnable.GetType().Name} on {targetObject.name} and disabling others.", targetObject);

                       foreach (var interactable in interactablesOnObject)
                       {
                            if (interactable is MonoBehaviour mono)
                            {
                                 if (interactable == interactableToEnable)
                                 {
                                      if (!mono.enabled) mono.enabled = true;
                                 }
                                 else
                                 {
                                      if (mono.enabled)
                                      {
                                           interactable.DeactivatePrompt();
                                           mono.enabled = false;
                                      }
                                 }
                            }
                       }
                  }
                  else
                  {
                       Debug.LogWarning($"InteractionManager: Attempted to enable interactable {interactableToEnable.GetType().Name} on {targetObject.name}, but it is not registered for this GameObject.", targetObject);
                  }
             }
             else
             {
                  Debug.LogWarning($"InteractionManager: GameObject {targetObject.name} has no registered interactables when trying to enable a specific one.", targetObject);
             }
        }

        /// <summary>
        /// Disables the MonoBehaviour component of a specific IInteractable type on a given GameObject.
        /// </summary>
        public void DisableInteractableComponent<T>(GameObject targetObject) where T : MonoBehaviour, IInteractable
        {
             if (targetObject == null) return;

             if (registeredInteractables.TryGetValue(targetObject, out List<IInteractable> interactablesOnObject))
             {
                  var interactableToDisable = interactablesOnObject.OfType<T>().FirstOrDefault();

                  if (interactableToDisable != null && interactableToDisable is MonoBehaviour mono)
                  {
                       if (mono.enabled)
                       {
                            Debug.Log($"InteractionManager: Disabling {typeof(T).Name} on {targetObject.name}.", targetObject);
                            interactableToDisable.DeactivatePrompt();
                            mono.enabled = false;
                       }
                  }
             }
        }

        /// <summary>
        /// Disables all registered IInteractable components on a given GameObject.
        /// </summary>
        public void DisableAllInteractablesOnGameObject(GameObject targetObject)
        {
             if (targetObject == null) return;

             if (registeredInteractables.TryGetValue(targetObject, out List<IInteractable> interactablesOnObject))
             {
                  Debug.Log($"InteractionManager: Disabling all registered interactables on GameObject {targetObject.name}.", targetObject);
                  foreach (var interactable in interactablesOnObject)
                  {
                       if (interactable is MonoBehaviour mono && mono.enabled)
                       {
                            interactable.DeactivatePrompt();
                            mono.enabled = false;
                       }
                  }
              }
        }
    }
}
// --- END OF FILE InteractionManager.cs ---