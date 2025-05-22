using UnityEngine;
using Game.NPC;
using System.Collections;
using CustomerManagement; // Needed for BrowseLocation

public class CustomerBrowseLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Browse;

    // Initialize is handled by the base class

    public override void OnEnter()
    {
        base.OnEnter(); // Call base OnEnter (enables Agent)
        Debug.Log($"{customerAI.gameObject.name}: Entering Browse state. Stopping movement and starting browse routine.");
        // --- Use MovementHandler to stop movement ---
        customerAI.MovementHandler?.StopMoving(); // Use null conditional for safety
        // --------------------------------------------

        // Note: Animation handler could be used here to trigger 'browsing' animation
        // customerAI.AnimationHandler?.Play("Browsing"); // Example
    }


    // OnUpdate is likely not needed as the main logic is in the coroutine
    // public override void OnUpdate() { base.OnUpdate(); }

    public override IEnumerator StateCoroutine()
    {
         Debug.Log($"{customerAI.gameObject.name}: BrowseRoutine started in CustomerBrowseLogic.");

         // --- Use MovementHandler to Rotate towards the target point's (browse point) facing direction ---
         // Access the target rotation via customerAI.CurrentTargetLocation
         if (customerAI.MovementHandler != null && customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.browsePoint != null)
         {
              // Start the rotation coroutine managed by the MovementHandler
              customerAI.MovementHandler.StartRotatingTowards(customerAI.CurrentTargetLocation.Value.browsePoint.rotation);
              // Wait for the rotation coroutine to complete (it runs within the handler)
              // There's no direct way to yield *on* the handler's coroutine from here.
              // Instead, the handler could publish an event when rotation is done, or
              // we add a check here. For now, let's just wait a small fixed duration
              // or yield until the handler reports not rotating (if we add that to the handler API).
              // A better approach for the SO states later will be to have the SO start/stop coroutines
              // via the StateMachineRunner, which then starts/stops them *on* the handlers if needed.
              // For this intermediate step, let's just yield a small time to allow rotation to start.
               yield return new WaitForSeconds(0.5f); // Small wait to allow rotation to begin
               // In a real scenario, you'd want to yield until rotation is *finished*.
               // Adding IsRotating property to MovementHandler would allow: yield return new WaitWhile(() => customerAI.MovementHandler.IsRotating);

         }
         else // Fallback if target is somehow null
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No valid target location stored for Browse rotation or MovementHandler missing!", this);
              customerAI.SetState(CustomerState.Exiting); // Decide what to do if no valid location
              yield break;
         }
         // ---------------------------------------------------------


         float browseTime = Random.Range(3f, 8f);
         Debug.Log($"{customerAI.gameObject.name}: Browse for {browseTime} seconds.");

         yield return new WaitForSeconds(browseTime);


         // --- Simulate Shopping Call (Remains the same, uses Shopper component) ---
         Debug.Log($"{customerAI.gameObject.name}: Finished Browse time. Simulating shopping now.");
         bool boughtItemsFromThisShelf = false;

         if (customerAI.Shopper != null && customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.inventory != null)
         {
              boughtItemsFromThisShelf = customerAI.Shopper.SimulateShopping(customerAI.CurrentTargetLocation.Value.inventory);
         }
         else
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Cannot simulate shopping. Shopper or Inventory reference is null!", this);
         }

         if (boughtItemsFromThisShelf)
          {
          customerAI.Shopper.ResetConsecutiveShelvesCount();
          }
          else
          {
          customerAI.Shopper.IncrementConsecutiveShelvesCount();
          }

          // --- Check if the customer is impatient due to not finding items ---
          if (customerAI.Shopper != null && customerAI.Shopper.GetConsecutiveShelvesCount() >= 3)
          {
          Debug.Log($"{customerAI.gameObject.name}: Visited 3 consecutive shelves without finding items. Exiting.", this);
          customerAI.SetState(CustomerState.Exiting);
          yield break;
          }
         // -----------------------------


         // --- Decide Next Step After Browse and Shopping (Remains the same) ---
         bool finishedShoppingTrip = customerAI.Shopper != null && (customerAI.Shopper.HasItems || customerAI.Shopper.DistinctItemCount >= Random.Range(customerAI.Shopper.MinItemsToBuy, customerAI.Shopper.MaxItemsToBuy + 1));

          if (finishedShoppingTrip)
          {
          // ... (Logic to check register occupancy and transition to Queue or MovingToRegister remains) ...
               Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Finished shopping trip. Total quantity to buy: {customerAI.Shopper?.TotalQuantityToBuy}. Distinct items: {customerAI.Shopper?.DistinctItemCount}.", customerAI.gameObject);

               if (customerAI.Manager != null && customerAI.Manager.IsRegisterOccupied())
               {
                    Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Register is occupied. Attempting to join queue.");
                    if (customerAI.Manager.TryJoinQueue(customerAI, out Transform assignedSpot, out int spotIndex))
                    {
                         customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null };
                         customerAI.AssignedQueueSpotIndex = spotIndex;
                         customerAI.SetState(CustomerState.Queue);
                    }
                    else
                    {
                         Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Register is occupied and queue is full! Exiting empty-handed (fallback).");
                         customerAI.SetState(CustomerState.Exiting);
                    }
               }
               else
               {
                    Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Register is free. Moving to register.");
                    customerAI.SetState(CustomerState.MovingToRegister);
               }
          }
          else // Not finished shopping trip
          {
               Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Shopping not complete, looking for next Browse location.");

               BrowseLocation? nextBrowseLocation = customerAI.Manager?.GetRandomBrowseLocation();

               if (nextBrowseLocation.HasValue && nextBrowseLocation.Value.browsePoint != null)
               {
                    customerAI.CurrentTargetLocation = nextBrowseLocation;
                    customerAI.SetState(CustomerState.Entering); // Transition back to entering to move
               }
               else // No more Browse locations available
               {
                    Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No more Browse locations available or valid!");
                    // ... (Fallback logic based on having items or not remains) ...
                    if (customerAI.Shopper != null && customerAI.Shopper.HasItems && customerAI.Manager != null && customerAI.Manager.IsRegisterOccupied())
                    {
                         Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse, register is occupied, attempting to join queue with items.");
                         if (customerAI.Manager.TryJoinQueue(customerAI, out Transform assignedSpot, out int spotIndex))
                         {
                              customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null };
                              customerAI.AssignedQueueSpotIndex = spotIndex;
                              customerAI.SetState(CustomerState.Queue);
                         }
                         else
                         {
                              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No more places to browse, register occupied, queue full! Exiting with items (fallback).");
                              customerAI.SetState(CustomerState.Exiting);
                         }
                    }
                    else if (customerAI.Shopper != null && customerAI.Shopper.HasItems)
                    {
                         Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse and has items. Heading to register.");
                         customerAI.SetState(CustomerState.MovingToRegister);
                    }
                    else
                    {
                         Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse and no items collected. Exiting.");
                         customerAI.SetState(CustomerState.Exiting);
                    }
               }
         }
         Debug.Log($"{customerAI.gameObject.name}: BrowseRoutine finished.");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log($"{customerAI.gameObject.name}: Exiting Browse state.");
        // Any cleanup specific to exiting browse could go here
    }
}