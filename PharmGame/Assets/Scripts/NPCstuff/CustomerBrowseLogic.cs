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
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering Browse state. Stopping and starting browse routine.");
        // Ensure NavMeshAgent is stopped while Browse
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled)
        {
            customerAI.NavMeshAgent.isStopped = true;
            customerAI.NavMeshAgent.ResetPath();
        }
    }

    // OnUpdate is likely not needed as the main logic is in the coroutine
    // public override void OnUpdate() { base.OnUpdate(); }

    public override IEnumerator StateCoroutine()
    {
         Debug.Log($"{customerAI.gameObject.name}: BrowseRoutine started in CustomerBrowseLogic.");

         // --- Rotate towards the target point's (browse point) facing direction ---
         // Access the target rotation via customerAI.CurrentTargetLocation
         if (customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.browsePoint != null)
         {
              // Start the rotation coroutine managed by CustomerAI
              yield return customerAI.StartManagedCoroutine(RotateTowardsTargetRoutine(customerAI.CurrentTargetLocation.Value.browsePoint.rotation));
         }
         else // Fallback if target is somehow null
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No valid target location stored for Browse rotation!", this);
              // Decide what to do if no valid location (e.g., exit)
              customerAI.SetState(CustomerState.Exiting);
              yield break; // Exit coroutine
         }
         // ---------------------------------------------------------


         float browseTime = Random.Range(3f, 8f); // This configuration might move later
         Debug.Log($"{customerAI.gameObject.name}: Browse for {browseTime} seconds.");

         yield return new WaitForSeconds(browseTime); // Wait for the Browse duration


         // --- Simulate Shopping Call ---
         Debug.Log($"{customerAI.gameObject.name}: Finished Browse time. Simulating shopping now.");
         bool boughtItemsFromThisShelf = false;

         // Access the Shopper component and the inventory via customerAI.CurrentTargetLocation
         if (customerAI.Shopper != null && customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.inventory != null)
         {
              boughtItemsFromThisShelf = customerAI.Shopper.SimulateShopping(customerAI.CurrentTargetLocation.Value.inventory);
         }
         else
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Cannot simulate shopping. Shopper or Inventory reference is null!", this);
              boughtItemsFromThisShelf = false;
         }

         if (boughtItemsFromThisShelf)
          {
          // If items were bought from this shelf, reset the counter
          customerAI.Shopper.ResetConsecutiveShelvesCount();
          }
          else
          {
          // If no items were bought from this shelf, increment the counter
          customerAI.Shopper.IncrementConsecutiveShelvesCount(); // <-- USE THE NEW METHOD
          }

          // --- Check if the customer is impatient due to not finding items ---
          // Access the counter via customerAI.Shopper
          if (customerAI.Shopper != null && customerAI.Shopper.GetConsecutiveShelvesCount() >= 3) // <-- Access directly or add a getter if preferred
          {
          Debug.Log($"{customerAI.gameObject.name}: Visited 3 consecutive shelves without finding items. Exiting.", this);
          customerAI.SetState(CustomerState.Exiting); // Trigger the exit
          yield break; // Exit the coroutine immediately
          }
         // -----------------------------


         // --- Decide Next Step After Browse and Shopping ---
         // Access Shopper properties via customerAI.Shopper
          bool finishedShoppingTrip = customerAI.Shopper != null && (customerAI.Shopper.HasItems || customerAI.Shopper.DistinctItemCount >= Random.Range(customerAI.Shopper.MinItemsToBuy, customerAI.Shopper.MaxItemsToBuy + 1));

          if (finishedShoppingTrip)
          {
          Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Finished shopping trip. Total quantity to buy: {customerAI.Shopper?.TotalQuantityToBuy}. Distinct items: {customerAI.Shopper?.DistinctItemCount}.", customerAI.gameObject);

          // --- Check if the register is occupied ---
          if (customerAI.Manager != null && customerAI.Manager.IsRegisterOccupied())
          {
               Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Register is occupied. Attempting to join queue.");
               // Try to join the queue via CustomerManager
               if (customerAI.Manager.TryJoinQueue(customerAI, out Transform assignedSpot, out int spotIndex))
               {
                    // Successfully joined the queue
                    customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null }; // Store the assigned queue spot as target
                    customerAI.AssignedQueueSpotIndex = spotIndex; // Store the assigned spot index
                    customerAI.SetState(CustomerState.Queue); // Transition to the Queue state
               }
               else
               {
                    // Queue is full, cannot join
                    Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Register is occupied and queue is full! Exiting empty-handed (fallback).");
                    // Fallback: Exit the store empty-handed if cannot join queue
                    customerAI.SetState(CustomerState.Exiting); // Transition to Exiting state
               }
          }
          else
          {
               // Register is not occupied, proceed directly to the register
               Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Register is free. Moving to register.");
               // The existing logic to find the register point and set state happens in MovingToRegisterLogic.OnEnter
               // We just need to transition to that state.
               customerAI.SetState(CustomerState.MovingToRegister); // Transition to MovingToRegister
          }
          }
          else // Not finished shopping trip
          {
               Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Shopping not complete, looking for next Browse location.");
               
               // Get a new browse location from CustomerManager via customerAI.Manager
              BrowseLocation? nextBrowseLocation = customerAI.Manager?.GetRandomBrowseLocation();

               if (nextBrowseLocation.HasValue && nextBrowseLocation.Value.browsePoint != null)
               {
                    customerAI.CurrentTargetLocation = nextBrowseLocation; // Update the target location on AI
                    customerAI.SetState(CustomerState.Entering); // Transition to entering state to move to the new browse point
               }
               else // No more Browse locations available
               {
                    Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No more Browse locations available or valid!");
                    // If they have items but can't browse more AND register is occupied, try queue
                    if (customerAI.Shopper != null && customerAI.Shopper.HasItems && customerAI.Manager != null && customerAI.Manager.IsRegisterOccupied())
                    {
                         Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse, register is occupied, attempting to join queue with items.");
                         // Try to join the queue as above
                         if (customerAI.Manager.TryJoinQueue(customerAI, out Transform assignedSpot, out int spotIndex))
                         {
                              customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null };
                              customerAI.AssignedQueueSpotIndex = spotIndex;
                              customerAI.SetState(CustomerState.Queue);
                         }
                         else
                         {
                              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No more places to browse, register occupied, queue full! Exiting with items (fallback).");
                              // Fallback: Exit even with items if queue is full
                              customerAI.SetState(CustomerState.Exiting);
                         }
                    }
                    else if (customerAI.Shopper != null && customerAI.Shopper.HasItems)
                    {
                         // No more places to browse, register is free, go to register with items
                         Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse and has items. Heading to register.");
                         customerAI.SetState(CustomerState.MovingToRegister);
                    }
                    else
                    {
                         // No more places to browse and no items
                         Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse and no items collected. Exiting.");
                         customerAI.SetState(CustomerState.Exiting); // Exit empty-handed
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