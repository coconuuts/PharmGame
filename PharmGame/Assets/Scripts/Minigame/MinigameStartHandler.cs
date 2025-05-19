using UnityEngine;
using System; // Needed for Tuple
using System.Collections.Generic;
using Systems.GameStates; // Needed for MinigameType enum
using Systems.Interaction; // Needed for InteractionResponse, StartMinigameResponse, CashRegisterInteractable
using Systems.Inventory; // Needed for ItemDetails (for data)
// We don't need EconomyManager here, only in the completion handler

namespace Systems.Minigame // Ensure this is in the correct namespace
{
    /// <summary>
    /// Static helper class responsible for determining which minigame to start
    /// based on an InteractionResponse and preparing the necessary components and data.
    /// </summary>
    public static class MinigameStartHandler
    {
        /// <summary>
        /// Analyzes the interaction response and attempts to find and prepare
        /// the necessary components and data to start a minigame.
        /// </summary>
        /// <param name="response">The InteractionResponse received from the state change.</param>
        /// <param name="minigameConfigMap">The map of minigame types to their configurations (using the nested struct type).</param>
        /// <returns>A MinigameStartInfo struct containing the logic component, UI root, and start data, or an invalid struct if unable to prepare.</returns>
        // FIX: Change the type here to reference the nested struct correctly
        public static MinigameStartInfo GetStartInfo(InteractionResponse response, Dictionary<MinigameType, MinigameManager.MinigameConfig> minigameConfigMap)
        {
            Debug.Log("MinigameStartHandler: Analyzing response to get start info.");

            IMinigame minigameLogicToStart = null;
            GameObject minigameUIRootToActivate = null;
            object startData = null; // Data to pass to the minigame's SetupAndStart method
            MinigameType minigameType = MinigameType.None; // Default to None

            if (response is StartMinigameResponse startMinigameResponse)
            {
                Debug.Log($"MinigameStartHandler: Received StartMinigameResponse for type: {startMinigameResponse.Type}.");
                minigameType = startMinigameResponse.Type;

                // --- Find the correct minigame config based on the Type ---
                // FIX: Change the out parameter type here as well
                if (minigameConfigMap.TryGetValue(minigameType, out MinigameManager.MinigameConfig config))
                {
                     // Get the logic component from the assigned GameObject
                     if (config.minigameLogicGameObject != null)
                     {
                          minigameLogicToStart = config.minigameLogicGameObject.GetComponent<IMinigame>();
                          // Get the UI Root GameObject from the assigned field
                          minigameUIRootToActivate = config.minigameUIRootGameObject;


                         if (minigameLogicToStart != null && minigameUIRootToActivate != null)
                         {
                              Debug.Log($"MinigameStartHandler: Identified {config.type} minigame logic and UI root.");
                              // Prepare the specific start data based on the minigame type
                              switch (config.type)
                              {
                                   case MinigameType.BarcodeScanning:
                                        // Assuming BarcodeMinigameStartData is accessible (e.g., in the same namespace)
                                        startData = new BarcodeMinigameStartData(startMinigameResponse.ItemsToScan, startMinigameResponse.CashRegisterInteractable);
                                      break;
                                   // Add cases for other minigame types and their specific data structs
                                   // case MinigameType.Lockpicking:
                                   //      startData = new LockpickingMinigameStartData(...); // Define this struct/class
                                   //     break;
                                   // ...
                                   default:
                                        Debug.LogWarning($"MinigameStartHandler: Unhandled MinigameType during data preparation for type: {config.type}.", minigameLogicToStart as UnityEngine.Object); // Log warning with context
                                       break;
                              }

                              // Return the valid start info
                              return new MinigameStartInfo(minigameLogicToStart, minigameUIRootToActivate, startData);
                         }
                         else
                         {
                              // Log specific errors if IMinigame component is missing or UI Root is null
                              if (minigameLogicToStart == null) Debug.LogError($"MinigameStartHandler: Assigned Logic GameObject '{config.minigameLogicGameObject.name}' does not have an IMinigame component for type {config.type}!", config.minigameLogicGameObject);
                              if (minigameUIRootToActivate == null) Debug.LogError($"MinigameStartHandler: Assigned UI Root GameObject is null in config for type {config.type}!", minigameLogicToStart as UnityEngine.Object); // Log warning with context

                         }
                     }
                      else
                      {
                           Debug.LogError($"MinigameStartHandler: Minigame Logic GameObject is null in config for type {config.type}!", null); // Log error
                      }
                }
                else
                {
                     Debug.LogWarning($"MinigameStartHandler: No Minigame Config found for type: {minigameType}. No minigame will start.", null); // Log warning
                }
                // ---------------------------------------------------------------------

            }
            // Add checks for other potential minigame-starting response types if they exist
            // else if (response is StartSomeOtherMinigameResponse otherResponse) { ... }
            else
            {
                 Debug.LogWarning("MinigameStartHandler: Response was not a StartMinigameResponse or another recognized start response type. Cannot prepare minigame.", null); // Log warning
            }

            // If we reached here, we couldn't prepare valid start info
            return MinigameStartInfo.Invalid;
        }

        // Note: We don't handle deactivation/ending here. That's the Manager's job.
    }
}