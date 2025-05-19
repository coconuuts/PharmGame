using UnityEngine;
using System;
using System.Collections.Generic; // May not be needed here yet, but good practice
using Systems.Interaction; // Needed for CashRegisterInteractable
using Systems.Inventory; // Needed for ItemDetails if needed in other processing methods
using Systems.Economy; // Needed for EconomyManager

namespace Systems.Minigame // Ensure this is in the correct namespace if needed
{
    /// <summary>
    /// Static helper class to process completion data for various minigames,
    /// keeping this logic separate from the core MinigameManager.
    /// </summary>
    public static class MinigameCompletionProcessor
    {
        /// <summary>
        /// Processes the completion data specifically for the Barcode Scanning Minigame.
        /// Expects data to be a Tuple<float, CashRegisterInteractable>.
        /// </summary>
        /// <param name="completionData">The data returned by the completed BarcodeMinigame.</param>
        public static void ProcessBarcodeCompletion(object completionData)
        {
            Debug.Log("MinigameCompletionProcessor: Processing Barcode Minigame completion data.");

            if (completionData is Tuple<float, CashRegisterInteractable> barcodeCompletionData)
            {
                float totalPayment = barcodeCompletionData.Item1;
                CashRegisterInteractable initiatingRegister = barcodeCompletionData.Item2;

                Debug.Log($"MinigameCompletionProcessor: Received total payment: {totalPayment}");

                // Process Payment (Add to Player's Currency)
                if (EconomyManager.Instance != null)
                {
                    EconomyManager.Instance.AddCurrency(totalPayment);
                    Debug.Log($"MinigameCompletionProcessor: Added {totalPayment} currency via EconomyManager.");
                }
                else Debug.LogError("MinigameCompletionProcessor: EconomyManager Instance is null! Cannot process payment.");

                // Notify the Initiating Register
                if (initiatingRegister != null)
                {
                     initiatingRegister.OnMinigameCompleted(totalPayment); // Call the completion method on the register
                     Debug.Log("MinigameCompletionProcessor: Notified initiating register of completion.");
                }
                else Debug.LogWarning("MinigameCompletionProcessor: Initiating register reference is null in completion data! Cannot notify completion.");
            }
            else
            {
                 Debug.LogWarning($"MinigameCompletionProcessor: Barcode Minigame completion data was not the expected Tuple<float, CashRegisterInteractable>. Received type: {completionData?.GetType().Name ?? "null"}.");
            }
        }

        // Add more static methods here for processing other minigame types as needed
        // For example:
        // public static void ProcessLockpickingCompletion(object completionData) { ... }
    }
}