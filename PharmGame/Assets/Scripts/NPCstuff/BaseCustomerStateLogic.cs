// BaseCustomerStateLogic.cs
using System.Collections; // For IEnumerator
using UnityEngine;
using Game.NPC;
using UnityEngine.AI;
using Game.NPC.Handlers;

/// <summary>
/// Base class for specific Customer AI state logic components.
/// Provides a structure for handling state entry, update, coroutines, and exit.
/// </summary>
public abstract class BaseCustomerStateLogic : MonoBehaviour // Inherit from MonoBehaviour
{
    // Protected reference to the main CustomerAI script
    protected CustomerAI customerAI;

    /// <summary>
    /// Called once when the state logic component is initialized by the CustomerAI.
    /// </summary>
    /// <param name="customerAI">The CustomerAI instance managing this state.</param>
    public virtual void Initialize(CustomerAI customerAI)
    {
        this.customerAI = customerAI;
        // Debug.Log($"State Logic: {GetType().Name} Initialized.", this); // Optional logging
    }

    /// <summary>
    /// Called when the Customer AI enters this state.
    /// Use for setup actions.
    /// </summary>
    public virtual void OnEnter()
    {
        // Debug.Log($"State Logic: {GetType().Name} OnEnter.", this); // Optional logging
        // Ensure the NavMeshAgent is enabled when entering any state that might move or stand on NavMesh
        if (customerAI?.MovementHandler?.Agent != null)
        {
             customerAI.MovementHandler.Agent.enabled = true;
        }
        else if (customerAI != null)
        {
             Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): MovementHandler or Agent is null during OnEnter for state {HandledState}!", this);
             // Consider forcing a transition to Exiting/ReturningToPool if movement is essential
             if (HandledState != CustomerState.Exiting && HandledState != CustomerState.ReturningToPool)
             {
                  customerAI.SetState(CustomerState.Exiting);
             }
        }
    }

    /// <summary>
    /// Called every frame while the Customer AI is in this state.
    /// Use for continuous logic or checking conditions for state transitions.
    /// </summary>
    public virtual void OnUpdate()
    {
        // Default implementation does nothing
    }

    /// <summary>
    /// Coroutine that runs while the Customer AI is in this state.
    /// Use for time-based actions or delays.
    /// </summary>
    /// <returns>IEnumerator for the coroutine.</returns>
    public abstract IEnumerator StateCoroutine();

    /// <summary>
    /// Called when the Customer AI exits this state.
    /// Use for cleanup actions.
    /// </summary>
    public virtual void OnExit()
    {
        // Debug.Log($"State Logic: {GetType().Name} OnExit.", this); // Optional logging
        // Stop any movement or rotation when exiting a state
        customerAI?.MovementHandler?.StopMoving();
        customerAI?.MovementHandler?.StopRotation();
    }

    /// <summary>
    /// Defines which CustomerState enum value this logic component handles.
    /// Must be implemented by derived classes.
    /// </summary>
    public abstract CustomerState HandledState { get; } // Abstract property
}