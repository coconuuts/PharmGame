// --- START OF FILE NPCCoreEvents.cs ---

using UnityEngine; // Required for GameObject
using Game.NPC;

namespace Game.Events // Keep events in their dedicated namespace
{
    // --- General NPC Lifecycle Events ---

    /// <summary>
    /// Published when an NPC's movement handler detects it has reached its destination.
    /// </summary>
    public struct NpcReachedDestinationEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC that reached the destination.

        public NpcReachedDestinationEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }

    /// <summary>
    /// Published when an NPC is transitioning to the state where it intends to return to the pool.
    /// </summary>
    public struct NpcReturningToPoolEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC returning.

        public NpcReturningToPoolEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }

    /// <summary>
    /// Published when an NPC becomes impatient in a waiting or queue state.
    /// </summary>
    public struct NpcImpatientEvent
    {
        public GameObject NpcObject; // The GameObject of the impatient NPC.
        public Game.NPC.CustomerState State; // The state the NPC was in when they became impatient.

        public NpcImpatientEvent(GameObject npcObject, Game.NPC.CustomerState state)
        {
            NpcObject = npcObject;
            State = state;
        }
    }

    // --- NEW: Events for Store Entry/Exit ---
    /// <summary>
    /// Published when an NPC transitions into a state considered "inside the store"
    /// (e.g., Entering state). Used by CustomerManager to track active count.
    /// </summary>
    public struct NpcEnteredStoreEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC entering the store.

        public NpcEnteredStoreEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }

    /// <summary>
    /// Published when an NPC transitions into a state considered "exiting the store"
    /// (e.g., Exiting state). Used by CustomerManager to track active count.
    /// </summary>
    public struct NpcExitedStoreEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC exiting the store.

        public NpcExitedStoreEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }
    // --- END NEW ---


    // --- Cash Register & Transaction Events ---

    /// <summary>
    /// Published by the CashRegisterInteractable when the player starts a transaction with an NPC.
    /// </summary>
    public struct NpcStartedTransactionEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC starting the transaction.

        public NpcStartedTransactionEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }

    /// <summary>
    /// Published by the CashRegisterInteractable (or minigame system) when a transaction is completed.
    /// </summary>
    public struct NpcTransactionCompletedEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC whose transaction completed.
        public float PaymentReceived; // The amount of money the player received.

        public NpcTransactionCompletedEvent(GameObject npcObject, float paymentReceived)
        {
            NpcObject = npcObject;
            PaymentReceived = paymentReceived;
        }
    }

    /// <summary>
    /// Published when the cash register area becomes free (customer departs after transaction/impatience).
    /// This event allows the CustomerManager to know it can send the next person from the queue.
    /// </summary>
    public struct CashRegisterFreeEvent
    {
        // Could optionally include a reference to the register itself if you had multiple
        // public CashRegisterInteractable Register;
    }


    // --- Queue System Events ---

    /// <summary>
    /// Published by a queue logic component (BaseQueueLogic derivative) when an NPC vacates a queue spot.
    /// </summary>
    public struct QueueSpotFreedEvent
    {
        public QueueType Type; // The type of queue (Main or Secondary).
        public int SpotIndex; // The index of the spot that is now free.

        public QueueSpotFreedEvent(QueueType type, int spotIndex)
        {
            Type = type;
            SpotIndex = spotIndex;
        }
    }

    /// <summary>
    /// Published by the CustomerManager when the next customer in the secondary queue is released.
    /// The customer themselves subscribes to this to transition to the Entering state.
    /// </summary>
    public struct ReleaseNpcFromSecondaryQueueEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC being released.

        public ReleaseNpcFromSecondaryQueueEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }

    // --- Future Interruption Events (Placeholder for Goal 4) ---

    /// <summary>
    /// Published when an NPC is attacked.
    /// </summary>
    public struct NpcAttackedEvent
    {
        public GameObject NpcObject;    // The NPC that was attacked.
        public GameObject AttackerObject; // The entity that performed the attack (e.g., player).

        public NpcAttackedEvent(GameObject npcObject, GameObject attackerObject)
        {
            NpcObject = npcObject;
            AttackerObject = attackerObject;
        }
    }

    /// <summary>
    /// Published when an NPC is directly interacted with (e.g., talked to).
    /// </summary>
    public struct NpcInteractedEvent
    {
        public GameObject NpcObject;      // The NPC that was interacted with.
        public GameObject InteractorObject; // The entity that performed the interaction (e.g., player).

        public NpcInteractedEvent(GameObject npcObject, GameObject interactorObject)
        {
            NpcObject = npcObject;
            InteractorObject = interactorObject;
        }
    }
    /// <summary>
    /// Published to trigger an NPC to enter the Emoting state.
    /// Temporary event for Phase 3 testing.
    /// </summary>
    public struct TriggerNpcEmoteEvent
    {
        public GameObject NpcObject; // The GameObject of the NPC to emote.
        // Optional: Could add emote ID, duration, etc.

        public TriggerNpcEmoteEvent(GameObject npcObject)
        {
            NpcObject = npcObject;
        }
    }

    // --- Interruption Completion Events (NEW) ---
    /// <summary>
    /// Published when an NPC's Combat state has ended.
    /// </summary>
    public struct NpcCombatEndedEvent
    {
        public GameObject NpcObject; // The NPC whose combat ended.
        // Optional: Outcome (win/loss/disengage), target, etc.
        public NpcCombatEndedEvent(GameObject npcObject) { NpcObject = npcObject; }
    }

    /// <summary>
    /// Published when an NPC's Social state has ended.
    /// </summary>
    public struct NpcInteractionEndedEvent // Name might change based on interaction system
    {
        public GameObject NpcObject; // The NPC whose interaction ended.
        // Optional: Outcome (success/fail), interactor, etc.
        public NpcInteractionEndedEvent(GameObject npcObject) { NpcObject = npcObject; }
    }

     /// <summary>
     /// Published when an NPC's Emoting state has ended (e.g., animation complete).
     /// Published by the EmotingStateSO's coroutine.
     /// </summary>
     public struct NpcEmoteEndedEvent // Name might change based on emoting system
     {
         public GameObject NpcObject; // The NPC whose emote ended.
         // Optional: Emote ID, etc.
         public NpcEmoteEndedEvent(GameObject npcObject) { NpcObject = npcObject; }
     }

     // Could add other completion events for other interrupt states if needed
}