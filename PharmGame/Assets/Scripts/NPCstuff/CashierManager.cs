// --- START OF FILE CashierManager.cs ---

using UnityEngine;

namespace Game.NPC // Or a new namespace like Game.Cashiers if preferred, but keeping it within NPC for now
{
    /// <summary>
    /// Manages Cashier-specific elements, such as the Cashier's designated spot.
    /// </summary>
    public class CashierManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static CashierManager Instance { get; private set; }

        [Header("Cashier Settings")]
        [Tooltip("The point where the Cashier NPC should stand when working.")]
        [SerializeField] private Transform cashierSpot;

        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // Consider if this manager should persist
            }
            else
            {
                Debug.LogWarning("CashierManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Validate essential references
            if (cashierSpot == null)
            {
                Debug.LogError("CashierManager: Cashier Spot Transform is not assigned!", this);
                // Don't disable the manager, but log the error.
            }

            Debug.Log("CashierManager: Awake completed.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Debug.Log("CashierManager: OnDestroy completed.");
        }

        /// <summary>
        /// Gets the Transform for the Cashier's designated standing spot.
        /// </summary>
        public Transform GetCashierSpot()
        {
            if (cashierSpot == null)
            {
                Debug.LogError("CashierManager: GetCashierSpot called but cashierSpot is null!", this);
            }
            return cashierSpot;
        }

        // Add other Cashier-specific manager methods here as needed
    }
}

// --- END OF FILE CashierManager.cs ---