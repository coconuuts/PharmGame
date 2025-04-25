using UnityEngine;

namespace Systems.Economy // A new namespace for economy-related systems
{
    /// <summary>
    /// Manages the player's currency.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static EconomyManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("The player's starting currency.")]
        [SerializeField] private float startingCurrency = 0f;

        // --- Player's Currency ---
        private float currentCurrency;

        /// <summary>
        /// Gets the player's current currency amount.
        /// </summary>
        public float CurrentCurrency => currentCurrency;

        // Optional: Event for UI updates
        public event System.Action<float> OnCurrencyChanged;


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

            // Initialize currency
            currentCurrency = startingCurrency;
            Debug.Log($"EconomyManager: Initialized with starting currency: {currentCurrency}");
             OnCurrencyChanged?.Invoke(currentCurrency); // Trigger event on start
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
        /// Adds currency to the player's balance.
        /// </summary>
        /// <param name="amount">The amount of currency to add (should be positive).</param>
        public void AddCurrency(float amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("EconomyManager: Attempted to add negative currency. Use RemoveCurrency instead.");
                return;
            }

            currentCurrency += amount;
            Debug.Log($"EconomyManager: Added {amount} currency. New total: {currentCurrency}");
            OnCurrencyChanged?.Invoke(currentCurrency); // Trigger event for UI
        }

        /// <summary>
        /// Removes currency from the player's balance.
        /// </summary>
        /// <param name="amount">The amount of currency to remove (should be positive).</param>
        /// <returns>True if currency was successfully removed, false if balance is insufficient.</returns>
        public bool RemoveCurrency(float amount)
        {
             if (amount < 0)
            {
                Debug.LogWarning("EconomyManager: Attempted to remove negative currency. Use AddCurrency instead.");
                return false;
            }

            if (currentCurrency >= amount)
            {
                currentCurrency -= amount;
                Debug.Log($"EconomyManager: Removed {amount} currency. New total: {currentCurrency}");
                OnCurrencyChanged?.Invoke(currentCurrency); // Trigger event for UI
                return true;
            }
            else
            {
                Debug.LogWarning($"EconomyManager: Insufficient currency to remove {amount}. Current balance: {currentCurrency}.");
                return false;
            }
        }

        // TODO: Add methods for saving/loading currency if needed
    }
}