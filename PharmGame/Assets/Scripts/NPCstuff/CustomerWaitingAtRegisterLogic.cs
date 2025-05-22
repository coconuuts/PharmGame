using UnityEngine;
using Game.NPC;
using System.Collections;
using CustomerManagement;
using Game.Events;

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

        // --- Use MovementHandler to stop movement ---
        customerAI.MovementHandler?.StopMoving(); // Use null conditional for safety
        // --------------------------------------------

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
        base.OnUpdate();

        // --- Impatience Timer Update and Check ---
        impatientTimer += Time.deltaTime; // Increment the timer

        if (impatientTimer >= impatientDuration) // Check if timer has reached the duration
        {
            Debug.Log($"{customerAI.gameObject.name}: IMPATIENT in WaitingAtRegister state after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", this); // Log timeout
            // --- Publish NpcImpatientEvent instead of setting state directly ---
            EventManager.Publish(new NpcImpatientEvent(customerAI.gameObject, CustomerState.WaitingAtRegister)); // Use the event struct
            // -------------------------------------------------------------------
            return; // Exit the OnUpdate method early
        }
        // -------------------------------------------
    }


    public override IEnumerator StateCoroutine()
    {
        Debug.Log($"{customerAI.gameObject.name}: WaitingAtRegisterRoutine started in CustomerWaitingAtRegisterLogic.");

        // --- Use MovementHandler to Rotate towards the target point's facing direction ---
        if (customerAI.MovementHandler != null && customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.browsePoint != null)
        {
             // Start the rotation coroutine managed by the MovementHandler
             customerAI.MovementHandler.StartRotatingTowards(customerAI.CurrentTargetLocation.Value.browsePoint.rotation);
             // Wait for the rotation coroutine to complete (as noted before, a better way needed for SO)
             yield return new WaitForSeconds(0.5f); // Small wait to allow rotation to begin
             // In a real scenario, wait until rotation is *finished*.
        }
        else
        {
             Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No valid target location stored for WaitingAtRegister rotation or MovementHandler missing!", this);
        }
        // ---------------------------------------------------------

        Debug.Log($"{customerAI.gameObject.name}: Waiting at register for player interaction.");

        // Stay in this state until the state changes externally
        while (customerAI.CurrentState == CustomerState.WaitingAtRegister)
        {
            yield return null;
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