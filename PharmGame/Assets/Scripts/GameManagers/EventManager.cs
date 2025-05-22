using System;
using System.Collections.Generic;
using UnityEngine; // Included for Debug.Log, can be removed if not needed for logging

namespace Game.Events // Suggesting a dedicated namespace for your event system
{
    /// <summary>
    /// A static class providing a simple global event bus.
    /// Allows systems to publish events and other systems to subscribe to them
    /// without direct knowledge of each other.
    /// </summary>
    public static class EventManager
    {
        // Dictionary to hold delegates for each event type.
        // Key: Type of the event (e.g., typeof(NpcReachedDestinationEvent))
        // Value: A chain of delegates (System.Action<T>) to call when the event is published.
        private static readonly Dictionary<Type, Delegate> eventDictionary = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Subscribes a handler method to a specific event type.
        /// </summary>
        /// <typeparam name="T">The type of the event (e.g., NpcReachedDestinationEvent).</typeparam>
        /// <param name="handler">The method to call when the event is published. Must match the event type's signature (Action<T>).</param>
        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                Debug.LogWarning($"EventManager: Attempted to subscribe a null handler for event type {typeof(T).Name}.");
                return;
            }

            Type type = typeof(T);

            if (!eventDictionary.TryGetValue(type, out Delegate existingDelegate))
            {
                // If no delegates exist for this type, add the new handler
                eventDictionary[type] = handler;
                // Debug.Log($"EventManager: First handler subscribed for {type.Name}."); // Optional log
            }
            else
            {
                // If delegates exist, combine the new handler with the existing chain
                // Delegate.Combine creates a new delegate that calls both the old and new handlers
                eventDictionary[type] = Delegate.Combine(existingDelegate, handler);
                // Debug.Log($"EventManager: Handler added for existing event type {type.Name}."); // Optional log
            }
        }

        /// <summary>
        /// Unsubscribes a handler method from a specific event type.
        /// Should be called when a listener is no longer active (e.g., in OnDisable or OnDestroy).
        /// </summary>
        /// <typeparam name="T">The type of the event.</typeparam>
        /// <param name="handler">The handler method to remove.</param>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                // Attempting to unsubscribe a null handler is harmless but could indicate a logic error elsewhere
                // Debug.LogWarning($"EventManager: Attempted to unsubscribe a null handler for event type {typeof(T).Name}.");
                return;
            }

            Type type = typeof(T);

            if (eventDictionary.TryGetValue(type, out Delegate existingDelegate))
            {
                // Delegate.Remove creates a new delegate chain without the specified handler
                Delegate newDelegate = Delegate.Remove(existingDelegate, handler);

                if (newDelegate == null)
                {
                    // If the new delegate is null, it means the last handler was removed
                    eventDictionary.Remove(type);
                    // Debug.Log($"EventManager: Last handler unsubscribed for {type.Name}. Event type removed from dictionary."); // Optional log
                }
                else
                {
                    // Otherwise, update the dictionary with the new delegate chain
                    eventDictionary[type] = newDelegate;
                    // Debug.Log($"EventManager: Handler unsubscribed for {type.Name}."); // Optional log
                }
            }
            else
            {
                // Attempting to unsubscribe a handler from an event type that has no subscribers
                // Debug.LogWarning($"EventManager: Attempted to unsubscribe handler for event type {type.Name}, but no subscribers were found."); // Optional log
            }
        }

        /// <summary>
        /// Publishes an event, triggering all subscribed handlers for that event type.
        /// </summary>
        /// <typeparam name="T">The type of the event.</typeparam>
        /// <param name="eventArgs">The event data/arguments to pass to the handlers.</param>
        public static void Publish<T>(T eventArgs)
        {
            Type type = typeof(T);

            if (eventDictionary.TryGetValue(type, out Delegate delegateChain))
            {
                // GetInvocationList returns an array of individual delegates in the chain.
                // We iterate through this list and dynamically invoke each one.
                // Using foreach on Delegate is also possible but iterating the list is often safer with null checks.
                Delegate[] handlers = delegateChain.GetInvocationList();

                foreach (Delegate handler in handlers)
                {
                    try
                    {
                        // Explicitly cast and invoke for better performance and type safety than DynamicInvoke
                        ((Action<T>)handler).Invoke(eventArgs);
                    }
                    catch (Exception e)
                    {
                        // Log any exceptions thrown by an individual handler
                        // This prevents one faulty handler from stopping others
                        Debug.LogError($"EventManager: Error executing event handler for {type.Name}: {e}", handler.Target as UnityEngine.Object); // Use handler.Target to potentially link to GameObject
                    }
                }
                // Debug.Log($"EventManager: Published event {type.Name}. {handlers.Length} handlers invoked."); // Optional log
            }
            else
            {
                // No subscribers for this event type
                // Debug.Log($"EventManager: No subscribers for event type {type.Name}."); // Optional log (can be noisy)
            }
        }

        /// <summary>
        /// Clears all event subscriptions. Use with caution (e.g., on scene unload if EventManager is not persistent).
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            eventDictionary.Clear();
            Debug.Log("EventManager: All subscriptions cleared.");
        }
    }
}