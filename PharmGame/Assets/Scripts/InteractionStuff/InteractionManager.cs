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
        // Using a List<IInteractable> allows for multiple interactables on the same object,
        // which was the original use case for MultiInteractableManager.
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
             // This is a basic cleanup. More robust cleanup might be needed depending on game lifecycle.
             foreach(var list in registeredInteractables.Values)
             {
                  foreach(var interactable in list)
                  {
                       // Check if the interactable component is a MonoBehaviour and is enabled
                       // Only deactivate prompt if it was actually active
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
                // This is the core change from the old system.
                monoInteractable.enabled = false;
                Debug.Log($"InteractionManager: Registered interactable {interactable.GetType().Name} on GameObject {go.name}. Initially disabled.", go);

                // --- NEW: Check the enableOnStart flag and enable if true ---
                // We need to use reflection or a common interface/base class to access enableOnStart
                // Assuming enableOnStart is a public property or field on the MonoBehaviour implementing IInteractable
                // A safer way is to define a base class or interface for this flag.
                // Let's assume for now that all relevant IInteractables have a public bool enableOnStart field/property.
                // A more robust solution would involve a dedicated interface like IDefaultEnableInteractable.

                // Using reflection (less performant, but flexible if no common base/interface)
                // System.Reflection.PropertyInfo propInfo = monoInteractable.GetType().GetProperty("enableOnStart");
                // System.Reflection.FieldInfo fieldInfo = monoInteractable.GetType().GetField("enableOnStart");

                // bool shouldEnableOnStart = false;
                // if (propInfo != null && propInfo.PropertyType == typeof(bool))
                // {
                //     shouldEnableOnStart = (bool)propInfo.GetValue(monoInteractable);
                // }
                // else if (fieldInfo != null && fieldInfo.FieldType == typeof(bool))
                // {
                //     shouldEnableOnStart = (bool)fieldInfo.GetValue(monoInteractable);
                // }
                // else
                // {
                //     Debug.LogWarning($"InteractionManager: Registered interactable {interactable.GetType().Name} on {go.name} does not have a public 'bool enableOnStart' field or property. Cannot determine default enabled state.", go);
                // }

                // Assuming you added the [SerializeField] private bool enableOnStart = false; field directly to each script,
                // we can try to access it directly if they are in the same assembly or use reflection.
                // Let's modify the IInteractable interface or create a base class for a cleaner approach in a real project.
                // For this exercise, let's assume a public property or field named 'EnableOnStart' exists.
                // If it's private [SerializeField], direct access won't work without reflection or changing access modifier.
                // Let's change the field to public for simplicity in this example:
                // public bool enableOnStart = false; // Change in IInteractable scripts

                // OR, if we want to keep it [SerializeField] private, we need reflection:
                 bool shouldEnableOnStart = false;
                 var enableOnStartField = monoInteractable.GetType().GetField("enableOnStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                 if (enableOnStartField != null && enableOnStartField.FieldType == typeof(bool))
                 {
                     shouldEnableOnStart = (bool)enableOnStartField.GetValue(monoInteractable);
                 }
                 else
                 {
                     Debug.LogWarning($"InteractionManager: Registered interactable {interactable.GetType().Name} on {go.name} does not have a field named 'enableOnStart' or it's not a bool. Cannot determine default enabled state.", go);
                 }


                if (shouldEnableOnStart)
                {
                    // Use the generic method to enable this specific interactable
                    // This will also disable any other registered interactables on the same GameObject
                    Debug.Log($"InteractionManager: {interactable.GetType().Name} on {go.name} has enableOnStart = true. Enabling it.", go);
                    // We need to call the generic method, but we only have the IInteractable reference.
                    // We can use reflection to call the generic method with the correct type.
                    // Or, add a non-generic overload to EnableOnlyInteractableComponent.
                    // Let's add a non-generic overload for simplicity.

                    // Call the new non-generic helper method
                    EnableOnlyInteractableComponent(go, interactable);
                }
                // --- END NEW ---
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
        /// Call this from NPC state SOs or other logic deciding which interaction is currently available.
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
                  // Find the specific interactable of type T
                  foundInteractable = interactablesOnObject.OfType<T>().FirstOrDefault();

                  if (foundInteractable != null)
                  {
                       Debug.Log($"InteractionManager: Enabling {typeof(T).Name} on {targetObject.name} and disabling others on this object.", targetObject);

                       // Iterate through all registered interactables on this object
                       foreach (var interactable in interactablesOnObject)
                       {
                            if (interactable is MonoBehaviour mono)
                            {
                                 if (interactable == foundInteractable)
                                 {
                                      // Enable the target interactable if it's not already enabled
                                       if (!mono.enabled) mono.enabled = true;
                                 }
                                 else
                                 {
                                      // Disable all other interactables on this object
                                      if (mono.enabled)
                                      {
                                           interactable.DeactivatePrompt(); // Deactivate prompt before disabling
                                           mono.enabled = false;
                                      }
                                 }
                            }
                       }
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

             return foundInteractable; // Return the component instance
        }

        /// <summary>
        /// Non-generic helper to enable a specific registered IInteractable component on a GameObject.
        /// Used internally by RegisterInteractable for enableOnStart.
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
        /// Use this if an interactable needs to be disabled without necessarily enabling another.
        /// </summary>
        /// <typeparam name="T">The type of the IInteractable component to disable.</typeparam>
        /// <param name="targetObject">The GameObject the interactable component is attached to.</param>
        public void DisableInteractableComponent<T>(GameObject targetObject) where T : MonoBehaviour, IInteractable
        {
             if (targetObject == null)
             {
                  Debug.LogWarning("InteractionManager: Attempted to disable interactable on a null GameObject.", this);
                  return;
             }

             if (registeredInteractables.TryGetValue(targetObject, out List<IInteractable> interactablesOnObject))
             {
                  // Find the specific interactable of type T
                  var interactableToDisable = interactablesOnObject.OfType<T>().FirstOrDefault();

                  if (interactableToDisable != null && interactableToDisable is MonoBehaviour mono)
                  {
                       if (mono.enabled)
                       {
                            Debug.Log($"InteractionManager: Disabling {typeof(T).Name} on {targetObject.name}.", targetObject);
                            interactableToDisable.DeactivatePrompt(); // Deactivate prompt before disabling
                            mono.enabled = false;
                       }
                       // else { Debug.Log($"InteractionManager: {typeof(T).Name} on {targetObject.name} was already disabled.", targetObject); }
                  }
                  // else { Debug.LogWarning($"InteractionManager: Cannot find registered interactable of type {typeof(T).Name} on GameObject {targetObject.name} to disable.", targetObject); }
             }
             // else { Debug.LogWarning($"InteractionManager: GameObject {targetObject.name} has no registered interactables when trying to disable {typeof(T).Name}.", targetObject); }
        }

        /// <summary>
        /// Disables all registered IInteractable components on a given GameObject.
        /// Useful when an object becomes completely non-interactive (e.g., NPC leaves).
        /// </summary>
        /// <param name="targetObject">The GameObject whose interactables should be disabled.</param>
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
              // else { Debug.LogWarning($"InteractionManager: GameObject {targetObject.name} has no registered interactables to disable all.", targetObject); }
        }

        // You could add other helper methods if needed, e.g., GetActiveInteractableOnGameObject<T>(GameObject go)
    }
}
// --- END OF FILE InteractionManager.cs ---