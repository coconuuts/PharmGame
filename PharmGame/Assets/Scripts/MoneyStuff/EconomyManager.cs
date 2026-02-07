using UnityEngine;
using TMPro; // Import TextMeshPro namespace
using GameEconomy;
using Systems.Persistence;  
using Systems.Inventory;

namespace Systems.Economy
{
    /// <summary>
    /// Manages the player's currency and updates the UI display.
    /// </summary>
    public class EconomyManager : MonoBehaviour, IBind<GameData>
    {
        // --- Singleton Instance ---
        public static EconomyManager Instance { get; private set; }
        [field: SerializeField] public SerializableGuid Id { get; set; } = SerializableGuid.NewGuid();

        [Header("Money Wallet")]
        [Tooltip("The ScriptableObject asset representing the player's money wallet.")]
        [SerializeField] private MoneyWalletSO playerMoneyWallet;

        [Header("UI Settings")]
        [Tooltip("The tag of the GameObject containing the TextMeshProUGUI for displaying money.")]
        [SerializeField] private string playerUITag = "PlayerUI"; // Tag to find the UI GameObject
        [Tooltip("The TextMeshProUGUI component that displays the player's total money.")]
        [SerializeField] private TextMeshProUGUI moneyDisplayTMP; // Reference to the TextMeshProUGUI

        public MoneyWalletSO PlayerMoneyWallet => playerMoneyWallet;
        private GameData boundData;

        private void Awake()
        {
            // Singleton pattern: If an instance already exists, destroy this one.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("EconomyManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // Removed DontDestroyOnLoad to allow scene lifecycle management

            // Ensure the Money Wallet SO is assigned
            if (playerMoneyWallet == null)
            {
                Debug.LogError("EconomyManager: Player Money Wallet SO is not assigned in the Inspector!", this);
                enabled = false;
                return;
            }

            // --- Find and assign the TextMeshProUGUI component ---
            FindAndAssignMoneyDisplayTMP();
            
            Debug.Log($"EconomyManager: Initialized with Player Money Wallet SO: {playerMoneyWallet.name}");
        }
        private void Start()
        {
            // Update the UI with the initial money amount
            UpdateMoneyDisplay();
        }

        private void OnEnable()
        {
            if (playerMoneyWallet != null)
            {
                playerMoneyWallet.OnCleanCashChanged += SyncCleanCash;
                playerMoneyWallet.OnDirtyCashChanged += SyncDirtyCash;
            }
        }

        private void OnDisable()
        {
            if (playerMoneyWallet != null)
            {
                playerMoneyWallet.OnCleanCashChanged -= SyncCleanCash;
                playerMoneyWallet.OnDirtyCashChanged -= SyncDirtyCash;
            }
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

        public void Bind(GameData data)
        {
            this.boundData = data;

            FindAndAssignMoneyDisplayTMP();
            
            // 1. LOAD: Apply saved data to the runtime wallet
            // This puts the saved numbers INTO the ScriptableObject
            if (playerMoneyWallet != null)
            {
                playerMoneyWallet.SetWallet(data.PlayerCleanMoney, data.PlayerDirtyMoney);
            }

            Debug.Log("EconomyManager: Bound to GameData. Wallet loaded.");
        }

        // These run whenever the wallet changes, ensuring 'boundData' is ready for saving at any moment.
        void SyncCleanCash(float amount)
        {
            if (boundData != null) boundData.PlayerCleanMoney = amount;
        }

        void SyncDirtyCash(float amount)
        {
            if (boundData != null) boundData.PlayerDirtyMoney = amount;
        }
    }
}