// Systems/Minigame/MinigameManager.cs
using UnityEngine;
using System; // Needed for Action and Tuple
using System.Collections.Generic;
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Interaction; // Needed for InteractionResponse types and StartMinigameResponse
using Systems.Inventory; // Needed for ItemDetails (for data)
using Systems.Economy; // Needed for EconomyManager

namespace Systems.Minigame // Your Minigame namespace
{
    /// <summary>
    /// Central manager for coordinating different general minigame types (non-crafting).
    /// Listens to state changes and activates the appropriate minigame component and its UI.
    /// Delegates completion processing to a separate handler.
    /// Delegates start information retrieval to a separate handler.
    /// Handles aborting minigames via external request (e.g. MenuManager).
    /// </summary>
    public class MinigameManager : MonoBehaviour
    {
        public static MinigameManager Instance { get; private set; }

        [Header("Minigame Configurations")]
        [Tooltip("List of configurations for different general minigame types.")]
        [SerializeField] private List<MinigameConfig> minigameConfigs; // Using a list of configurations

        [System.Serializable] // Make this struct visible in the Inspector
        public struct MinigameConfig
        {
            public MinigameType type;
            [Tooltip("The GameObject containing the IMinigame script component (Optional, can be the same as UI Root).")]
            public GameObject minigameLogicGameObject; // The GameObject with the script component
            [Tooltip("The root GameObject of the minigame's UI hierarchy.")]
            public GameObject minigameUIRootGameObject; // The root of the UI visuals (your 'BarcodeGame' object)
        }

        private IMinigame currentActiveMinigameLogic; // Tracks the currently running general minigame LOGIC component
        private GameObject currentActiveMinigameUIRoot; // Tracks the currently active general minigame UI ROOT GameObject

        private Dictionary<MinigameType, MinigameConfig> minigameConfigMap;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("MinigameManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; } // Destroy duplicate
            Debug.Log("MinigameManager: Awake completed.");

            minigameConfigMap = new Dictionary<MinigameType, MinigameConfig>();
            if (minigameConfigs != null)
            {
                foreach (var config in minigameConfigs)
                {
                    // Ensure both logic and UI root GameObjects are assigned and valid
                    if (config.minigameLogicGameObject != null && config.minigameUIRootGameObject != null)
                    {
                         IMinigame logicComponent = config.minigameLogicGameObject.GetComponent<IMinigame>();
                         if (logicComponent == null)
                         {
                              Debug.LogError($"MinigameManager: Minigame Logic GameObject for type {config.type} is missing the IMinigame component!", config.minigameLogicGameObject);
                              continue; // Skip this config if missing component
                         }

                         if (!minigameConfigMap.ContainsKey(config.type))
                         {
                             minigameConfigMap.Add(config.type, config);
                             // Ensure the logic GameObject and UI root are initially inactive
                             config.minigameLogicGameObject.SetActive(false);
                             config.minigameUIRootGameObject.SetActive(false);
                         }
                         else
                         {
                              Debug.LogWarning($"MinigameManager: Duplicate MinigameConfig found for type {config.type}. Using the first one.", this);
                         }
                    }
                    else
                    {
                        Debug.LogWarning($"MinigameManager: Minigame Config for type {config.type} has null Logic GameObject or UI Root GameObject assigned! This minigame will not be functional.", this);
                    }
                }
            }
            else
            {
                Debug.LogError("MinigameManager: Minigame Configs list is not assigned in the Inspector! No minigames can be managed.", this);
                enabled = false; // Disable if configs are missing
            }
        }

        private void OnEnable()
        {
            // MinigameManager does NOT subscribe to MenuManager.OnStateChanged.
            // MenuManager CALLS MinigameManager methods (StartMinigame, EndCurrentMinigame)
            // based on its state changes and interaction responses.
        }

        private void OnDisable()
        {
            // Ensure any active minigame is ended and deactivated if the manager is disabled
            if (currentActiveMinigameLogic != null)
            {
                 // Call End with true because the manager itself is disabling/shutting down
                 EndCurrentMinigame(true); // Call the public method
                 Debug.Log("MinigameManager: Active minigame and UI deactivated due to manager being disabled.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Ensure event handlers are removed from any active minigame if manager is destroyed
             if (currentActiveMinigameLogic != null)
             {
                 // Defensive unsubscribe, EndCurrentMinigame also unsubscribes
                 currentActiveMinigameLogic.OnMinigameCompleted -= HandleMinigameCompleted;
             }
        }

        /// <summary>
        /// Called by MenuManager to start a general minigame based on an InteractionResponse.
        /// Finds the appropriate minigame config, activates it, and calls SetupAndStart.
        /// </summary>
        /// <param name="response">The StartMinigameResponse containing minigame type and data.</param>
        public bool StartMinigame(StartMinigameResponse response)
        {
             if (response == null)
             {
                 Debug.LogError("MinigameManager: StartMinigame called with null response.", this);
                 return false;
             }

             MinigameType minigameType = response.Type;

             // End the previous minigame using the designated method
             if (currentActiveMinigameLogic != null)
             {
                 Debug.LogWarning($"MinigameManager: Attempted to start minigame type {minigameType} but a minigame is already active ({currentActiveMinigameLogic.GetType().Name}). Ending previous one.", this);
                 EndCurrentMinigame(true); // End the previous one, mark as aborted
             }

             if (!minigameConfigMap.TryGetValue(minigameType, out MinigameConfig config))
             {
                 Debug.LogError($"MinigameManager: No minigame config found for type {minigameType}.", this);
                 return false;
             }

             // Get the IMinigame component from the configured GameObject
             IMinigame minigameLogic = config.minigameLogicGameObject.GetComponent<IMinigame>();
             if (minigameLogic == null)
             {
                 Debug.LogError($"MinigameManager: Configured GameObject for minigame type {minigameType} is missing the IMinigame component!", config.minigameLogicGameObject);
                 return false;
             }

             // Set the new active references BEFORE activating/starting
             currentActiveMinigameLogic = minigameLogic;
             currentActiveMinigameUIRoot = config.minigameUIRootGameObject;

             // Activate the GameObjects
             if (currentActiveMinigameLogic is MonoBehaviour activeMono)
             {
                 activeMono.gameObject.SetActive(true);
                 Debug.Log($"MinigameManager: Activated minigame Logic GameObject: {activeMono.gameObject.name}");
             }
             else
             {
                 Debug.LogError("MinigameManager: Active minigame logic is not a MonoBehaviour. Cannot activate Logic GameObject!", this);
                 EndCurrentMinigame(true); // Abort if cannot activate
                 return false;
             }
             currentActiveMinigameUIRoot.SetActive(true);

             // Subscribe to its completion event
             currentActiveMinigameLogic.OnMinigameCompleted += HandleMinigameCompleted;
             Debug.Log("MinigameManager: Subscribed to active minigame's completion event.");


             // Notify MenuManager about the state change, passing camera data from the response
             if (MenuManager.Instance != null)
             {
                 // Re-use the incoming response as it contains the necessary data for MenuManager's SetState/SetCameraModeAction
                 MenuManager.Instance.SetState(MenuManager.GameState.InMinigame, response);
                 Debug.Log($"MinigameManager: Notified MenuManager to enter InMinigame state with camera data from response.");
             }
             else
             {
                 Debug.LogError("MinigameManager: MenuManager.Instance is null! Cannot set game state.", this);
                 // Cannot proceed without MenuManager state change, abort the minigame launch
                 EndCurrentMinigame(true); // Mark as aborted due to manager failure
                 return false;
             }

             // Call the minigame's SetupAndStart method with the data from the response
             currentActiveMinigameLogic.SetupAndStart(response); // Pass the response directly
             Debug.Log("MinigameManager: Called SetupAndStart on the active minigame logic component.");

             return true; // Successfully initiated
        }


        /// <summary>
        /// Handles the OnMinigameCompleted event from the currently active minigame.
        /// Delegates completion data processing and transitions the game state back to Playing.
        /// Handles boolean success/failure data.
        /// --- MODIFIED: Removed cleanup logic, now handled by EndCurrentMinigame ---
        /// --- MODIFIED: Calls EndCurrentMinigame(false) ONLY if currentActiveMinigameLogic is still valid ---
        /// </summary>
        /// <param name="completionData">A Tuple: (bool success, object data).</param>
        private void HandleMinigameCompleted(object completionData)
        {
            // Safely extract boolean status and data from the Tuple
            bool minigameWasSuccessful = false;
            object minigameResultData = null;

            if (completionData is Tuple<bool, object> resultTuple)
            {
                 minigameWasSuccessful = resultTuple.Item1;
                 minigameResultData = resultTuple.Item2; // This is the actual data payload (e.g., payment)
            }
            else
            {
                 Debug.LogError($"MinigameManager: Received completion data in unexpected format. Expected Tuple<bool, object>.", this);
                 // Assume failure if data is malformed
            }
             Debug.Log($"MinigameManager: Received OnMinigameCompleted event from active minigame logic. Outcome: {(minigameWasSuccessful ? "Success" : "Failure/Aborted")}. Data: {minigameResultData}.", this);

            // Delegate Processing Completion Data ONLY if successful
            if (minigameWasSuccessful)
            {
                 Debug.Log($"MinigameManager: Delegating completion processing for successful outcome.");
                 MinigameCompletionProcessor.ProcessCompletion(true, minigameResultData); // Pass success status and the data payload
            }
            else
            {
                 Debug.Log($"MinigameManager: Minigame outcome was failure/abort. Not delegating completion processing to MinigameCompletionProcessor.");
                 // Optionally still pass failure/abort data if processor handles it
                 MinigameCompletionProcessor.ProcessCompletion(false, minigameResultData);
            }


            // --- MODIFIED: Request EndCurrentMinigame(false) here for natural completion ---
            // This ensures cleanup happens via the designated method for natural completions too.
            // The 'wasAborted' flag is false because this event came from the minigame finishing its logic.
            // Add a null check for currentActiveMinigameLogic before calling EndCurrentMinigame(false),
            // as EndCurrentMinigame might have already been called by MenuManager exit actions (e.g., during Escape).
             if (currentActiveMinigameLogic != null)
             {
                  Debug.Log("MinigameManager: Requesting EndCurrentMinigame(false) for natural completion cleanup.");
                  // Pass false for wasAborted as this is a natural completion
                  EndCurrentMinigame(false); // Call the cleanup method
             }
             else
             {
                  // This case happens if MenuManager.SetState(GameState.Playing) was called *before* this event handler completed,
                  // and that state change triggered MinigameManager.EndCurrentMinigame(true) via MenuManager's exit actions.
                  Debug.Log("MinigameManager: currentActiveMinigameLogic is null when HandleMinigameCompleted finished. Cleanup was likely handled by MenuManager exit actions (e.g., Escape).");
             }
            // -----------------------------------------------------------------------------

            // Transition Game State back to Playing
            // Only transition back if MenuManager is *still* in the InMinigame state.
            // This check is crucial to avoid fighting MenuManager during Escape sequence.
            if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.InMinigame)
            {
                Debug.Log("MinigameManager: Minigame session ended and state is still InMinigame. Requesting state transition back to Playing.");
                MenuManager.Instance.SetState(MenuManager.GameState.Playing, null); // Exit minigame state
            }
            else
            {
                 Debug.Log($"MinigameManager: Minigame session ended, but MenuManager is already in state {MenuManager.Instance?.currentState}. Not forcing return to Playing.");
            }
        }


        /// <summary>
        /// Ends the current active general minigame, if any.
        /// This might be called externally (e.g., by MenuManager during an emergency exit like Escape)
        /// or internally (by HandleMinigameCompleted after a natural finish).
        /// Performs cleanup and clears references.
        /// Accepts wasAborted parameter.
        /// --- MODIFIED: Unsubscribes from the event BEFORE calling minigame.End() ---
        /// </summary>
        /// <param name="wasAborted">True if the minigame is being ended prematurely (e.g., by Escape).</param>
        public void EndCurrentMinigame(bool wasAborted)
        {
             if (currentActiveMinigameLogic != null)
             {
                  Debug.Log($"MinigameManager: Ending current active general minigame session. Aborted: {wasAborted}.", this);

                  // --- MODIFIED: Unsubscribe from the minigame's event FIRST to break the recursive loop ---
                  currentActiveMinigameLogic.OnMinigameCompleted -= HandleMinigameCompleted;
                  Debug.Log("MinigameManager: Unsubscribed from active minigame's completion event.");
                  // --------------------------------------------------------------------------------------

                  // Now call the minigame's End method. This will trigger its internal cleanup
                  // and invoke its OnMinigameCompleted event *again*, but our handler is unsubscribed.
                  currentActiveMinigameLogic.End(wasAborted);


                  // Deactivate the Logic GameObject
                  if (currentActiveMinigameLogic is MonoBehaviour completedMono)
                  {
                      completedMono.gameObject.SetActive(false);
                      Debug.Log($"MinigameManager: Deactivated minigame Logic GameObject: {completedMono.gameObject.name}");
                  }
                  else
                  {
                       // This warning might indicate a design issue if IMinigame implementations aren't MonoBehaviours
                       Debug.LogWarning("MinigameManager: Active minigame logic is not a MonoBehaviour. Cannot deactivate Logic GameObject directly.");
                  }


                  // Deactivate the UI Root GameObject
                  if (currentActiveMinigameUIRoot != null)
                  {
                      currentActiveMinigameUIRoot.SetActive(false);
                      Debug.Log($"MinigameManager: Deactivated minigame UI Root GameObject: {currentActiveMinigameUIRoot.name}");
                  }
                  else Debug.LogWarning("MinigameManager: No active minigame UI Root found to deactivate.");

                  // Optional: Destroy the GameObject if it was dynamically created (add logic to track this if needed)
                  // if (wasDynamicallyCreated && completedMono != null) Destroy(completedMono.gameObject);

                  // Clear the references after cleanup
                  currentActiveMinigameLogic = null;
                  currentActiveMinigameUIRoot = null;
                  Debug.Log("MinigameManager: Current active minigame references cleared.");
             }
             else
             {
                 Debug.LogWarning("MinigameManager: Attempted to end general minigame, but none was active.", this);
             }
        }

         // Placeholder for MinigameCompletionProcessor
         public static class MinigameCompletionProcessor
         {
             /// <summary>
             /// Processes the outcome of a general minigame.
             /// </summary>
             /// <param name="success">True if the minigame was successful, false if it failed or was aborted.</param>
             /// <param name="dataPayload">Optional data from the minigame (e.g., payment amount for BarcodeScanning).</param>
             public static void ProcessCompletion(bool success, object dataPayload)
             {
                 Debug.Log($"MinigameCompletionProcessor: Received completion. Success: {success}. Data Payload: {dataPayload}. (Implement specific processing here!)");

                 // Example: If successful and dataPayload is a Tuple<float, CashRegisterInteractable> from BarcodeMinigame
                 if (success && dataPayload is Tuple<float, CashRegisterInteractable> barcodeResult)
                 {
                     float earnedCash = barcodeResult.Item1;
                     CashRegisterInteractable register = barcodeResult.Item2;

                     Debug.Log($"Processing Barcode Scan completion: Earned {earnedCash} cash.");

                     // Add cash using EconomyManager (assuming it's a singleton)
                     if (EconomyManager.Instance != null)
                     {
                         EconomyManager.Instance.AddCurrency(earnedCash);
                         Debug.Log($"Added {earnedCash} cash via EconomyManager.");
                     }
                     else
                     {
                          Debug.LogError("MinigameCompletionProcessor: EconomyManager.Instance is null! Cannot add cash.");
                     }

                     // Notify the initiating register (if needed, e.g., to display a success message)
                     if (register != null)
                     {
                          // Assuming CashRegisterInteractable has a method like OnMinigameEnded
                          // Or the register itself might subscribe to the MinigameManager's event?
                          // Let's assume it has a method to be called after processing.
                          register.OnMinigameCompleted(earnedCash); // Assuming this method exists on CashRegisterInteractable
                          Debug.Log("MinigameCompletionProcessor: Notified initiating Cash Register of completion.");
                     }
                     else
                     {
                          Debug.LogWarning("MinigameCompletionProcessor: Initiating CashRegisterInteractable is null. Cannot notify.");
                     }
                 }
                 else if (!success)
                 {
                     // Handle general minigame failure/abort if needed (e.g., display a generic failure message)
                      Debug.Log("MinigameCompletionProcessor: General minigame failed or was aborted. No specific success processing.");
                      // If failure has specific data to pass, check dataPayload type here.
                      // Example: if (dataPayload is FailureReason reason) { ... }
                 }
                 // Add else if checks for other MinigameTypes and their specific data payloads
             }
         }
    }
}