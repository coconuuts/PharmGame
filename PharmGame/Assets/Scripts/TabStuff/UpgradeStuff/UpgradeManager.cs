// --- START OF FILE UpgradeManager.cs ---

// --- START OF FILE UpgradeManager.cs ---

// --- START OF FILE UpgradeManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System; // Needed for Action
using System.Linq; // Needed for ToHashSet(), FirstOrDefault
using Systems.Persistence; 
using Systems.Inventory;

// Make sure this script is in a namespace if you are using them consistently
// namespace Systems.Upgrades // Example namespace
// {

    /// <summary>
    /// Singleton manager responsible for holding available upgrade data,
    /// tracking purchased upgrades, and dispatching an event when an upgrade
    /// purchase is attempted via the UI.
    /// MODIFIED: Added tracking and checking for purchased upgrades.
    /// ADDED: Constant and helper method for the "Music License" upgrade.
    /// </summary>
    public class UpgradeManager : MonoBehaviour, IBind<GameData>
    {
        // --- Public Configuration ---
        [Header("Passive Effect Modifiers")]
        [Tooltip("The multiplier applied to customer impatience timers when the 'Music License' is purchased. E.g., 1.5 means 50% longer wait time.")]
        public const float MusicLicensePatienceModifier = 1.5f; // Customers will wait 50% longer

        // --- Singleton Instance ---
        public static UpgradeManager Instance { get; private set; }
        [field: SerializeField] public SerializableGuid Id { get; set; } = SerializableGuid.NewGuid();
        private GameData boundData;

        // --- Upgrade Data ---
        [Header("Available Upgrades")]
        [Tooltip("List of all ScriptableObject assets defining the available upgrades.")]
        [SerializeField] private List<UpgradeDetailsSO> allAvailableUpgrades = new List<UpgradeDetailsSO>();

        // Public property to allow other scripts to access the list of upgrades
        public List<UpgradeDetailsSO> AllAvailableUpgrades => allAvailableUpgrades;

        // --- Tracking Purchased Upgrades ---
        // Using a HashSet for efficient checking if an upgrade has been purchased.
        // Stores the UpgradeDetailsSO references directly.
        // NOTE: For persistent saving/loading, you'd likely save/load the uniqueID string instead.
        private HashSet<UpgradeDetailsSO> purchasedUpgrades;

        // --- Cached Upgrade References for Performance ---
        private UpgradeDetailsSO musicLicenseSO;


        // --- Events ---
        /// <summary>
        /// Event triggered when the player attempts to purchase an upgrade via the UI.
        /// Subscribers should handle the actual purchase logic (cost, effects, etc.).
        /// </summary>
        public event Action<UpgradeDetailsSO> OnUpgradePurchaseAttempt;

        /// <summary>
        /// NEW: Event triggered when an upgrade is successfully purchased and marked as such.
        /// Subscribers can react to successful purchases (e.g., update UI, grant item).
        /// </summary>
        public event Action<UpgradeDetailsSO> OnUpgradePurchasedSuccessfully;


        // --- Singleton Implementation ---
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Optional: Keep this object alive across scene changes if needed
                DontDestroyOnLoad(gameObject); // UNCOMMENTED THIS LINE
                Debug.Log($"UpgradeManager: Singleton instance created on GameObject '{gameObject.name}'.", this);

                // Initialize the purchased upgrades set
                purchasedUpgrades = new HashSet<UpgradeDetailsSO>();

                 // --- Future: Load purchased upgrades from save data here ---
                 // Example (placeholder):
                 // LoadGameData();
                 // purchasedUpgrades = loadedData.purchasedUpgradeIDs.Select(id => FindUpgradeDetailsByID(id)).Where(so => so != null).ToHashSet();
                 // Debug.Log($"UpgradeManager: Loaded {purchasableItems.Count} purchased upgrades."); // Corrected a typo here, should be purchasedUpgrades.Count
                 // --- End Future Load ---

                 // --- Cache specific upgrade SOs for performance ---
                 musicLicenseSO = GetUpgradeDetailsByName("Music License");
                 if (musicLicenseSO == null)
                 {
                     Debug.LogWarning("UpgradeManager: 'Music License' SO not found in the AllAvailableUpgrades list. The upgrade effect will not work.", this);
                 }

            }
            else
            {
                // If an instance already exists, destroy this duplicate
                Debug.LogWarning($"UpgradeManager: Duplicate instance found on GameObject '{gameObject.name}'. Destroying it. Existing instance is on GameObject '{Instance.gameObject.name}'.", this);
                Destroy(gameObject);
            }
        }

        // IBind Implementation
        public void Bind(GameData data)
        {
            boundData = data;
            
            // 1. LOAD: Restore purchased upgrades from ID list
            purchasedUpgrades.Clear();
            
            if (data.UnlockedUpgradeIds != null)
            {
                foreach (string id in data.UnlockedUpgradeIds)
                {
                    // Find the SO asset that matches this saved ID
                    UpgradeDetailsSO upgrade = allAvailableUpgrades.FirstOrDefault(u => u.uniqueID == id);
                    
                    if (upgrade != null)
                    {
                        purchasedUpgrades.Add(upgrade);
                    }
                    else
                    {
                        Debug.LogWarning($"UpgradeManager: Could not find upgrade asset with ID '{id}' during load.");
                    }
                }
            }
            
            Debug.Log($"UpgradeManager: Loaded {purchasedUpgrades.Count} upgrades.");
        }

        // Optional: Add a method to trigger the purchase event from other scripts
        // (Though the UI handler will likely be the primary caller)
        public void TriggerUpgradePurchaseAttempt(UpgradeDetailsSO upgrade)
        {
            if (upgrade == null)
            {
                Debug.LogWarning("UpgradeManager: Attempted to trigger purchase attempt for a null upgrade.", this);
                return;
            }

            Debug.Log($"UpgradeManager: Triggering purchase attempt event for '{upgrade.upgradeName}'.", this);
            // Safely invoke the event, checking if there are any subscribers
            OnUpgradePurchaseAttempt?.Invoke(upgrade);
        }

        /// <summary>
        /// NEW: Marks an upgrade as successfully purchased.
        /// Should be called by the system handling the actual purchase logic (e.g., UpgradeEffectHandler)
        /// AFTER cost is deducted and effects are applied.
        /// </summary>
        /// <param name="upgrade">The UpgradeDetailsSO that was purchased.</param>
        public void MarkUpgradeAsPurchased(UpgradeDetailsSO upgrade)
        {
            if (upgrade == null)
            {
                Debug.LogWarning("UpgradeManager: Attempted to mark null upgrade as purchased.", this);
                return;
            }

            // Check if it's already marked as purchased (defensive)
            if (purchasedUpgrades.Contains(upgrade))
            {
                Debug.LogWarning($"UpgradeManager: Upgrade '{upgrade.upgradeName}' (ID: {upgrade.uniqueID}) was already marked as purchased.", this);
                // Optional: Re-invoke the purchased event or handle logic for multi-level upgrades
                // OnUpgradePurchasedSuccessfully?.Invoke(upgrade); // Maybe for multi-level?
                return;
            }

            purchasedUpgrades.Add(upgrade);
            Debug.Log($"UpgradeManager: Marked upgrade '{upgrade.upgradeName}' (ID: {upgrade.uniqueID}) as purchased. Total purchased: {purchasedUpgrades.Count}");

            if (boundData != null)
            {
                if (!boundData.UnlockedUpgradeIds.Contains(upgrade.uniqueID))
                {
                    boundData.UnlockedUpgradeIds.Add(upgrade.uniqueID);
                }
            }

            // Notify subscribers that an upgrade was successfully purchased
            OnUpgradePurchasedSuccessfully?.Invoke(upgrade);
        }

        /// <summary>
        /// NEW: Checks if a specific upgrade has been marked as purchased.
        /// </summary>
        /// <param name="upgrade">The UpgradeDetailsSO to check.</param>
        /// <returns>True if the upgrade is in the purchased list, false otherwise.</returns>
        public bool IsUpgradePurchased(UpgradeDetailsSO upgrade)
        {
            if (upgrade == null)
            {
                // Debug.LogWarning("UpgradeManager: Checking purchased status for null upgrade."); // Can be noisy
                return false; // A null upgrade is never purchased
            }
            // Use null-conditional operator for safety if purchasedUpgrades somehow isn't initialized
            return purchasedUpgrades?.Contains(upgrade) ?? false;
        }

        /// <summary>
        /// Checks if the 'Music License' upgrade has been purchased using a cached reference.
        /// </summary>
        /// <returns>True if the upgrade is owned, false otherwise.</returns>
        public bool IsMusicLicensePurchased()
        {
            if (musicLicenseSO == null) return false;
            return IsUpgradePurchased(musicLicenseSO);
        }

        /// <summary>
        /// NEW: Retrieves an UpgradeDetailsSO by its upgradeName.
        /// </summary>
        /// <param name="upgradeName">The name of the upgrade to find.</param>
        /// <returns>The UpgradeDetailsSO if found, otherwise null.</returns>
        public UpgradeDetailsSO GetUpgradeDetailsByName(string upgradeName)
        {
            if (string.IsNullOrEmpty(upgradeName))
            {
                Debug.LogWarning("UpgradeManager: GetUpgradeDetailsByName called with null or empty upgradeName.", this);
                return null;
            }
            // Use LINQ's FirstOrDefault to find the first matching upgrade or null
            return allAvailableUpgrades.FirstOrDefault(u => u.upgradeName == upgradeName);
        }


        // --- Future Placeholder for Loading/Saving ---
        // private UpgradeDetailsSO FindUpgradeDetailsByID(string id)
        // {
        //     return allAvailableUpgrades.Find(up => up.uniqueID == id);
        // }

        // private void SaveGameData()
        // {
        //      // Implement your save logic here, serializing the unique IDs from purchasedUpgrades.
        //      // Example:
        //      // SaveData data = new SaveData();
        //      // data.purchasedUpgradeIDs = purchasedUpgrades.Select(up => up.uniqueID).ToList();
        //      // SaveSystem.Save(data);
        //      Debug.Log("UpgradeManager: (Placeholder) SaveGameData called.");
        // }

        // private void LoadGameData()
        // {
        //      // Implement your load logic here.
        //      // Example:
        //      // SaveData data = SaveSystem.Load();
        //      // if (data != null)
        //      // {
        //      //     // Process loaded data and populate purchasedUpgrades
        //      // }
        //      Debug.Log("UpgradeManager: (Placeholder) LoadGameData called.");
        // }
        // --- End Future Placeholder ---

    }

// } // End namespace (if using one)

// --- END OF FILE UpgradeManager.cs ---