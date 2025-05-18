using UnityEngine;
using System; // Needed for Action and Tuple
using System.Collections.Generic;
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Interaction; // Needed for InteractionResponse types and CashRegisterInteractable
using Systems.Inventory; // Needed for ItemDetails (for data)
using Systems.Economy; // Needed for EconomyManager

namespace Systems.Minigame // Your Minigame namespace
{
    /// <summary>
    /// Central manager for coordinating different minigame types.
    /// Listens to state changes and activates the appropriate minigame component and its UI.
    /// </summary>
    public class MinigameManager : MonoBehaviour
    {
        public static MinigameManager Instance { get; private set; }

        [Header("Minigame Configurations")]
        [Tooltip("List of configurations for different minigame types.")]
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

        private IMinigame currentActiveMinigameLogic; // Tracks the currently running minigame LOGIC component
        private GameObject currentActiveMinigameUIRoot; // Tracks the currently active minigame UI ROOT GameObject

        private Dictionary<MinigameType, MinigameConfig> minigameConfigMap;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("MinigameManager: Duplicate instance found. Destroying this one.", this); return; }
            Debug.Log("MinigameManager: Awake completed.");

            minigameConfigMap = new Dictionary<MinigameType, MinigameConfig>();
            if (minigameConfigs != null)
            {
                foreach (var config in minigameConfigs)
                {
                    // Ensure both logic and UI root GameObjects are assigned and valid
                    if (config.minigameLogicGameObject != null && config.minigameUIRootGameObject != null)
                    {
                         if (!minigameConfigMap.ContainsKey(config.type))
                         {
                              minigameConfigMap.Add(config.type, config);
                              // Ensure the logic GameObject and UI root are initially inactive
                              config.minigameLogicGameObject.SetActive(false);
                              config.minigameUIRootGameObject.SetActive(false);
                         }
                         else
                         {
                              Debug.LogWarning($"MinigameManager: Duplicate MinigameConfig found for type {config.type}. Using the first one.", config.minigameLogicGameObject);
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
            MenuManager.OnStateChanged += HandleGameStateChanged;
            Debug.Log("MinigameManager: Subscribed to MenuManager.OnStateChanged.");
        }

        private void OnDisable()
        {
            MenuManager.OnStateChanged -= HandleGameStateChanged;
            Debug.Log("MinigameManager: Unsubscribed from MenuManager.OnStateChanged.");

            // Ensure any active minigame is ended and deactivated if the manager is disabled
            if (currentActiveMinigameLogic != null)
            {
                 currentActiveMinigameLogic.End();
                 // Deactivate both the UI root and the logic GameObject
                 if (currentActiveMinigameUIRoot != null) currentActiveMinigameUIRoot.SetActive(false);
                 if (currentActiveMinigameLogic is MonoBehaviour activeMono) activeMono.gameObject.SetActive(false);

                 currentActiveMinigameLogic.OnMinigameCompleted -= HandleMinigameCompleted; // Unsubscribe
                 currentActiveMinigameLogic = null;
                 currentActiveMinigameUIRoot = null;
                 Debug.Log("MinigameManager: Active minigame and UI deactivated due to manager being disabled.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Ensure event handlers are removed from any active minigame if manager is destroyed
             if (currentActiveMinigameLogic != null)
             {
                 currentActiveMinigameLogic.OnMinigameCompleted -= HandleMinigameCompleted;
             }
        }

        /// <summary>
        /// Event handler for MenuManager.OnStateChanged.
        /// Manages the activation, starting, and ending of minigame components and their UI.
        /// </summary>
        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
             Debug.Log($"MinigameManager: Handling state change from {oldState} to {newState}.");

            // --- Handle Exiting the Minigame State ---
            if (oldState == MenuManager.GameState.InMinigame)
            {
                 Debug.Log("MinigameManager: Exiting InMinigame state.");
                 // End and deactivate the currently active minigame logic and UI
                 if (currentActiveMinigameLogic != null)
                 {
                     currentActiveMinigameLogic.End(); // Call the End method for cleanup

                     // Deactivate the Logic GameObject
                     if (currentActiveMinigameLogic is MonoBehaviour exitingMono)
                     {
                         exitingMono.gameObject.SetActive(false);
                         Debug.Log($"MinigameManager: Deactivated minigame Logic GameObject: {exitingMono.gameObject.name}");
                     }
                     else Debug.LogWarning("MinigameManager: Active minigame logic is not a MonoBehaviour. Cannot deactivate Logic GameObject.");

                     // Deactivate the UI Root GameObject
                     if (currentActiveMinigameUIRoot != null)
                     {
                         currentActiveMinigameUIRoot.SetActive(false);
                         Debug.Log($"MinigameManager: Deactivated minigame UI Root GameObject: {currentActiveMinigameUIRoot.name}");
                     }
                     else Debug.LogWarning("MinigameManager: No active minigame UI Root found to deactivate.");


                     currentActiveMinigameLogic.OnMinigameCompleted -= HandleMinigameCompleted; // Unsubscribe from completion
                     currentActiveMinigameLogic = null; // Clear the references
                     currentActiveMinigameUIRoot = null;
                     Debug.Log("MinigameManager: Current active minigame ended and references cleared.");
                 }
                 else
                 {
                      Debug.LogWarning("MinigameManager: Exiting InMinigame state but no active minigame found.");
                 }
            }


            // --- Handle Entering the Minigame State ---
            if (newState == MenuManager.GameState.InMinigame)
            {
                 Debug.Log("MinigameManager: Entering InMinigame state.");
                 // Determine which minigame to start based on the response

                 IMinigame minigameLogicToStart = null;
                 GameObject minigameUIRootToActivate = null;
                 object startData = null; // Data to pass to the minigame's SetupAndStart method

                 if (response is StartMinigameResponse startMinigameResponse)
                 {
                     Debug.Log($"MinigameManager: Received StartMinigameResponse for type: {startMinigameResponse.Type}.");

                     // --- Find the correct minigame config based on the Type ---
                     if (minigameConfigMap.TryGetValue(startMinigameResponse.Type, out var config))
                     {
                          // Get the logic component from the assigned GameObject
                          if (config.minigameLogicGameObject != null)
                          {
                               minigameLogicToStart = config.minigameLogicGameObject.GetComponent<IMinigame>();
                               // Get the UI Root GameObject from the assigned field
                               minigameUIRootToActivate = config.minigameUIRootGameObject;


                               if (minigameLogicToStart != null && minigameUIRootToActivate != null)
                               {
                                    Debug.Log($"MinigameManager: Identified {config.type} minigame logic and UI root.");
                                    // Prepare the specific start data based on the minigame type
                                    switch (config.type)
                                    {
                                        case MinigameType.BarcodeScanning:
                                             startData = new BarcodeMinigameStartData(startMinigameResponse.ItemsToScan, startMinigameResponse.CashRegisterInteractable);
                                            break;
                                        // Add cases for other minigame types and their specific data structs
                                        // case MinigameType.Lockpicking:
                                        //      startData = new LockpickingMinigameStartData(...); // Define this struct/class
                                        //     break;
                                        // ...
                                        default:
                                            Debug.LogWarning($"MinigameManager: Unhandled MinigameType during data preparation for type: {config.type}.", this);
                                            break;
                                    }
                               }
                               else
                               {
                                    // Log specific errors if IMinigame component is missing or UI Root is null
                                    if (minigameLogicToStart == null) Debug.LogError($"MinigameManager: Assigned Logic GameObject '{config.minigameLogicGameObject.name}' does not have an IMinigame component for type {config.type}!", config.minigameLogicGameObject);
                                    if (minigameUIRootToActivate == null) Debug.LogError($"MinigameManager: Assigned UI Root GameObject is null in config for type {config.type}!", this);

                                    minigameLogicToStart = null; // Prevent starting if logic or UI is missing
                                    minigameUIRootToActivate = null;
                               }
                          }
                           else
                           {
                                Debug.LogError($"MinigameManager: Minigame Logic GameObject is null in config for type {config.type}!", this);
                           }
                     }
                     else
                     {
                          Debug.LogWarning($"MinigameManager: No Minigame Config found for type: {startMinigameResponse.Type}. No minigame will start.", this);
                     }
                     // ---------------------------------------------------------------------

                 }
                 // Add checks for other potential minigame-starting response types if they exist
                 // else if (response is StartSomeOtherMinigameResponse otherResponse) { ... }
                 else
                 {
                      Debug.LogWarning("MinigameManager: Entered InMinigame state but the response was not a StartMinigameResponse.", this);
                 }


                 // --- If a minigame logic component and UI root were found, activate and start it ---
                 if (minigameLogicToStart != null && minigameUIRootToActivate != null)
                 {
                     if (currentActiveMinigameLogic != null)
                     {
                          Debug.LogWarning("MinigameManager: Entering InMinigame state, but there is already an active minigame! Ending the previous one.", this);
                          // Clean up the previous one before starting a new one
                          currentActiveMinigameLogic.End();
                           if (currentActiveMinigameLogic is MonoBehaviour alreadyActiveMono) alreadyActiveMono.gameObject.SetActive(false);
                           if (currentActiveMinigameUIRoot != null) currentActiveMinigameUIRoot.SetActive(false); // Deactivate old UI
                           currentActiveMinigameLogic.OnMinigameCompleted -= HandleMinigameCompleted;
                     }

                     currentActiveMinigameLogic = minigameLogicToStart; // Set the new active logic component
                     currentActiveMinigameUIRoot = minigameUIRootToActivate; // Set the new active UI root

                     // Activate the GameObject hosting the minigame component
                      if (currentActiveMinigameLogic is MonoBehaviour enteringMono)
                      {
                          enteringMono.gameObject.SetActive(true);
                          Debug.Log($"MinigameManager: Activated minigame Logic GameObject: {enteringMono.gameObject.name}");
                      }
                      else Debug.LogWarning("MinigameManager: Minigame logic to start is not a MonoBehaviour. Cannot activate Logic GameObject.");

                     // Activate the UI Root GameObject for this minigame
                     currentActiveMinigameUIRoot.SetActive(true);
                     Debug.Log($"MinigameManager: Activated minigame UI Root GameObject: {currentActiveMinigameUIRoot.name}");


                     currentActiveMinigameLogic.OnMinigameCompleted += HandleMinigameCompleted; // Subscribe to completion
                     Debug.Log("MinigameManager: Subscribed to active minigame's completion event.");


                     // Call the minigame's SetupAndStart method with the prepared data
                     currentActiveMinigameLogic.SetupAndStart(startData);
                     Debug.Log("MinigameManager: Called SetupAndStart on the active minigame logic component.");

                 }
                 else
                 {
                      Debug.LogWarning("MinigameManager: No valid IMinigame logic component or UI root was selected/found to start for this state entry.");
                 }
            }
        }

        /// <summary>
        /// Handles the OnMinigameCompleted event from the currently active minigame.
        /// Processes completion data and transitions the game state.
        /// </summary>
        /// <param name="completionData">Data provided by the completed minigame.</param>
        private void HandleMinigameCompleted(object completionData)
        {
             Debug.Log("MinigameManager: Received OnMinigameCompleted event from active minigame logic.");

             // --- Process Completion Data (Specific to each minigame type) ---
             // Check the type of the active minigame logic component to process the data
             if (currentActiveMinigameLogic is BarcodeMinigame completedBarcodeMinigame)
             {
                 Debug.Log("MinigameManager: Completed minigame was Barcode Minigame.");
                 // Expect completionData to be a Tuple<float, CashRegisterInteractable>
                 if (completionData is Tuple<float, CashRegisterInteractable> barcodeCompletionData)
                 {
                      float totalPayment = barcodeCompletionData.Item1;
                      CashRegisterInteractable initiatingRegister = barcodeCompletionData.Item2;

                      Debug.Log($"MinigameManager: Received total payment: {totalPayment}");

                      // Process Payment (Add to Player's Currency)
                      if (EconomyManager.Instance != null)
                      {
                          EconomyManager.Instance.AddCurrency(totalPayment);
                          Debug.Log($"MinigameManager: Added {totalPayment} currency via EconomyManager.");
                      }
                      else Debug.LogError("MinigameManager: EconomyManager Instance is null! Cannot process payment.");

                      // Notify the Initiating Register
                      if (initiatingRegister != null)
                      {
                           initiatingRegister.OnMinigameCompleted(totalPayment); // Call the completion method on the register
                           Debug.Log("MinigameManager: Notified initiating register of completion.");
                      }
                      else Debug.LogWarning("MinigameManager: Initiating register reference is null in completion data! Cannot notify completion.");
                 }
                  else Debug.LogWarning($"MinigameManager: Barcode Minigame completion data was not the expected Tuple<float, CashRegisterInteractable>. Received type: {completionData?.GetType().Name ?? "null"}.");

             }
             // Add cases for other minigame types and how to process their completion data:
             // else if (currentActiveMinigameLogic is LockpickingMinigame completedLockpickingMinigame)
             // {
             //     Debug.Log("MinigameManager: Completed minigame was Lockpicking Minigame.");
             //     // Process Lockpicking specific completion data (e.g., bool success)
             //     if (completionData is bool success) { ... }
             // }
             // ...
             else
             {
                  Debug.LogWarning($"MinigameManager: Completed minigame logic component type ({currentActiveMinigameLogic?.GetType().Name ?? "null"}) is not recognized. Cannot process completion data.");
             }


             // --- Transition Game State ---
             // After processing completion, always transition back to the Playing state.
             Debug.Log("MinigameManager: Requesting state transition back to Playing.");
             if (MenuManager.Instance != null)
             {
                 MenuManager.Instance.SetState(MenuManager.GameState.Playing, null); // Exit minigame state
             }
             else Debug.LogError("MinigameManager: MenuManager Instance is null! Cannot exit minigame state after completion.");

             // The state exit handling in HandleGameStateChanged will now clean up the minigame component and UI.
        }


        // Public methods if needed by StateActions to interact with the CURRENTLY active minigame logic component
        // public void CallMethodOnActiveMinigame(string methodName, object parameter = null) { ... }
    }
}