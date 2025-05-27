using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States; 
using Random = UnityEngine.Random;

namespace Game.NPC.States
{
     [CreateAssetMenu(fileName = "CustomerBrowseState", menuName = "NPC/Customer States/Browse", order = 3)]
     public class CustomerBrowseStateSO : NpcStateSO
     {
          public override System.Enum HandledState => CustomerState.Browse;

          [Header("Browse Settings")]
          [SerializeField] private Vector2 browseTimeRange = new Vector2(3f, 8f);

          private Coroutine browseCoroutine;

          public override void OnEnter(NpcStateContext context)
          {
               base.OnEnter(context);

               context.MovementHandler?.StopMoving();

               // Start the state coroutine via context and store the reference
               browseCoroutine = context.StartCoroutine(BrowseRoutine(context));
          }

          // OnUpdate remains empty or base call
          // OnReachedDestination is not applicable

          public override void OnExit(NpcStateContext context)
          {
               base.OnExit(context);
          }

          // Coroutine method
          private IEnumerator BrowseRoutine(NpcStateContext context)
          {
               if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null)
               {
                    context.RotateTowardsTarget(context.CurrentTargetLocation.Value.browsePoint.rotation);
                    yield return new WaitForSeconds(0.5f);
               }
               else
               {
                    Debug.LogWarning($"{context.NpcObject.name}: No valid target location stored for Browse rotation or MovementHandler missing!", context.NpcObject);
                    context.TransitionToState(CustomerState.Exiting);
                    yield break;
               }

               float browseTime = Random.Range(browseTimeRange.x, browseTimeRange.y);
               Debug.Log($"{context.NpcObject.name}: Browse for {browseTime:F2} seconds.", context.NpcObject);
               yield return new WaitForSeconds(browseTime);

               Debug.Log($"{context.NpcObject.name}: Finished Browse time. Simulating shopping now.", context.NpcObject);
               bool boughtItemsFromThisShelf = false;
               if (context.Shopper != null && context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.inventory != null)
               { boughtItemsFromThisShelf = context.Shopper.SimulateShopping(context.CurrentTargetLocation.Value.inventory); }
               else { Debug.LogWarning($"{context.NpcObject.name}: Cannot simulate shopping. Shopper or Inventory reference is null!", context.NpcObject); }

               if (boughtItemsFromThisShelf) context.Shopper?.ResetConsecutiveShelvesCount();
               else context.Shopper?.IncrementConsecutiveShelvesCount();

               if (context.Shopper != null && context.Shopper.GetConsecutiveShelvesCount() >= 3)
               {
                    Debug.Log($"{context.NpcObject.name}: Visited 3 consecutive shelves without finding items. Exiting.", context.NpcObject);
                    context.TransitionToState(CustomerState.Exiting);
                    yield break;
               }

               bool finishedShoppingTrip = context.Shopper != null && (context.Shopper.HasItems || context.Shopper.DistinctItemCount >= Random.Range(context.Shopper.MinItemsToBuy, context.Shopper.MaxItemsToBuy + 1));

               if (finishedShoppingTrip)
               {
                    if (context.IsRegisterOccupied())
                    {
                         // Register is occupied, attempt to join queue.
                         // If successful, the Manager will signal arrival *later* when this NPC reaches the front.
                         Debug.Log($"{context.NpcObject.name}: Shopping complete, Register is occupied. Attempting to join queue.", context.NpcObject);
                         Transform assignedSpot;
                         int spotIndex;
                         if (context.TryJoinQueue(context.Runner, out assignedSpot, out spotIndex))
                         {
                              Debug.Log($"{context.NpcObject.name}: TryJoinQueue succeeded! Assigned spot index {spotIndex} at position {assignedSpot.position}. Transitioning to Queue.", context.NpcObject);
                              context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null };
                              context.TransitionToState(CustomerState.Queue);
                         }
                         else
                         {
                              Debug.LogWarning($"{context.NpcObject.name}: Register is occupied and queue is full! Exiting.", context.NpcObject);
                              context.TransitionToState(CustomerState.Exiting);
                         }
                    }
                    else
                    {
                         // Register is FREE, move to register directly.
                         Debug.Log($"{context.NpcObject.name}: Shopping complete, Register is free. Claiming spot and moving to register.", context.NpcObject);
                         // --- NEW: Claim the register spot immediately before moving ---
                         context.SignalCustomerAtRegister(); // <-- Call the signal here!
                                                             // -----------------------------------------------------------
                         context.TransitionToState(CustomerState.MovingToRegister); // Transition to move
                    }
               }
               else // Not finished shopping trip
               {
                    Debug.Log($"{context.NpcObject.name}: Shopping not complete, looking for next Browse location.", context.NpcObject);
                    BrowseLocation? nextBrowseLocation = context.GetRandomBrowseLocation();
                    if (nextBrowseLocation.HasValue && nextBrowseLocation.Value.browsePoint != null)
                    {
                         context.Runner.CurrentTargetLocation = nextBrowseLocation;
                         context.TransitionToState(CustomerState.Entering);
                    }
                    else
                    {
                         Debug.LogWarning($"{context.NpcObject.name}: No more Browse locations available or valid!", context.NpcObject);
                         if (context.Shopper != null && context.Shopper.HasItems && context.Manager != null && context.Manager.IsRegisterOccupied())
                         {
                              Debug.Log("... has items, register occupied, attempting to join queue.", context.NpcObject);
                              Transform assignedSpot; int spotIndex;
                              // --- FIX 2: Pass context.Runner instead of GetComponent<CustomerAI>() ---
                              if (context.TryJoinQueue(context.Runner, out assignedSpot, out spotIndex))
                              {
                                   context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null };
                                   context.TransitionToState(CustomerState.Queue);
                              }
                              else
                              {
                                   Debug.LogWarning("... has items, register occupied, queue full! Exiting.", context.NpcObject);
                                   context.TransitionToState(CustomerState.Exiting);
                              }
                         }
                         else if (context.Shopper != null && context.Shopper.HasItems)
                         {
                              Debug.Log("... has items. Heading to register.", context.NpcObject);
                              context.TransitionToState(CustomerState.MovingToRegister);
                         }
                         else
                         {
                              Debug.Log("... no items. Exiting.", context.NpcObject);
                              context.TransitionToState(CustomerState.Exiting);
                         }
                    }
               }
               Debug.Log($"{context.NpcObject.name}: BrowseRoutine finished.", context.NpcObject);
          }
     }
}