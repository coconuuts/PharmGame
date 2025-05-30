using UnityEngine;
using System.Collections;
using System;
using CustomerManagement; // Ensure this is present
using Game.NPC;
using Game.Events;
using Game.NPC.States; // Ensure this is present
using Random = UnityEngine.Random;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerWaitingAtRegisterState", menuName = "NPC/Customer States/Waiting At Register", order = 5)]
    public class CustomerWaitingAtRegisterStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.WaitingAtRegister;

        [Header("Waiting Settings")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(10f, 15f);

        private float impatientTimer;
        private float impatientDuration;

        private Coroutine waitingRoutine;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            context.MovementHandler?.StopMoving();

             // Cache the register reference here:
            if (context.RegisterCached == null) // <-- Use the new property
            {
                GameObject registerGO = GameObject.FindGameObjectWithTag("CashRegister");
                if (registerGO != null)
                {
                    context.CacheCashRegister(registerGO.GetComponent<CashRegisterInteractable>()); // This calls the context method, which sets Runner.CachedCashRegister
                }
            }

            if (context.RegisterCached != null) // <-- Use the new property
            {
                Debug.Log($"{context.NpcObject.name}: Notifying CashRegister '{context.RegisterCached.gameObject.name}' of arrival.", context.NpcObject); // <-- Use the new property
                context.RegisterCached.CustomerArrived(context.Runner); // <-- Use the new property
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: Could not find CashRegisterInteractable by tag 'CashRegister' and it wasn't cached! Cannot complete transaction flow.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Fallback if register not found
                return;
            }

            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y);
            impatientTimer = 0f;
            Debug.Log($"{context.NpcObject.name}: Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);

            waitingRoutine = context.StartCoroutine(WaitingRoutine(context));
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);

            impatientTimer += context.DeltaTime; // <-- MODIFIED: Use context.DeltaTime

            if (impatientTimer >= impatientDuration)
            {
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in WaitingAtRegister state after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.WaitingAtRegister));
            }
        }

        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);
            impatientTimer = 0f;
        }

        // Coroutine method
        private IEnumerator WaitingRoutine(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: WaitingRoutine started in {name}.", context.NpcObject);

            if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null)
            {
                 context.RotateTowardsTarget(context.CurrentTargetLocation.Value.browsePoint.rotation);
                 yield return new WaitForSeconds(0.5f);
            }
            else
            {
                 Debug.LogWarning($"{context.NpcObject.name}: No valid target location stored for WaitingAtRegister rotation or MovementHandler missing!", context.NpcObject);
            }

            Debug.Log($"{context.NpcObject.name}: Waiting at register for player interaction.", context.NpcObject);

            while (true)
            {
                 if (context.Runner.GetCurrentState() != this) yield break;
                 yield return null;
            }
        }
    }
}