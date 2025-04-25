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
         // Access the Shopper component and the inventory via customerAI.CurrentTargetLocation
         if (customerAI.Shopper != null && customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.inventory != null)
         {
              customerAI.Shopper.SimulateShopping(customerAI.CurrentTargetLocation.Value.inventory);
         }
         else
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Cannot simulate shopping. Shopper or Inventory reference is null!", this);
         }
         // -----------------------------


         // --- Decide Next Step After Browse and Shopping ---
         // Access Shopper properties via customerAI.Shopper
         bool finishedShoppingTrip = customerAI.Shopper != null && (customerAI.Shopper.HasItems || customerAI.Shopper.DistinctItemCount >= Random.Range(customerAI.Shopper.MinItemsToBuy, customerAI.Shopper.MaxItemsToBuy + 1));

         if (finishedShoppingTrip)
         {
              Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Finished shopping trip. Total quantity to buy: {customerAI.Shopper?.TotalQuantityToBuy}. Distinct items: {customerAI.Shopper?.DistinctItemCount}.", this);
              customerAI.SetState(CustomerState.MovingToRegister); // Done shopping, go to register
         }
         else
         {
              // Not done shopping, move to another Browse location
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
                   if (customerAI.Shopper != null && customerAI.Shopper.HasItems)
                   {
                        Debug.Log($"CustomerAI ({customerAI.gameObject.name}): No more places to browse, heading to register with {customerAI.Shopper.DistinctItemCount} items.");
                        customerAI.SetState(CustomerState.MovingToRegister); // Go to register with items collected so far
                   }
                   else
                   {
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