using UnityEngine;
using System; // Needed for Action

namespace GameEconomy // A namespace for economy-related systems
{
    /// <summary>
    /// Represents the player's money wallet, tracking different types of cash.
    /// This is a ScriptableObject asset instance representing the player's current money.
    /// </summary>
    [CreateAssetMenu(fileName = "MoneyWallet", menuName = "Economy/Money Wallet", order = 50)]
    public class MoneyWalletSO : ScriptableObject
    {
        // --- Money Amounts ---
        [Tooltip("Amount of clean, legitimate cash.")]
        public int cleanCash;

        [Tooltip("Amount of dirty, untraceable cash from illicit activities.")]
        public int dirtyCash;
        // ----------------------

        // --- Events for Tracking Changes ---
        // Useful for UI updates or other systems reacting to money changes.
        public event Action<int> OnCleanCashChanged;
        public event Action<int> OnDirtyCashChanged;
        public event Action OnAnyCashChanged; // General event for any change


        /// <summary>
        /// Resets the wallet amounts. Useful for starting a new game.
        /// </summary>
        public void ResetWallet()
        {
            cleanCash = 0;
            dirtyCash = 0;
            Debug.Log("MoneyWalletSO: Wallet reset.");
            // Trigger events after reset
            OnCleanCashChanged?.Invoke(cleanCash);
            OnDirtyCashChanged?.Invoke(dirtyCash);
            OnAnyCashChanged?.Invoke();
        }

        /// <summary>
        /// Adds dirty cash to the wallet (e.g., from selling drugs).
        /// </summary>
        /// <param name="amount">The amount of dirty cash to add. Must be non-negative.</param>
        public void AddDirtyCash(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("MoneyWalletSO: Attempted to add a negative amount of dirty cash. Use Spend methods instead.");
                return;
            }
            dirtyCash += amount;
            Debug.Log($"MoneyWalletSO: Added {amount} dirty cash. Total dirty: {dirtyCash}");
            // Trigger events
            OnDirtyCashChanged?.Invoke(dirtyCash);
            OnAnyCashChanged?.Invoke();
        }

        /// <summary>
        /// Attempts to spend clean cash from the wallet.
        /// </summary>
        /// <param name="amount">The amount of clean cash to spend. Must be non-negative.</param>
        /// <returns>True if successful, false if insufficient clean cash.</returns>
        public bool SpendCleanCash(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("MoneyWalletSO: Attempted to spend a negative amount of clean cash.");
                return false;
            }
            if (cleanCash >= amount)
            {
                cleanCash -= amount;
                Debug.Log($"MoneyWalletSO: Spent {amount} clean cash. Total clean: {cleanCash}");
                // Trigger events
                OnCleanCashChanged?.Invoke(cleanCash);
                OnAnyCashChanged?.Invoke();
                return true;
            }
            else
            {
                Debug.Log($"MoneyWalletSO: Insufficient clean cash to spend {amount}. Have {cleanCash}.");
                return false;
            }
        }

        /// <summary>
        /// Attempts to spend dirty cash from the wallet.
        /// </summary>
        /// <param name="amount">The amount of dirty cash to spend. Must be non-negative.</param>
        /// <returns>True if successful, false if insufficient dirty cash.</returns>
        public bool SpendDirtyCash(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("MoneyWalletSO: Attempted to spend a negative amount of dirty cash.");
                return false;
            }
            if (dirtyCash >= amount)
            {
                dirtyCash -= amount;
                Debug.Log($"MoneyWalletSO: Spent {amount} dirty cash. Total dirty: {dirtyCash}");
                // Trigger events
                OnDirtyCashChanged?.Invoke(dirtyCash);
                OnAnyCashChanged?.Invoke();
                return true;
            }
            else
            {
                Debug.Log($"MoneyWalletSO: Insufficient dirty cash to spend {amount}. Have {dirtyCash}.");
                return false;
            }
        }

        // --- Basic Laundering Placeholder ---
        // This is a simplified example. Actual laundering mechanics will be more complex.
        /// <summary>
        /// Attempts to launder dirty cash into clean cash.
        /// </summary>
        /// <param name="amount">The amount of dirty cash to attempt to launder.</param>
        /// <param name="cost">The cost to launder the money.</param>
        /// <returns>True if laundering is successful, false otherwise (e.g., not enough dirty cash or cost).</returns>
        public bool AttemptLaunder(int amount, int cost)
        {
            if (amount < 0 || cost < 0)
            {
                 Debug.LogWarning("MoneyWalletSO: Attempted to launder negative amount or with negative cost.");
                 return false;
            }
            if (dirtyCash >= amount)
            {
                // In a real system, laundering would have success chance, take time,
                // potentially require other resources or actions, and involve spending/losing some money.
                // For this placeholder, we'll just assume a simple cost.

                // Check if we can afford the cost (assuming cost is paid with clean cash for simplicity here)
                // Or you might have 'laundering expenses' as another cash type
                bool canAffordCost = SpendCleanCash(cost); // Example: cost is paid with clean cash

                if (canAffordCost)
                {
                     dirtyCash -= amount;
                     cleanCash += amount - cost; // The amount laundered minus the cost
                     Debug.Log($"MoneyWalletSO: Successfully laundered {amount} dirty cash at cost of {cost}.");
                     Debug.Log($"MoneyWalletSO: New Dirty: {dirtyCash}, New Clean: {cleanCash}");

                     // Trigger events
                     OnDirtyCashChanged?.Invoke(dirtyCash);
                     OnCleanCashChanged?.Invoke(cleanCash);
                     OnAnyCashChanged?.Invoke();

                     return true;
                }
                else
                {
                     Debug.Log($"MoneyWalletSO: Cannot afford laundering cost of {cost}. Have {cleanCash} clean cash.");
                     return false;
                }
            }
            else
            {
                Debug.Log($"MoneyWalletSO: Insufficient dirty cash to launder {amount}. Have {dirtyCash}.");
                return false;
            }
        }


        // --- Getters for convenience ---
        public int TotalCash => cleanCash + dirtyCash;
        public int CleanCash => cleanCash;
        public int DirtyCash => dirtyCash;
        // -------------------------------
    }
}