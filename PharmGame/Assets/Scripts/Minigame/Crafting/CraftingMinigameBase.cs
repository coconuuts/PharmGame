// --- START OF FILE CraftingMinigameBase.cs ---

// Systems/CraftingMinigames/CraftingMinigameBase.cs
using UnityEngine;
using System;
using Systems.Inventory;
using System.Collections.Generic; // Needed for Dictionary // <-- Added using directive

namespace Systems.CraftingMinigames
{
    /// <summary>
    /// Abstract base class for all crafting minigames, providing a reusable state machine.
    /// Inherit from this class for specific crafting minigame implementations.
    /// </summary>
    public abstract class CraftingMinigameBase : MonoBehaviour, ICraftingMinigame
    {
        public enum MinigameState
        {
            None,
            Beginning,
            Middle,
            End
        }

        [SerializeField] protected MinigameState currentMinigameState = MinigameState.None;

        protected CraftingRecipe craftingRecipe;
        protected int craftBatches;
        protected Dictionary<string, object> minigameParameters; // <-- NEW: Field to store parameters
        public bool minigameSuccessStatus = false; // Field to track success status, accessed by EndMinigame

        // --- NEW: Field to store the actual crafted amount from the minigame ---
        protected int actualCraftedAmount = 0;
        // --- END NEW ---


        protected Transform _initialCameraTarget;
        protected float _initialCameraDuration;


        public Transform InitialCameraTarget => _initialCameraTarget;
        public float InitialCameraDuration => _initialCameraDuration;


        /// <summary>
        /// Event triggered when a crafting minigame session is completed or aborted.
        /// --- MODIFIED: Now passes boolean success status AND the actual crafted amount. ---
        /// </summary>
        /// <remarks>
        /// The parameters are:
        /// bool: true if the minigame reached its natural end sequence, false if aborted or critical failure.
        /// int: The actual number of units/amount successfully crafted by the minigame (e.g., pill count).
        /// The central CraftingMinigameManager will subscribe to this event.
        /// </remarks>
        public event Action<bool, int> OnCraftingMinigameCompleted; // <-- MODIFIED Signature

        // Marked as virtual in Phase 1 to allow override in derived class
        /// <summary>
        /// Sets up and starts the crafting minigame with recipe details, batches, and additional parameters.
        /// --- MODIFIED: Added parameters dictionary ---
        /// </summary>
        /// <param name="recipe">The CraftingRecipe being crafted.</param>
        /// <param name="batches">The number of batches being crafted.</param>
        /// <param name="parameters">A dictionary of additional parameters for minigame setup (e.g., target count from prescription).</param>
        public virtual void SetupAndStart(CraftingRecipe recipe, int batches, Dictionary<string, object> parameters)
        {
            if (recipe == null)
            {
                 Debug.LogError($"CraftingMinigameBase: SetupAndStart called with null recipe.", this);
                 minigameSuccessStatus = false; // Critical failure
                 actualCraftedAmount = 0; // Ensure 0 on failure
                 EndMinigame(false); // End with failure status
                 return;
            }

            craftingRecipe = recipe;
            craftBatches = batches;
            minigameParameters = parameters ?? new Dictionary<string, object>(); // <-- Store parameters, handle null
            minigameSuccessStatus = false; // Initialize success status to false on start
            actualCraftedAmount = 0; // Initialize actual crafted amount
            Debug.Log($"CraftingMinigameBase: Setup and Start for recipe '{recipe.recipeName}' x {batches} batches.", this);

            // Derived class MUST set _initialCameraTarget and _initialCameraDuration
            // in its override of this method or in its own Awake/Start.

            SetMinigameState(MinigameState.Beginning);
        }
        // --- END MODIFIED ---

        /// <summary>
        /// Implementation from ICraftingMinigame.
        /// Transitions to the End state for cleanup before deactivation.
        /// </summary>
        /// <param name="wasAborted">True if the minigame was aborted (e.g., by player pressing Escape), false if it reached a natural end state.</param>
        public virtual void EndMinigame(bool wasAborted)
        {
            Debug.Log($"CraftingMinigameBase: Ending minigame. Aborted: {wasAborted}.", this);

            // If aborted, the success status is false regardless of internal progress.
            // If not aborted, the success status was set internally by MarkMinigameCompleted (now always true for natural completion)
            // or set to false by a critical error handler.
            bool finalSuccessStatus = wasAborted ? false : minigameSuccessStatus;

            // If aborted, the actual crafted amount should be 0. Otherwise, use the stored amount.
            int finalCraftedAmount = wasAborted ? 0 : actualCraftedAmount;

            PerformCleanup(); // Call cleanup regardless of current internal state

            // Ensure we transition to None state if not already there
            // Note: SetMinigameState(None) still calls the Exit method for the *current* state,
            // which might still be Beginning, Middle, or End. The CleanupLogic is now centralized.
            if (currentMinigameState != MinigameState.None)
            {
                 SetMinigameState(MinigameState.None);
            }

            // Invoke the completion event, passing the final success/abort status AND the actual crafted amount
            OnCraftingMinigameCompleted?.Invoke(finalSuccessStatus, finalCraftedAmount); // <-- MODIFIED Parameters
        }


        // --- State Machine Logic ---
        protected void SetMinigameState(MinigameState newState)
        {
            if (currentMinigameState == newState)
            {
                return;
            }

            MinigameState oldState = currentMinigameState;
            currentMinigameState = newState;
            Debug.Log($"CraftingMinigameBase: Transitioning state from {oldState} to {newState}.", this);

            HandleStateExit(oldState);
            HandleStateEntry(newState);
        }

        private void HandleStateEntry(MinigameState state)
        {
            switch (state)
            {
                case MinigameState.Beginning:
                    OnEnterBeginningState();
                    break;
                case MinigameState.Middle:
                    OnEnterMiddleState();
                    break;
                case MinigameState.End:
                    OnEnterEndState();
                    break;
                case MinigameState.None:
                    OnEnterNoneState(); // Optional None state entry
                    break;
            }
        }

        private void HandleStateExit(MinigameState state)
        {
            switch (state)
            {
                case MinigameState.Beginning:
                    OnExitBeginningState();
                    break;
                case MinigameState.Middle:
                    OnExitMiddleState();
                    break;
                case MinigameState.End:
                    OnExitEndState();
                    break;
                case MinigameState.None:
                     // No specific exit for None
                    break;
            }
        }

         // Optional virtual method for entering the None state
         protected virtual void OnEnterNoneState()
         {
             Debug.Log("CraftingMinigameBase: Entering None state.", this);
             // Base class does no specific work here, derived classes can override for final logic
         }

         /// <summary>
         /// Abstract method for performing specific minigame cleanup (e.g., returning pooled objects).
         /// Called by EndMinigame(bool).
         /// </summary>
         protected abstract void PerformCleanup();


        protected virtual void Update()
        {
            // Optional: Handle state-specific updates in derived classes
        }

        // --- Abstract Methods ---
        protected abstract void OnEnterBeginningState();
        protected abstract void OnExitBeginningState();
        protected abstract void OnEnterMiddleState();
        protected abstract void OnExitMiddleState();
        protected abstract void OnEnterEndState();
        protected abstract void OnExitEndState(); // This method is still called, but cleanup is moved elsewhere


        // --- Completion Handling ---

        /// <summary>
        /// Call this method from derived classes when the minigame logic reaches its natural completion point.
        /// This now always sets the internal success status to true, stores the actual crafted amount,
        /// and triggers the transition to the End state.
        /// </summary>
        /// <param name="actualAmount">The actual amount of units/items successfully crafted by the minigame.</param>
        // --- MODIFIED: Added 'actualAmount' parameter ---
        protected void MarkMinigameCompleted(int actualAmount)
        {
            Debug.Log($"CraftingMinigameBase: Minigame logic reached natural completion point. Marking as success with amount: {actualAmount}.", this);
            this.minigameSuccessStatus = true; // Set the inherited status field to true
            this.actualCraftedAmount = actualAmount; // Store the actual amount
            // --- END MODIFIED ---

            SetMinigameState(MinigameState.End); // Transition to the End state for packaging/final steps
            // The actual OnCraftingMinigameCompleted event is invoked by EndMinigame(false)
            // after the End state logic is processed.
        }
        // --- END MODIFIED ---

         /// <summary>
         /// Call this method from derived classes if a critical, unrecoverable error occurs.
         /// Sets the internal success status to false and triggers the transition to the End state.
         /// </summary>
         protected void MarkMinigameFailedCritically()
         {
              Debug.LogError($"CraftingMinigameBase: Minigame encountered a critical failure.", this);
              this.minigameSuccessStatus = false; // Set to false
              this.actualCraftedAmount = 0; // Ensure 0 on failure
              SetMinigameState(MinigameState.End); // Transition to End state for cleanup
              // EndMinigame(false) will be called by the coroutine or state logic that detected the failure.
         }
    }
}