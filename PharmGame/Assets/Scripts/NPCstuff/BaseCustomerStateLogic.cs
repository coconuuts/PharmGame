// BaseCustomerStateLogic.cs
using System.Collections; // For IEnumerator
using UnityEngine;
using Game.NPC;
using UnityEngine.AI;

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
    public virtual IEnumerator StateCoroutine()
    {
        // Default implementation yields once and finishes
        yield break;
    }

    /// <summary>
    /// Called when the Customer AI exits this state.
    /// Use for cleanup actions.
    /// </summary>
    public virtual void OnExit()
    {
        // Debug.Log($"State Logic: {GetType().Name} OnExit.", this); // Optional logging
    }

    /// <summary>
    /// Defines which CustomerState enum value this logic component handles.
    /// Must be implemented by derived classes.
    /// </summary>
    public abstract CustomerState HandledState { get; } // Abstract property

        /// <summary>
    /// Helper coroutine for derived state logics to smoothly rotate the customer.
    /// Should be started using customerAI.StartManagedCoroutine().
    /// </summary>
    protected IEnumerator RotateTowardsTargetRoutine(Quaternion targetRotation)
    {
         Debug.Log($"{customerAI.gameObject.name}: Starting rotation towards {targetRotation.eulerAngles}.");
         // Access NavMeshAgent via the customerAI reference
         if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.isActiveAndEnabled)
         {
              customerAI.NavMeshAgent.isStopped = true; // Stop movement while rotating
              customerAI.NavMeshAgent.ResetPath(); // Clear path
         }

         Quaternion startRotation = customerAI.transform.rotation; // Access transform via customerAI
         float angleDifference = Quaternion.Angle(startRotation, targetRotation);
         if (angleDifference < 0.1f)
         {
              Debug.Log($"{customerAI.gameObject.name}: Already facing target direction.");
              yield break; // Already rotated, exit immediately
         }

         // Using rotationSpeed from CustomerAI (you might move this config value later)
         // Assume rotationSpeed is accessible via customerAI or a common config
         float rotationSpeed = customerAI.rotationSpeed; // Access the speed from AI

         float duration = angleDifference / (rotationSpeed * 100f); // Calculate duration based on speed
         if (duration < 0.2f) duration = 0.2f; // Minimum duration

         float timeElapsed = 0f;

         while (timeElapsed < duration)
         {
              // Access transform via the customerAI reference
              customerAI.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed / duration);
              timeElapsed += Time.deltaTime;
              yield return null; // Wait for the next frame
         }

         customerAI.transform.rotation = targetRotation; // Ensure final rotation is exact
         Debug.Log($"{customerAI.gameObject.name}: Rotation complete.");
    }
}