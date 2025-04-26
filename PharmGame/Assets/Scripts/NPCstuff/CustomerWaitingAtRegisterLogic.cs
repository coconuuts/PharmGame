using UnityEngine;
using Game.NPC;
using System.Collections;

public class CustomerWaitingAtRegisterLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.WaitingAtRegister;

    private float impatientTimer; // Tracks how long the customer has been waiting in this state
    private float impatientDuration; // The random duration they will wait before becoming impatient

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering WaitingAtRegister state.");
        // --- Impatience Timer Start ---
        impatientDuration = Random.Range(10f, 15f); // Set a random duration
        impatientTimer = 0f; // Reset the timer
        Debug.Log($"{customerAI.gameObject.name}: Starting impatience timer for {impatientDuration:F2} seconds.", this); // Log timer start
        // ------------------------------


        // Ensure NavMeshAgent is stopped upon reaching the register
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled)
        {
            customerAI.NavMeshAgent.isStopped = true;
            customerAI.NavMeshAgent.ResetPath();
        }

        // --- Find and cache the CashRegisterInteractable (if not already) and signal arrival ---
        // This logic was previously in CustomerAI.SetState(CustomerState.WaitingAtRegister)
        if (customerAI.CachedCashRegister == null) // Use the public property
        {
             // Find the cash register by tag (Ensure your register GO has this tag!)
             GameObject registerGO = GameObject.FindGameObjectWithTag("CashRegister");
             if (registerGO != null)
             {
                  customerAI.CachedCashRegister = registerGO.GetComponent<CashRegisterInteractable>(); // Cache on the main AI
             }
        }

        if (customerAI.CachedCashRegister != null) // Use the public property
        {
             Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Notifying CashRegister '{customerAI.CachedCashRegister.gameObject.name}' of arrival.", customerAI.gameObject);
             customerAI.CachedCashRegister.CustomerArrived(customerAI); // Call the method on the cached register
        }
        else
        {
             Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): Could not find CashRegisterInteractable by tag 'CashRegister'! Cannot complete transaction flow.", this);
             // Cannot wait at register, exit instead
             customerAI.SetState(CustomerState.Exiting); // Transition via the AI
             return; // Exit OnEnter early
        }
        // --------------------------------------------------------

        Debug.Log($"{customerAI.gameObject.name}: Starting WaitingAtRegisterRoutine.");
        // The StateCoroutine will handle rotation and waiting.
    }

    // In CustomerWaitingAtRegisterLogic.cs, inside OnUpdate()
    public override void OnUpdate()
    {
        base.OnUpdate(); // Call the base OnUpdate (currently empty)

        // --- Impatience Timer Update and Check ---
        impatientTimer += Time.deltaTime; // Increment the timer

        if (impatientTimer >= impatientDuration) // Check if timer has reached the duration
        {
            Debug.Log($"{customerAI.gameObject.name}: IMPATIENT in WaitingAtRegister state after {impatientTimer:F2} seconds. Exiting.", this); // Log timeout
            customerAI.SetState(CustomerState.Exiting); // Transition to the Exiting state
            // No need for further logic in this Update cycle after setting state
            return; // Exit the OnUpdate method early
        }
        // -------------------------------------------
    }


    public override IEnumerator StateCoroutine()
    {
        Debug.Log($"{customerAI.gameObject.name}: WaitingAtRegisterRoutine started in CustomerWaitingAtRegisterLogic.");

        // --- Rotate towards the target point's (register point) facing direction ---
        // Access the target rotation via customerAI.CurrentTargetLocation
        if (customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.browsePoint != null)
        {
             // Start the rotation coroutine managed by CustomerAI
             yield return customerAI.StartManagedCoroutine(RotateTowardsTargetRoutine(customerAI.CurrentTargetLocation.Value.browsePoint.rotation));
        }
        else // Fallback if target is somehow null
        {
             Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No valid target location stored for WaitingAtRegister rotation!", this);
             // What should happen if rotation target is missing? Maybe just continue waiting.
        }
        // ---------------------------------------------------------

        Debug.Log($"{customerAI.gameObject.name}: Waiting at register for player interaction.");

        // Stay in this state until the state changes externally
        // The state transition is handled by external calls like StartTransaction() or OnTransactionCompleted()
        while (customerAI.CurrentState == CustomerState.WaitingAtRegister) // Check the state on the main AI script
        {
            yield return null; // Wait for the next frame
        }

        Debug.Log($"{customerAI.gameObject.name}: WaitingAtRegisterRoutine finished.");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log($"{customerAI.gameObject.name}: Exiting WaitingAtRegister state.");
        impatientTimer = 0f; // <-- RESET TIMER ON EXIT
        // Any cleanup specific to exiting waiting could go here
    }
}