using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections.Generic;
using Utils.Pooling;
using CustomerManagement; // Required for CustomerManager and BrowseLocation
using System.Collections; // Required for Coroutines
using Systems.Inventory; // Required for Inventory and ItemDetails
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Linq; // Required for LINQ methods like Where, ToList, Sum


namespace Game.NPC // Your NPC namespace
{
    /// <summary>
    /// Defines the possible states for a customer NPC.
    /// </summary>
    public enum CustomerState
    {
        Inactive,          // In the pool, not active in the scene
        Initializing,      // Brief state after activation, before entering store
        Entering,          // Moving from spawn point into the store
        Browse,          // Moving between/simulating Browse at shelves
        MovingToRegister,  // Moving towards the cash register
        WaitingAtRegister, // Waiting for the player at the register
        TransactionActive, // Player is scanning items (minigame)
        Exiting,           // Moving towards an exit point
        ReturningToPool    // Signalling completion and waiting to be returned
    }

    /// <summary>
    /// Manages the behavior and movement of a customer NPC.
    /// Handles shopping logic and interactions.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))] // Ensure the GameObject has a NavMeshAgent
    public class CustomerAI : MonoBehaviour
    {
        // --- Components ---
        private NavMeshAgent navMeshAgent;

        // --- State ---
        private CustomerState currentState = CustomerState.Inactive;
        private float stateEntryTime;
        private Coroutine stateCoroutine; // Coroutine for managing timed states (like Browse) or rotation


        // --- References (Provided by CustomerManager during Initialize) ---
        private CustomerManager customerManager; // Reference to the manager to signal completion

        // --- Cached References ---
        // Cache the CashRegisterInteractable reference once found
        private CashRegisterInteractable cachedCashRegister;


        // --- Internal Data (Managed by AI script) ---
        private BrowseLocation? currentTargetLocation = null;
        private const float DestinationReachedThreshold = 0.5f;
        private float BrowseTime = 0f;
        [SerializeField] private float rotationSpeed = 5f;

        // --- Shopping Data (Phase 2) ---
        private List<(ItemDetails details, int quantity)> itemsToBuy = new List<(ItemDetails, int)>();
        [SerializeField] private int minItemsToBuy = 1;
        [SerializeField] private int maxItemsToBuy = 3;
        [SerializeField] private int minQuantityPerItem = 1;
        [SerializeField] private int maxQuantityPerItem = 5;
        // -------------------------------


        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent component not found!", this);
                enabled = false;
            }

             Debug.Log($"CustomerAI ({gameObject.name}): Awake completed.");
        }

        /// <summary>
        /// Initializes the NPC when it's retrieved from the pool.
        /// Should be called by the CustomerManager AFTER the GameObject is active.
        /// </summary>
        /// <param name="manager">The CustomerManager instance managing this NPC.</param>
        /// <param name="startPosition">The initial position for the NPC.</param>
        public void Initialize(CustomerManager manager, Vector3 startPosition)
        {
            this.customerManager = manager;
            ResetNPC();

            if (navMeshAgent != null)
            {
                 navMeshAgent.enabled = true;

                 if (navMeshAgent.Warp(startPosition))
                 {
                      Debug.Log($"CustomerAI ({gameObject.name}): Warped to {startPosition}.");
                      navMeshAgent.ResetPath();
                      navMeshAgent.isStopped = true;
                 }
                 else
                 {
                      Debug.LogWarning($"CustomerAI ({gameObject.name}): Failed to Warp to {startPosition}. Is the position on the NavMesh?", this);
                      SetState(CustomerState.ReturningToPool);
                      return;
                 }
            }
             else
             {
                 Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent is null during Initialize!", this);
                 SetState(CustomerState.ReturningToPool);
                 return;
             }

            SetState(CustomerState.Initializing);

            Debug.Log($"CustomerAI ({gameObject.name}): Initialized at {startPosition}.");
        }

         /// <summary>
         /// Resets the NPC's state and data when initialized from the pool.
         /// Cleans up properties from prior use.
         /// </summary>
         private void ResetNPC()
         {
             currentState = CustomerState.Inactive;
             stateEntryTime = 0f;
             StopStateCoroutine();

             if (navMeshAgent != null)
             {
                  if (navMeshAgent.isActiveAndEnabled)
                  {
                    navMeshAgent.ResetPath();
                    navMeshAgent.isStopped = true;
                  }
                   navMeshAgent.enabled = false;
             }
             currentTargetLocation = null;

             itemsToBuy.Clear();
             cachedCashRegister = null; // Clear cached register reference
         }

        /// <summary>
        /// Stops any currently running state coroutine.
        /// </summary>
        private void StopStateCoroutine()
        {
            if (stateCoroutine != null)
            {
                StopCoroutine(stateCoroutine);
                stateCoroutine = null;
            }
        }


        private void Update()
        {
             bool agentActiveAndEnabled = navMeshAgent != null && navMeshAgent.isActiveAndEnabled;

            switch (currentState)
            {
                case CustomerState.Initializing:
                    HandleInitializingState(agentActiveAndEnabled);
                    break;
                case CustomerState.Entering:
                    if (agentActiveAndEnabled) HandleEnteringState();
                    break;
                case CustomerState.MovingToRegister:
                     if (agentActiveAndEnabled) HandleMovingToRegisterState();
                    break;
                case CustomerState.TransactionActive:
                    // Handled by external calls and state changes
                    break; // Added break
                case CustomerState.Exiting:
                     if (agentActiveAndEnabled) HandleExitingState();
                    break;
                case CustomerState.ReturningToPool:
                    HandleReturningToPoolState();
                    break;
                case CustomerState.Inactive:
                    if (navMeshAgent != null && navMeshAgent.enabled) navMeshAgent.enabled = false;
                    break;
            }
            // States handled by coroutines don't need Update logic here:
            // case CustomerState.Browse:
            // case CustomerState.WaitingAtRegister:
        }

        /// <summary>
        /// Sets the NPC's current state and performs any state entry logic.
        /// Manages starting/stopping state coroutines and setting destinations.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        protected void SetState(CustomerState newState)
        {
            Debug.Log($"CustomerAI ({gameObject.name}): <color=yellow>Transitioning from {currentState} to {newState}</color>", this); // Highlight state changes

            StopStateCoroutine();

            currentState = newState;
            stateEntryTime = Time.time;

            bool agentActiveAndEnabled = navMeshAgent != null && navMeshAgent.isActiveAndEnabled;

            if (agentActiveAndEnabled)
            {
                 navMeshAgent.isStopped = false;
            }


            switch (currentState)
            {
                 case CustomerState.Initializing:
                      if (agentActiveAndEnabled)
                      {
                           navMeshAgent.isStopped = true;
                           stateCoroutine = StartCoroutine(InitializeRoutine());
                      }
                      else
                      {
                           Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent not ready for Initializing state entry!", this);
                           SetState(CustomerState.ReturningToPool);
                      }
                     break;

                 case CustomerState.Entering:
                      if (agentActiveAndEnabled)
                      {
                          currentTargetLocation = customerManager?.GetRandomBrowseLocation();
                           if (currentTargetLocation.HasValue && currentTargetLocation.Value.browsePoint != null)
                           {
                                navMeshAgent.SetDestination(currentTargetLocation.Value.browsePoint.position);
                                navMeshAgent.isStopped = false;
                           }
                           else
                           {
                                Debug.LogWarning($"CustomerAI ({gameObject.name}): No Browse locations available for Entering state! Exiting empty-handed.");
                                SetState(CustomerState.Exiting); // Exit if no destination
                           }
                      }
                      else
                      {
                          Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent not ready for Entering state entry!", this);
                          SetState(CustomerState.ReturningToPool);
                      }
                     break;

                 case CustomerState.Browse:
                       if (agentActiveAndEnabled && currentTargetLocation.HasValue && currentTargetLocation.Value.browsePoint != null)
                       {
                           navMeshAgent.isStopped = true;
                           navMeshAgent.ResetPath();
                           stateCoroutine = StartCoroutine(BrowseRoutine(currentTargetLocation.Value.browsePoint.rotation));
                       }
                       else
                       {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready or no target for Browse state entry! Exiting.", this);
                           SetState(CustomerState.Exiting);
                       }
                     break;

                 case CustomerState.MovingToRegister:
                      if (agentActiveAndEnabled)
                      {
                          Transform registerTarget = customerManager?.GetRegisterPoint();
                           if (registerTarget != null)
                          {
                                // Store the register Transform in a BrowseLocation struct
                                // Inventory is null as it's not a shopping location
                                currentTargetLocation = new BrowseLocation { browsePoint = registerTarget, inventory = null };

                                navMeshAgent.SetDestination(registerTarget.position);
                                navMeshAgent.isStopped = false;
                          }
                          else
                          {
                                Debug.LogWarning($"CustomerAI ({gameObject.name}): No register point assigned! Exiting.", this);
                                SetState(CustomerState.Exiting);
                          }
                      }
                       else
                      {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready for MovingToRegister state entry! Exiting.", this);
                           SetState(CustomerState.Exiting);
                      }
                     break;

                 case CustomerState.WaitingAtRegister:
                      // Reached the register point, stop and rotate towards it
                       if (agentActiveAndEnabled && currentTargetLocation.HasValue && currentTargetLocation.Value.browsePoint != null)
                      {
                           navMeshAgent.isStopped = true;
                           navMeshAgent.ResetPath();

                           // --- NEW: Signal arrival to the CashRegisterInteractable ---
                           if (cachedCashRegister == null)
                           {
                                // Find the cash register by tag if not cached
                                GameObject registerGO = GameObject.FindGameObjectWithTag("CashRegister"); // Make sure your register GO has this tag!
                                if (registerGO != null)
                                {
                                    cachedCashRegister = registerGO.GetComponent<CashRegisterInteractable>();
                                }
                           }

                           if (cachedCashRegister != null)
                           {
                                Debug.Log($"CustomerAI ({gameObject.name}): Notifying CashRegister '{cachedCashRegister.gameObject.name}' of arrival.", this);
                                cachedCashRegister.CustomerArrived(this); // Call the new method
                                // Start the waiting and rotation routine
                                stateCoroutine = StartCoroutine(WaitingAtRegisterRoutine(currentTargetLocation.Value.browsePoint.rotation)); // Rotate to the register's rotation
                           }
                           else
                           {
                                Debug.LogError($"CustomerAI ({gameObject.name}): Could not find CashRegisterInteractable by tag 'CashRegister'! Cannot complete transaction flow.", this);
                                // Cannot wait at register, exit instead
                                SetState(CustomerState.Exiting);
                           }
                           // --------------------------------------------------------
                      }
                       else // Should have reached from MovingToRegister
                       {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready or no target for WaitingAtRegister state entry! Exiting.", this);
                           SetState(CustomerState.Exiting);
                       }
                     break;

                 case CustomerState.TransactionActive:
                       if (agentActiveAndEnabled)
                       {
                           navMeshAgent.isStopped = true;
                           navMeshAgent.ResetPath();
                       }
                      break;

                 case CustomerState.Exiting:
                    cachedCashRegister.CustomerDeparted();
                      if (agentActiveAndEnabled)
                      {
                          Transform exitTarget = customerManager?.GetRandomExitPoint();
                           if (exitTarget != null)
                           {
                                // Store the exit Transform
                                currentTargetLocation = new BrowseLocation { browsePoint = exitTarget, inventory = null };
                                Debug.Log($"CustomerAI ({gameObject.name}): Setting exit destination to {exitTarget.position}.", this); // Log exit destination
                                navMeshAgent.SetDestination(exitTarget.position);
                                navMeshAgent.isStopped = false;
                           }
                           else
                           {
                                Debug.LogWarning($"CustomerAI ({gameObject.name}): No exit points available for Exiting state! Returning to pool.", this);
                                SetState(CustomerState.ReturningToPool);
                           }
                      }
                       else
                      {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready for Exiting state entry! Returning to pool.", this);
                           SetState(CustomerState.ReturningToPool);
                      }
                     break;

                 case CustomerState.ReturningToPool:
                      Debug.Log($"CustomerAI ({gameObject.name}): Entering ReturningToPool state.", this); // Log entering this state
                      if (navMeshAgent != null)
                      {
                           navMeshAgent.ResetPath();
                           navMeshAgent.isStopped = true;
                           navMeshAgent.enabled = false;
                      }
                      // The actual return call happens in HandleReturningToPoolState,
                      // which is called from Update when in this state.
                      break;

                 case CustomerState.Inactive:
                      if (navMeshAgent != null)
                      {
                           navMeshAgent.enabled = false;
                           navMeshAgent.isStopped = true;
                           navMeshAgent.ResetPath();
                      }
                      currentTargetLocation = null;
                      itemsToBuy.Clear();
                      cachedCashRegister = null; // Clear cached register reference on becoming inactive
                      break;
            }
        }

        // --- State Handling Coroutines ---

        private IEnumerator InitializeRoutine()
        {
             yield return null; // Wait one frame
             SetState(CustomerState.Entering);
        }

private IEnumerator BrowseRoutine(Quaternion targetRotation)
         {
             yield return StartCoroutine(RotateTowardsTargetRoutine(targetRotation));

             BrowseTime = Random.Range(3f, 8f);
             Debug.Log($"CustomerAI ({gameObject.name}): Browsing for {BrowseTime} seconds.");

             // --- REMOVED: SimulateShopping() call from here ---
             // SimulateShopping();
             // -------------------------------------------------

             yield return new WaitForSeconds(BrowseTime); // Wait for the Browse duration

             // --- MOVED: SimulateShopping() call to AFTER the wait ---
             Debug.Log($"CustomerAI ({gameObject.name}): Finished Browse time. Simulating shopping now.");
             SimulateShopping(); // Perform shopping logic AFTER the wait
             // -----------------------------------------------------


             // --- Decide Next Step After Browse and Shopping ---
             bool finishedShoppingTrip = itemsToBuy.Count > 0 || itemsToBuy.Count >= Random.Range(minItemsToBuy, maxItemsToBuy + 1); // Decide if done based on items bought or randomly

             if (finishedShoppingTrip)
             {
                  Debug.Log($"CustomerAI ({gameObject.name}): Finished shopping trip. Total items to buy: {itemsToBuy.Sum(item => item.quantity)}.");
                  SetState(CustomerState.MovingToRegister); // Done shopping, go to register
             }
             else
             {
                 // Not done shopping, move to another Browse location
                 Debug.Log($"CustomerAI ({gameObject.name}): Shopping not complete, looking for next Browse location.");

                 currentTargetLocation = customerManager?.GetRandomBrowseLocation();
                  if (currentTargetLocation.HasValue && currentTargetLocation.Value.browsePoint != null)
                  {
                      SetState(CustomerState.MovingToRegister); // Transition to moving state to set new destination (MovingToRegister will now target the new browse point)
                  }
                  else // No more Browse locations available
                  {
                      Debug.LogWarning($"CustomerAI ({gameObject.name}): No more Browse locations available or valid!");
                      if (itemsToBuy.Count > 0)
                      {
                          Debug.Log($"CustomerAI ({gameObject.name}): No more places to browse, heading to register with {itemsToBuy.Count} items.");
                          SetState(CustomerState.MovingToRegister); // Go to register with items collected so far
                      }
                      else
                      {
                           Debug.Log($"CustomerAI ({gameObject.name}): No more places to browse and no items collected. Exiting.");
                           SetState(CustomerState.Exiting); // Exit empty-handed
                      }
                  }
             }
         }

         private IEnumerator WaitingAtRegisterRoutine(Quaternion targetRotation) // This parameter might become redundant
         {
             // --- Rotate towards the target point's (register point) facing direction ---
             // Use the rotation of the browsePoint stored in currentTargetLocation
             if (currentTargetLocation.HasValue && currentTargetLocation.Value.browsePoint != null)
             {
                 yield return StartCoroutine(RotateTowardsTargetRoutine(currentTargetLocation.Value.browsePoint.rotation));
             }
             else // Fallback if target is somehow null (shouldn't happen)
             {
                  Debug.LogWarning($"CustomerAI ({gameObject.name}): No valid target location stored for WaitingAtRegister rotation!", this);
                  // Optionally rotate to face the CashRegister's forward direction as a less ideal fallback
                  // if (cachedCashRegister != null) yield return StartCoroutine(RotateTowardsTargetRoutine(cachedCashRegister.transform.rotation));
             }
             // ---------------------------------------------------------

             Debug.Log($"CustomerAI ({gameObject.name}): Waiting at register.");

             // Stay in this state until the state changes externally
             while(currentState == CustomerState.WaitingAtRegister)
             {
                 yield return null;
             }

             Debug.Log($"CustomerAI ({gameObject.name}): No longer waiting at register.");
         }


         // --- Smooth Rotation Coroutine ---
         private IEnumerator RotateTowardsTargetRoutine(Quaternion targetRotation)
         {
              Debug.Log($"CustomerAI ({gameObject.name}): Starting rotation towards {targetRotation.eulerAngles}.");
              if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
              {
                   navMeshAgent.isStopped = true;
                   navMeshAgent.ResetPath();
              }

              Quaternion startRotation = transform.rotation;
              float angleDifference = Quaternion.Angle(startRotation, targetRotation);
              if (angleDifference < 0.1f)
              {
                   Debug.Log($"CustomerAI ({gameObject.name}): Already facing target direction.");
                   yield break;
              }

              float duration = angleDifference / (rotationSpeed * 100f);
               if (duration < 0.2f) duration = 0.2f;

              float timeElapsed = 0f;

              while (timeElapsed < duration)
              {
                  transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed / duration);
                  timeElapsed += Time.deltaTime;
                  yield return null;
              }

              transform.rotation = targetRotation;
              Debug.Log($"CustomerAI ({gameObject.name}): Rotation complete.");
         }


         // --- Shopping Logic Implementation ---
         private void SimulateShopping()
         {
              if (!currentTargetLocation.HasValue || currentTargetLocation.Value.inventory == null)
              {
                  Debug.LogWarning($"CustomerAI ({gameObject.name}): Cannot simulate shopping. Not at a valid Browse location with an inventory.", this);
                  return;
              }

              Inventory currentInventory = currentTargetLocation.Value.inventory;
              Debug.Log($"CustomerAI ({gameObject.name}): Simulating shopping at '{currentTargetLocation.Value.browsePoint?.name}' using inventory '{currentInventory.gameObject.name}'.");

              Item[] availableItems = currentInventory.InventoryState.GetCurrentArrayState();

              List<ItemDetails> availableOtcItemDetails = availableItems
                 .Where(item => item != null && item.details != null && item.quantity > 0 && item.details.isOverTheCounter)
                 .Select(item => item.details)
                 .Distinct()
                 .ToList();

              Debug.Log($"CustomerAI ({gameObject.name}): Found {availableOtcItemDetails.Count} distinct available OTC item types in this inventory.");

              int numItemTypesToSelect = Random.Range(minItemsToBuy - itemsToBuy.Count, maxItemsToBuy - itemsToBuy.Count + 1);
              numItemTypesToSelect = Mathf.Clamp(numItemTypesToSelect, 0, availableOtcItemDetails.Count);
              numItemTypesToSelect = Mathf.Max(0, numItemTypesToSelect);

              if (numItemTypesToSelect <= 0)
              {
                  Debug.Log($"CustomerAI ({gameObject.name}): No new item types selected from this location.");
                  return; // Don't proceed if no items are selected
              }

              List<ItemDetails> selectedItemTypes = availableOtcItemDetails.OrderBy(x => Random.value).Take(numItemTypesToSelect).ToList();

              foreach(var itemDetails in selectedItemTypes)
              {
                   int desiredQuantity = Random.Range(minQuantityPerItem, maxQuantityPerItem + 1);
                   Debug.Log($"CustomerAI ({gameObject.name}): Trying to buy {desiredQuantity} of {itemDetails.Name}.");

                   Combiner inventoryCombiner = currentInventory.GetComponent<Combiner>();

                   if (inventoryCombiner != null)
                   {
                       int actualQuantityRemoved = inventoryCombiner.TryRemoveQuantity(itemDetails, desiredQuantity);

                       if (actualQuantityRemoved > 0)
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Successfully bought {actualQuantityRemoved} of {itemDetails.Name}.");
                            var existingPurchase = itemsToBuy.FirstOrDefault(item => item.details == itemDetails);

                            if (existingPurchase.details != null) // Item already in list
                            {
                                 itemsToBuy.Remove(existingPurchase);
                                 itemsToBuy.Add((itemDetails, existingPurchase.quantity + actualQuantityRemoved));
                            }
                            else // First time buying this item type
                            {
                                itemsToBuy.Add((itemDetails, actualQuantityRemoved));
                            }
                       }
                       else
                       {
                            Debug.Log($"CustomerAI ({gameObject.name}): Could not buy {desiredQuantity} of {itemDetails.Name} (none available or remove failed).");
                       }
                   }
                    else
                   {
                       Debug.LogError($"CustomerAI ({gameObject.name}): Inventory '{currentInventory.gameObject.name}' is missing Combiner component! Cannot simulate shopping.", currentInventory);
                   }
              }
              Debug.Log($"CustomerAI ({gameObject.name}): Finished shopping simulation at '{currentTargetLocation.Value.browsePoint?.name}'. Items collected so far: {itemsToBuy.Count} distinct types.");
         }


        private void HandleInitializingState(bool agentActiveAndEnabled)
        {
             // Logic is in InitializeRoutine coroutine.
        }


        private void HandleEnteringState()
        {
             if (HasReachedDestination())
             {
                 SetState(CustomerState.Browse);
             }
        }

        private void HandleMovingToRegisterState()
        {
             if (HasReachedDestination())
             {
                 SetState(CustomerState.WaitingAtRegister);
             }
        }

        private void HandleTransactionActiveState()
        {
             // NPC is passive during scanning.
             // State transition is handled by external call to OnTransactionCompleted().
        }

        private void HandleExitingState()
        {
             // Move towards an exit point.
             // Check if destination is reached:
             if (HasReachedDestination())
             {
                 Debug.Log($"CustomerAI ({gameObject.name}): Reached exit destination.", this); // Log reaching exit
                 SetState(CustomerState.ReturningToPool); // Transition to returning
             }
        }

        private void HandleReturningToPoolState()
        {
             // Signal the manager and become inactive.
             // This state handles the transition and cleanup in SetState.
             // We call the manager here just once.
             Debug.Log($"CustomerAI ({gameObject.name}): Handling ReturningToPool state, calling manager.", this); // Log calling the handler
             if (customerManager != null)
             {
                 customerManager.ReturnCustomerToPool(this.gameObject);
                 // The ReturnCustomerToPool method deactivates the GameObject,
                 // so this script's Update will stop running.
                 // No need to call SetState(CustomerState.Inactive) here.
             }
             else
             {
                 Debug.LogError($"CustomerAI ({gameObject.name}): CustomerManager reference is null! Cannot return to pool. Destroying instead.", this);
                 Destroy(this.gameObject);
             }
        }

        /// <summary>
        /// Helper to check if the NavMeshAgent has reached its current destination.
        /// Accounts for path pending, remaining distance, and stopping.
        /// </summary>
        private bool HasReachedDestination()
        {
            if (navMeshAgent == null || !navMeshAgent.enabled || navMeshAgent.pathPending)
            {
                return false;
            }

            bool isCloseEnough = navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + DestinationReachedThreshold;

            if (navMeshAgent.hasPath && isCloseEnough)
            {
                 if (navMeshAgent.velocity.sqrMagnitude < 0.1f * 0.1f)
                 {
                      return true;
                 }
            }

             if (!navMeshAgent.hasPath && navMeshAgent.velocity.sqrMagnitude == 0f)
             {
                 return true;
             }

            return false;
        }


         // --- Public methods for external systems to call ---

         /// <summary>
         /// Called by the CashRegister to initiate the transaction minigame.
         /// </summary>
         // Removed the itemsToScan parameter here as the NPC already holds it internally
         public void StartTransaction()
         {
              // CashRegisterInteractable gets the items from GetItemsToBuy() when player interacts.
              // This method just signals the NPC to enter the transaction state.
              SetState(CustomerState.TransactionActive);
              Debug.Log($"CustomerAI ({gameObject.name}): Transaction started.");
         }

         /// <summary>
         /// Called by the CashRegister/Minigame system when the transaction is completed.
         /// </summary>
         /// <param name="paymentReceived">The amount of money the player received.</param>
         public void OnTransactionCompleted(float paymentReceived)
         {
              Debug.Log($"CustomerAI ({gameObject.name}): Transaction completed. Player received {paymentReceived} money.");
              // Maybe play a happy animation/sound
              SetState(CustomerState.Exiting); // Move to exit
         }

         /// <summary>
         /// Public getter for the list of items the customer intends to buy.
         /// Called by the CashRegister system in Phase 3.
         /// </summary>
         /// <returns>A list of (ItemDetails, quantity) pairs.</returns>
         public List<(ItemDetails details, int quantity)> GetItemsToBuy()
         {
             return itemsToBuy; // Return the collected list
         }

         // --- Optional: OnDrawGizmos for debugging paths ---
         // private void OnDrawGizmos() { ... }
    }
}