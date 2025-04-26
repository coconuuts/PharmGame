using UnityEngine;
using TMPro; // Import TextMeshPro namespace
using GameEconomy;

namespace Systems.Economy
{
    /// <summary>
    /// Manages the player's currency and updates the UI display.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static EconomyManager Instance { get; private set; }

        [Header("Money Wallet")]
        [Tooltip("The ScriptableObject asset representing the player's money wallet.")]
        [SerializeField] private MoneyWalletSO playerMoneyWallet;

        [Header("UI Settings")]
        [Tooltip("The tag of the GameObject containing the TextMeshProUGUI for displaying money.")]
        [SerializeField] private string playerUITag = "PlayerUI"; // Tag to find the UI GameObject
        [Tooltip("The TextMeshProUGUI component that displays the player's total money.")]
        [SerializeField] private TextMeshProUGUI moneyDisplayTMP; // Reference to the TextMeshProUGUI

        // --- Provide access to the Money Wallet SO ---
        public MoneyWalletSO PlayerMoneyWallet => playerMoneyWallet;

        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // If manager should persist between scenes
            }
            else
            {
                Debug.LogWarning("EconomyManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Ensure the Money Wallet SO is assigned
            if (playerMoneyWallet == null)
            {
                Debug.LogError("EconomyManager: Player Money Wallet SO is not assigned in the Inspector!", this);
                enabled = false; // Disable the script if the wallet is missing
                return; // Exit Awake
            }

            // --- Find and assign the TextMeshProUGUI component ---
            FindAndAssignMoneyDisplayTMP();
            // --- End of UI finding ---

            Debug.Log($"EconomyManager: Initialized with Player Money Wallet SO: {playerMoneyWallet.name}");

            // Optional: Reset the wallet when the manager awakes (e.g., for a new game start in this scene)
            // playerMoneyWallet.ResetWallet(); // Decide if you want this behavior

            // Initial balance will be whatever is set in the SO asset or reset by ResetWallet()
            // No float currency event to trigger here anymore.
        }

        private void Start()
        {
            // Update the UI with the initial money amount
            UpdateMoneyDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            // Note: Unsubscribe from OnCurrencyChanged by any listeners if DontDestroyOnLoad is not used
        }

        /// <summary>
        /// Finds the GameObject with the PlayerUITag and attempts to get the TextMeshProUGUI component.
        /// </summary>
        private void FindAndAssignMoneyDisplayTMP()
        {
            GameObject playerUIGameObject = GameObject.FindWithTag(playerUITag);

            if (playerUIGameObject != null)
            {
                // Try to get the component directly from the tagged GameObject
                moneyDisplayTMP = playerUIGameObject.GetComponent<TextMeshProUGUI>();

                if (moneyDisplayTMP == null)
                {
                    // If not found directly, search in children
                    moneyDisplayTMP = playerUIGameObject.GetComponentInChildren<TextMeshProUGUI>();
                }

                if (moneyDisplayTMP == null)
                {
                    Debug.LogWarning($"EconomyManager: TextMeshProUGUI component not found on GameObject with tag '{playerUITag}' or in its children.", this);
                }
            }
            else
            {
                Debug.LogWarning($"EconomyManager: GameObject with tag '{playerUITag}' not found. Money display will not be updated.", this);
            }
        }

        /// <summary>
        /// Updates the TextMeshProUGUI with the current total money amount.
        /// </summary>
        private void UpdateMoneyDisplay()
        {
            if (moneyDisplayTMP != null && playerMoneyWallet != null)
            {
                moneyDisplayTMP.text = $"Money: {playerMoneyWallet.TotalCash:F2}"; // Format to 2 decimal places
            }
             else if (moneyDisplayTMP == null && GameObject.FindWithTag(playerUITag) != null)
            {
                 // This case handles if the TMP component was found but then became null somehow
                 // or wasn't found initially but the tagged object exists.
                 // We could try to find it again here, but it's better to ensure it's found in Awake.
                 Debug.LogWarning("EconomyManager: Money display TextMeshProUGUI is null. UI will not update.", this);
            }
        }


        /// <summary>
        /// Adds clean currency to the player's balance via the MoneyWalletSO and updates the UI.
        /// </summary>
        /// <param name="amount">The amount of clean currency to add (should be positive).</param>
        public void AddCurrency(float amount)
        {
            if (playerMoneyWallet == null)
            {
                Debug.LogError("EconomyManager: Cannot add currency - Player Money Wallet SO is null!", this);
                return;
            }

            playerMoneyWallet.AddCleanCash(amount);
            UpdateMoneyDisplay(); // Update UI after adding currency
        }

        /// <summary>
        /// Attempts to spend clean currency from the player's balance via the MoneyWalletSO and updates the UI if successful.
        /// </summary>
        /// <param name="amount">The amount of currency to remove (should be positive).</param>
        /// <returns>True if currency was successfully removed, false if balance is insufficient.</returns>
        public bool RemoveCurrency(float amount)
        {
            if (playerMoneyWallet == null)
            {
                Debug.LogError("EconomyManager: Cannot remove currency - Player Money Wallet SO is null!", this);
                return false;
            }

            bool success = playerMoneyWallet.SpendCleanCash(amount);
            if (success)
            {
                UpdateMoneyDisplay(); // Update UI only if spending was successful
            }
            return success;
        }

        /// <summary>
        /// Adds dirty currency to the player's balance via the MoneyWalletSO and updates the UI.
        /// </summary>
        public void AddDirtyCurrency(float amount)
        {
            if (playerMoneyWallet == null)
            {
                Debug.LogError("EconomyManager: Cannot add dirty currency - Player Money Wallet SO is null!", this);
                return;
            }
            playerMoneyWallet.AddDirtyCash(amount);
            UpdateMoneyDisplay(); // Update UI after adding dirty currency
        }

        // You can add getters here to access the wallet's current amounts if needed by systems
        // that prefer accessing via the manager singleton rather than the SO directly.
        public float GetCleanCash() => playerMoneyWallet?.CleanCash ?? 0;
        public float GetDirtyCash() => playerMoneyWallet?.DirtyCash ?? 0;
        public float GetTotalCash() => playerMoneyWallet?.TotalCash ?? 0;

        // TODO: Add methods for saving/loading currency if needed
    }
}