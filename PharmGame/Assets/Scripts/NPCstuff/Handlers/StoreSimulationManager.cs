// --- START OF FILE StoreSimulationManager.cs ---

using UnityEngine;
using System.Collections; // Needed for Coroutine
using System.Collections.Generic; // Needed for List
using Systems.Economy; // Needed for EconomyManager
using Game.NPC.TI; // Needed for TiNpcManager
using CustomerManagement; // Needed for CustomerManager
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails
using System.Linq; // Needed for FirstOrDefault, ToList, Sum

/// <summary>
/// Manages the simulation of store sales when the Cashier TI NPC is inactive and the
/// 'Hire Cashier' upgrade has been purchased.
/// MODIFIED: Updated calls to CustomerManager to specify the source of the pause request.
/// </summary>
public class StoreSimulationManager : MonoBehaviour
{
    // --- Singleton Instance ---
    public static StoreSimulationManager Instance { get; private set; }

    [Header("Simulation Settings")]
    [Tooltip("The time interval (in seconds) between simulated sales.")]
    [SerializeField] private float simulationInterval = 1f;
    [Tooltip("The minimum number of items to attempt to sell per simulated sale tick.")]
    [SerializeField] private int itemsPerSaleMin = 1;
    [Tooltip("The maximum number of items to attempt to sell per simulated sale tick.")]
    [SerializeField] private int itemsPerSaleMax = 3;
    [Tooltip("The tag assigned to GameObjects representing store shelves that contain sellable inventory.")]
    [SerializeField] private string storageShelfTag = "StorageShelf";

    [Header("Dependencies (Assigned at Runtime)")]
    private EconomyManager economyManager;
    private TiNpcManager tiNpcManager;
    private CustomerManager customerManager;
    private UpgradeManager upgradeManager;
    
    // This will store the reference to the specific 'Hire Cashier' UpgradeDetailsSO asset.
    // It's populated at runtime to avoid hardcoding asset paths.
    private UpgradeDetailsSO hireCashierUpgradeSO; 

    // Internal state tracking for the simulation
    private bool isSimulationActive = false;
    private Coroutine salesSimulationCoroutine;
    private List<Inventory> allStorageShelves; // Cached list of Inventory components from shelves

    void Awake()
    {
        // Implement singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // Optional: DontDestroyOnLoad(gameObject); // Consider if this manager should persist across scenes
        }
        else
        {
            Debug.LogWarning("StoreSimulationManager: Duplicate instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        // Initialize lists/collections
        allStorageShelves = new List<Inventory>();

        Debug.Log("StoreSimulationManager: Awake completed.");
    }

    void Start()
    {
        // --- Acquire references to singleton managers ---
        economyManager = EconomyManager.Instance;
        if (economyManager == null)
        {
            Debug.LogError("StoreSimulationManager: EconomyManager instance not found! Store simulation cannot process sales.", this);
            // Consider disabling the component if this is a critical dependency
            // enabled = false;
        }

        tiNpcManager = TiNpcManager.Instance;
        if (tiNpcManager == null)
        {
            Debug.LogError("StoreSimulationManager: TiNpcManager instance not found! Store simulation cannot interact with TI NPCs.", this);
        }

        customerManager = CustomerManager.Instance;
        if (customerManager == null)
        {
            Debug.LogError("StoreSimulationManager: CustomerManager instance not found! Store simulation cannot pause/resume active NPC spawning.", this);
        }

        upgradeManager = UpgradeManager.Instance;
        if (upgradeManager == null)
        {
            Debug.LogError("StoreSimulationManager: UpgradeManager instance not found! Store simulation cannot check for 'Hire Cashier' upgrade.", this);
        }

        // --- Retrieve the 'Hire Cashier' UpgradeDetailsSO ---
        // Now using the new helper method from UpgradeManager
        if (upgradeManager != null)
        {
            hireCashierUpgradeSO = upgradeManager.GetUpgradeDetailsByName("Hire Cashier");
            if (hireCashierUpgradeSO == null)
            {
                Debug.LogWarning("StoreSimulationManager: 'Hire Cashier' UpgradeDetailsSO not found via GetUpgradeDetailsByName! Store simulation will not run even if cashier is hired. Ensure the upgrade asset's 'upgradeName' is exactly 'Hire Cashier'.", this);
            }
        }

        Debug.Log("StoreSimulationManager: Start completed. Dependencies acquired.");
    }

    void OnEnable()
    {
        // If the component is enabled, ensure simulation is stopped if it was running
        // This prevents a coroutine from running unexpectedly if the component was disabled mid-simulation
        StopSimulation(); 
    }

    void OnDisable()
    {
        // When the component is disabled, stop the simulation to prevent errors
        StopSimulation();
    }

    /// <summary>
    /// Starts the off-screen store sales simulation.
    /// </summary>
    public void StartSimulation()
    {
        if (isSimulationActive)
        {
            Debug.Log("StoreSimulationManager: Simulation is already active. Skipping StartSimulation call.", this);
            return;
        }

        // Validate critical dependencies before starting
        if (economyManager == null || customerManager == null || upgradeManager == null)
        {
            Debug.LogError("StoreSimulationManager: Cannot start simulation. One or more critical dependencies are null. Check Start() logs.", this);
            return;
        }
        if (hireCashierUpgradeSO == null)
        {
            Debug.LogWarning("StoreSimulationManager: Cannot start simulation. 'Hire Cashier' UpgradeDetailsSO is null. Ensure it's assigned and found.", this);
            // We allow simulation to start, but it will immediately pause in the routine if upgrade is not found.
            // This allows the cashier to still transition to BasicWaitingForCustomer even if the upgrade is misconfigured.
        }

        isSimulationActive = true;
        // Notify CustomerManager to pause active NPC spawning
        if (customerManager != null)
        {
            customerManager.SetStoreSimulationActive(true, StorePauseSource.CashierSimulation);
        }

        salesSimulationCoroutine = StartCoroutine(SimulateSalesRoutine());
        Debug.Log("StoreSimulationManager: Store Simulation Started.", this);
    }

    /// <summary>
    /// Stops the off-screen store sales simulation.
    /// </summary>
    public void StopSimulation()
    {
        if (!isSimulationActive)
        {
            // Debug.Log("StoreSimulationManager: Simulation is not active. Skipping StopSimulation call.", this); // Too noisy
            return;
        }

        isSimulationActive = false;
        if (salesSimulationCoroutine != null)
        {
            StopCoroutine(salesSimulationCoroutine);
            salesSimulationCoroutine = null;
        }
        // Notify CustomerManager to resume active NPC spawning
        if (customerManager != null)
        {
            customerManager.SetStoreSimulationActive(false, StorePauseSource.CashierSimulation);
        }

        Debug.Log("StoreSimulationManager: Store Simulation Stopped.", this);
    }

    /// <summary>
    /// The coroutine that simulates sales activity in the store when the cashier is inactive.
    /// </summary>
    private IEnumerator SimulateSalesRoutine()
    {
        Debug.Log("StoreSimulationManager: SimulateSalesRoutine started.", this);

        // --- Initial Scan of Shelves ---
        GameObject[] shelfObjects = GameObject.FindGameObjectsWithTag(storageShelfTag);
        allStorageShelves.Clear(); 
        foreach (GameObject shelfObj in shelfObjects)
        {
            Inventory shelfInventory = shelfObj.GetComponent<Inventory>();
            if (shelfInventory != null)
            {
                allStorageShelves.Add(shelfInventory);
            }
            else
            {
                Debug.LogWarning($"StoreSimulationManager: GameObject '{shelfObj.name}' with tag '{storageShelfTag}' is missing an Inventory component. Skipping.", shelfObj);
            }
        }

        if (allStorageShelves.Count == 0)
        {
            Debug.LogWarning($"StoreSimulationManager: No GameObjects with tag '{storageShelfTag}' and an Inventory component found. Store simulation cannot proceed with sales. However, active NPC spawning will remain paused as long as cashier is inactive.", this);
            // DO NOT call StopSimulation() or yield break here.
            // The loop will continue, but sales won't occur.
        }
        Debug.Log($"StoreSimulationManager: Found {allStorageShelves.Count} storage shelves for simulation.", this);


        // --- Main Simulation Loop ---
        while (isSimulationActive) // Loop continues as long as cashier is inactive
        {
            yield return new WaitForSeconds(simulationInterval);

            // 1. Check "Hire Cashier" Upgrade
            if (upgradeManager == null || !upgradeManager.IsUpgradePurchased(hireCashierUpgradeSO))
            {
                Debug.Log($"StoreSimulationManager: 'Hire Cashier' upgrade not purchased or UpgradeManager is null. Sales simulation paused, but active NPC spawning remains off.", this);
                continue; // Skip sales, but keep looping and `isSimulationActive` true.
            }

            // 2. Collect all available items WITH their origin shelf for this tick
            List<(Item item, Inventory shelf)> availableItemsWithOrigin = new List<(Item, Inventory)>();
            foreach (Inventory shelf in allStorageShelves)
            {
                if (shelf != null && shelf.InventoryState != null)
                {
                    foreach (Item item in shelf.InventoryState.GetCurrentArrayState())
                    {
                        if (item != null && item.quantity > 0)
                        {
                            availableItemsWithOrigin.Add((item, shelf));
                        }
                    }
                }
            }

            if (availableItemsWithOrigin.Count == 0)
            {
                Debug.Log("StoreSimulationManager: No items left on any shelves. Sales simulation paused, but active NPC spawning remains off.", this);
                continue; // Skip sales, but keep looping and `isSimulationActive` true.
            }

            // 3. Simulate a Sale (only runs if upgrade is purchased AND items are available)
            int itemsToSellThisTick = UnityEngine.Random.Range(itemsPerSaleMin, itemsPerSaleMax + 1);
            float totalSaleValue = 0f;
            int itemsActuallySold = 0;

            for (int i = 0; i < itemsToSellThisTick; i++)
            {
                // Re-check if any items are still available in our temporary list
                if (availableItemsWithOrigin.Count == 0)
                {
                    Debug.Log($"StoreSimulationManager: Ran out of items to sell during this tick after selling {itemsActuallySold}.");
                    break; // No more items to sell in this tick from the current snapshot
                }

                // Randomly pick an item-shelf pair from the currently available list
                int randomIndex = UnityEngine.Random.Range(0, availableItemsWithOrigin.Count);
                (Item selectedItem, Inventory originShelf) = availableItemsWithOrigin[randomIndex];
                
                // Remove this item from the temporary list IMMEDIATELY to prevent re-picking it in the same tick.
                availableItemsWithOrigin.RemoveAt(randomIndex);

                // Defensive check, though originShelf should be valid from how availableItemsWithOrigin is built
                if (originShelf == null) 
                {
                    Debug.LogError($"StoreSimulationManager: Origin shelf for selected item {selectedItem.details.Name} (ID: {selectedItem.Id}) is null. This indicates a data inconsistency during initial collection.", this);
                    continue; // Skip this problematic item and try next iteration
                }

                // Attempt to remove 1 quantity of this item type from its origin shelf.
                // TryRemoveQuantity works for both stackable (reduces quantity) and non-stackable (removes instance).
                int quantityRemoved = originShelf.TryRemoveQuantity(selectedItem.details, 1);

                if (quantityRemoved > 0)
                {
                    totalSaleValue += selectedItem.details.price * quantityRemoved;
                    itemsActuallySold += quantityRemoved;
                    Debug.Log($"StoreSimulationManager: Sold {quantityRemoved} of {selectedItem.details.Name} from shelf '{originShelf.gameObject.name}'. Current sale value: {totalSaleValue:F2}.", originShelf.gameObject);

                    // IMPORTANT: We do NOT need to re-filter `availableItemsWithOrigin` here.
                    // 1. We removed the item from `availableItemsWithOrigin` using `RemoveAt(randomIndex)`.
                    // 2. If the `selectedItem` was stackable and still has quantity > 0 after `TryRemoveQuantity`,
                    //    it will be re-added to `availableItemsWithOrigin` at the start of the *next* simulation tick,
                    //    ensuring a fresh snapshot.
                }
                else
                {
                    // Item couldn't be removed (e.g., it was already taken by another simulated sale in this tick, or quantity was 0)
                    Debug.LogWarning($"StoreSimulationManager: Failed to remove item {selectedItem.details.Name} from shelf '{originShelf.gameObject.name}' during simulation. Skipping this item.", originShelf.gameObject);
                    // No need to remove from `availableItemsWithOrigin` here, it was already removed by `RemoveAt(randomIndex)`.
                }
            }

            // 4. Add Money to Player
            if (itemsActuallySold > 0)
            {
                economyManager.AddCurrency(totalSaleValue);
                Debug.Log($"StoreSimulationManager: Simulated sale completed. {itemsActuallySold} items sold for ${totalSaleValue:F2}. Total money: {economyManager.GetTotalCash():F2}.", this);
            }
            else
            {
                Debug.Log("StoreSimulationManager: No items were actually sold in this tick (perhaps shelves were empty or items couldn't be removed).", this);
            }
        }
        Debug.Log("StoreSimulationManager: SimulateSalesRoutine finished.", this);
    }
}

// --- END OF FILE StoreSimulationManager.cs ---