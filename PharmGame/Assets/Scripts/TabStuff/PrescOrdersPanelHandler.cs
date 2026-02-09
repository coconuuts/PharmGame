using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Systems.UI;
using Game.Prescriptions;
using System.Linq;
using System.Text;
using Random = UnityEngine.Random;
using Utils.Pooling;
using Systems.Player;
using System;
using Systems.Inventory;
using Systems.GameStates;

public class PrescOrdersPanelHandler : MonoBehaviour, IPanelActivatable
{
    public enum SortCriterion
    {
        PatientName,
        PrescribedDrug,
        Amount
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    [Header("UI References")]
    [Tooltip("The Transform parent for the list of order buttons (the 'Content' GameObject under the ScrollRect).")]
    [SerializeField] private Transform orderListContentParent;

    [Tooltip("The GameObject container for the order detail view.")]
    [SerializeField] private GameObject orderDetailArea;

    [Tooltip("The TextMeshProUGUI component that displays the order details and notes.")]
    [SerializeField] private TextMeshProUGUI orderDetailText;

    [Header("Prefab References")]
    [Tooltip("The prefab for the individual prescription order buttons in the list.")]
    [SerializeField] private GameObject prescriptionOrderButtonPrefab;

    [Header("Doctor's Notes")]
    [Tooltip("A list of possible doctor's notes to randomly select from for the detail view.")]
    [SerializeField] private List<string> doctorsNotesLibrary = new List<string>();

    [Header("Controls")]
    [Tooltip("Button to refresh the list of orders.")]
    [SerializeField] private Button refreshButton;
    [Tooltip("Button to sort the list by Patient Name (Assign in Inspector).")]
    [SerializeField] private Button patientNameSortButton;
    [Tooltip("Button to sort the list by Prescribed Drug (Assign in Inspector).")]
    [SerializeField] private Button prescribedDrugSortButton;
    [Tooltip("Button to sort the list by Amount (Assign in Inspector).")]
    [SerializeField] private Button amountSortButton;
    [Tooltip("TextMeshProUGUI for the arrow on the Patient Name sort button (Assign in Inspector).")]
    [SerializeField] private TextMeshProUGUI patientNameArrowText;
    [Tooltip("TextMeshProUGUI for the arrow on the Prescribed Drug sort button (Assign in Inspector).")]
    [SerializeField] private TextMeshProUGUI prescribedDrugArrowText;
    [Tooltip("TextMeshProUGUI for the arrow on the Amount sort button (Assign in Inspector).")]
    [SerializeField] private TextMeshProUGUI amountArrowText;
    [Tooltip("Button to make the selected order the player's active task.")]
    [SerializeField] private Button makeActiveButton;
    [Tooltip("Button to mark the player's active order as ready for delivery.")]
    [SerializeField] private Button markReadyButton;

    // --- REMOVED Inspector assignment for cross-scene compatibility ---
    private PlayerPrescriptionTracker playerPrescriptionTracker; 

    [Header("Upgrade Dependencies")]
    [Tooltip("The exact 'upgradeName' from the UpgradeDetailsSO that enables the Make Active/Ready buttons.")]
    [SerializeField] private string premiumSoftwareUpgradeName = "Premium Software";

    // --- Manager References ---
    private PrescriptionManager prescriptionManager;
    private UpgradeManager upgradeManager;

    // --- Internal Tracking ---
    private List<GameObject> activeButtonInstances = new List<GameObject>();
    private List<PrescriptionOrder> displayedOrders;
    private SortCriterion currentSortCriterion = SortCriterion.PatientName;
    private SortDirection currentSortDirection = SortDirection.Ascending;
    private GameObject currentlyHighlightedButton = null;
    private PrescriptionOrder? currentDetailOrder;


    // --- IPanelActivatable Implementation ---

    public void OnPanelActivated()
    {
        Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Panel Activated. Attempting to populate order list.", this);

        if (orderDetailArea == null || orderListContentParent == null || prescriptionOrderButtonPrefab == null)
        {
            Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Essential UI references missing in OnPanelActivated. Cannot proceed.", this);
            if (orderDetailArea != null) orderDetailArea.SetActive(false);
            return;
        }
        
        // --- Runtime Lookup: Find the tracker via Singleton ---
        if (playerPrescriptionTracker == null)
        {
            playerPrescriptionTracker = PlayerPrescriptionTracker.Instance;
            if (playerPrescriptionTracker == null)
            {
                Debug.LogWarning($"PrescOrdersPanelHandler: PlayerPrescriptionTracker instance not found yet. It might be in a scene that hasn't loaded.", this);
            }
        }

        orderDetailArea.SetActive(false);
        ClearOrderList();
        FetchAndPopulateOrders();

        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshOrderList);
        }

        AddSortButtonListeners();

        // Subscribe to static event. 
        // Note: Even if the Instance is null (scene not loaded), the static event exists.
        PlayerPrescriptionTracker.OnActiveOrderChanged += OnActiveOrderChangedHandler;
        Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed to PlayerPrescriptionTracker.OnActiveOrderChanged.");

        if (makeActiveButton != null)
        {
            makeActiveButton.onClick.AddListener(OnMakeActiveButtonClick);
        }
        if (markReadyButton != null)
        {
            markReadyButton.onClick.AddListener(OnMarkReadyButtonClick);
        }

        if (upgradeManager != null)
        {
            upgradeManager.OnUpgradePurchasedSuccessfully += OnUpgradePurchasedHandler;
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed to UpgradeManager.OnUpgradePurchasedSuccessfully.");
        }

        UpdateButtonStates();
    }

    public void OnPanelDeactivated()
    {
        Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Panel Deactivated. Returning order list buttons to pool.", this);

        if (orderDetailArea != null)
        {
            orderDetailArea.SetActive(false);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
        }

        RemoveSortButtonListeners();

        // Unsubscribe from static event
        PlayerPrescriptionTracker.OnActiveOrderChanged -= OnActiveOrderChangedHandler;
        Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed from PlayerPrescriptionTracker.OnActiveOrderChanged.");

        if (makeActiveButton != null)
        {
            makeActiveButton.onClick.RemoveAllListeners();
        }
        if (markReadyButton != null)
        {
            markReadyButton.onClick.RemoveAllListeners();
        }

        if (upgradeManager != null)
        {
            upgradeManager.OnUpgradePurchasedSuccessfully -= OnUpgradePurchasedHandler;
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed from UpgradeManager.OnUpgradePurchasedSuccessfully.");
        }

        if (displayedOrders != null)
        {
            displayedOrders.Clear();
        }
        currentlyHighlightedButton = null;
        currentDetailOrder = null;
        ClearOrderList();
    }

    private void AddSortButtonListeners()
    {
        if (patientNameSortButton != null) patientNameSortButton.onClick.AddListener(() => OnSortButtonClick(SortCriterion.PatientName));
        if (prescribedDrugSortButton != null) prescribedDrugSortButton.onClick.AddListener(() => OnSortButtonClick(SortCriterion.PrescribedDrug));
        if (amountSortButton != null) amountSortButton.onClick.AddListener(() => OnSortButtonClick(SortCriterion.Amount));
    }

    private void RemoveSortButtonListeners()
    {
        if (patientNameSortButton != null) patientNameSortButton.onClick.RemoveAllListeners();
        if (prescribedDrugSortButton != null) prescribedDrugSortButton.onClick.RemoveAllListeners();
        if (amountSortButton != null) amountSortButton.onClick.RemoveAllListeners();
    }

    private void FetchAndPopulateOrders()
    {
        if (orderListContentParent == null || prescriptionManager == null || prescriptionOrderButtonPrefab == null || PoolingManager.Instance == null)
        {
            Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Essential references missing in FetchAndPopulateOrders. Cannot proceed.", this);
            displayedOrders = new List<PrescriptionOrder>();
            return;
        }

        List<PrescriptionOrder> fetchedOrders = prescriptionManager.GetCurrentlyActiveOrders();

        if (fetchedOrders == null || fetchedOrders.Count == 0)
        {
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: No currently active prescription orders to display.", this);
            displayedOrders = new List<PrescriptionOrder>();
        }
        else
        {
            displayedOrders = new List<PrescriptionOrder>(fetchedOrders);
            ApplySorting();
        }

        UpdateSortArrows();
        PopulateOrderList();
    }

    private void PopulateOrderList()
    {
        if (orderListContentParent == null || prescriptionOrderButtonPrefab == null || PoolingManager.Instance == null || displayedOrders == null)
        {
            Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Essential references missing in PopulateOrderList. Cannot proceed.", this);
            return;
        }

        // Ensure we have the latest reference
        if (playerPrescriptionTracker == null) playerPrescriptionTracker = PlayerPrescriptionTracker.Instance;

        PrescriptionOrder? currentPlayerActiveOrder = playerPrescriptionTracker?.ActivePrescriptionOrder;
        currentlyHighlightedButton = null;

        for (int i = 0; i < displayedOrders.Count; i++)
        {
            var order = displayedOrders[i];
            GameObject buttonInstance = PoolingManager.Instance.GetPooledObject(prescriptionOrderButtonPrefab);
            if (buttonInstance == null) continue;

            buttonInstance.transform.SetParent(orderListContentParent, false);
            buttonInstance.transform.SetSiblingIndex(i);
            activeButtonInstances.Add(buttonInstance);

            PrescriptionOrderButtonData buttonData = buttonInstance.GetComponent<PrescriptionOrderButtonData>();
            if (buttonData != null)
            {
                buttonData.order = order;
                if (currentPlayerActiveOrder.HasValue && buttonData.order.Equals(currentPlayerActiveOrder.Value))
                {
                    buttonData.SetHighlight(true);
                    currentlyHighlightedButton = buttonInstance;
                }
                else
                {
                    buttonData.SetHighlight(false);
                }
            }

            TextMeshProUGUI[] texts = buttonInstance.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text.gameObject.name == "PatientNameText") text.text = order.patientName;
                else if (text.gameObject.name == "DrugNameText") text.text = order.prescribedDrug;
                else if (text.gameObject.name == "WorthText") text.text = $"${order.moneyWorth:F2}";
            }

            Button button = buttonInstance.GetComponent<Button>();
            if (button != null && buttonData != null)
            {
                button.onClick.AddListener(() => ShowOrderDetails(buttonData.order));
            }
        }
    }

    private void ClearOrderList()
    {
        if (PoolingManager.Instance == null)
        {
            Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PoolingManager instance is null! Cannot return pooled objects.", this);
            foreach (var child in activeButtonInstances.ToList()) Destroy(child);
            activeButtonInstances.Clear();
            return;
        }
        foreach (GameObject child in activeButtonInstances.ToList())
        {
            if (child != null) PoolingManager.Instance.ReturnPooledObject(child);
        }
        activeButtonInstances.Clear();
        currentlyHighlightedButton = null;
        currentDetailOrder = null;
    }

    public void ShowOrderDetails(PrescriptionOrder order)
    {
        currentDetailOrder = order;
        if (orderDetailArea == null || orderDetailText == null) return;
        orderDetailArea.SetActive(true);

        string randomNote = "No notes available.";
        if (doctorsNotesLibrary != null && doctorsNotesLibrary.Count > 0)
        {
            randomNote = doctorsNotesLibrary[Random.Range(0, doctorsNotesLibrary.Count)];
        }

        StringBuilder detailBuilder = new StringBuilder();
        detailBuilder.AppendLine("--- Prescription Details ---").AppendLine($"Patient: {order.patientName}")
            .AppendLine($"Drug: {order.prescribedDrug}").AppendLine($"Dose: {order.dosePerDay} per day")
            .AppendLine($"Treatment Length: {order.lengthOfTreatmentDays} days").AppendLine($"Estimated Worth: ${order.moneyWorth:F2}")
            .AppendLine("\n--- Doctor's Notes ---").AppendLine(randomNote);

        orderDetailText.text = detailBuilder.ToString();
        UpdateButtonStates();
    }

    public void RefreshOrderList()
    {
        if (orderDetailArea != null) orderDetailArea.SetActive(false);
        ClearOrderList();
        FetchAndPopulateOrders();
        UpdateButtonStates();
    }

    private void OnSortButtonClick(SortCriterion clickedCriterion)
    {
        if (currentSortCriterion == clickedCriterion)
        {
            currentSortDirection = (currentSortDirection == SortDirection.Ascending) ? SortDirection.Descending : SortDirection.Ascending;
        }
        else
        {
            currentSortCriterion = clickedCriterion;
            currentSortDirection = SortDirection.Ascending;
        }
        if (orderDetailArea != null) orderDetailArea.SetActive(false);
        ApplySorting();
        ClearOrderList();
        PopulateOrderList();
        UpdateSortArrows();
        UpdateButtonStates();
    }

    private void ApplySorting()
    {
        if (displayedOrders == null || displayedOrders.Count <= 1) return;
        IOrderedEnumerable<PrescriptionOrder> sortedOrders;
        switch (currentSortCriterion)
        {
            case SortCriterion.PatientName:
                sortedOrders = (currentSortDirection == SortDirection.Ascending) ? displayedOrders.OrderBy(o => o.patientName) : displayedOrders.OrderByDescending(o => o.patientName);
                break;
            case SortCriterion.PrescribedDrug:
                sortedOrders = (currentSortDirection == SortDirection.Ascending) ? displayedOrders.OrderBy(o => o.prescribedDrug) : displayedOrders.OrderByDescending(o => o.prescribedDrug);
                break;
            case SortCriterion.Amount:
                sortedOrders = (currentSortDirection == SortDirection.Ascending) ? displayedOrders.OrderBy(o => o.moneyWorth) : displayedOrders.OrderByDescending(o => o.moneyWorth);
                break;
            default:
                sortedOrders = displayedOrders.OrderBy(o => o.patientName);
                break;
        }
        displayedOrders = sortedOrders.ToList();
    }

    private void UpdateSortArrows()
    {
        const string UP_ARROW = " ▲"; const string DOWN_ARROW = " ▼"; const string NO_ARROW = "";
        if (patientNameArrowText != null) patientNameArrowText.text = NO_ARROW;
        if (prescribedDrugArrowText != null) prescribedDrugArrowText.text = NO_ARROW;
        if (amountArrowText != null) amountArrowText.text = NO_ARROW;
        string arrow = (currentSortDirection == SortDirection.Ascending) ? UP_ARROW : DOWN_ARROW;
        switch (currentSortCriterion)
        {
            case SortCriterion.PatientName: if (patientNameArrowText != null) patientNameArrowText.text = arrow; break;
            case SortCriterion.PrescribedDrug: if (prescribedDrugArrowText != null) prescribedDrugArrowText.text = arrow; break;
            case SortCriterion.Amount: if (amountArrowText != null) amountArrowText.text = arrow; break;
        }
    }

    private void OnActiveOrderChangedHandler(PrescriptionOrder? newActiveOrder)
    {
        if (currentlyHighlightedButton != null)
        {
            var oldButtonData = currentlyHighlightedButton.GetComponent<PrescriptionOrderButtonData>();
            if (oldButtonData != null) oldButtonData.SetHighlight(false);
            currentlyHighlightedButton = null;
        }

        if (newActiveOrder.HasValue)
        {
            foreach (GameObject buttonInstance in activeButtonInstances)
            {
                var buttonData = buttonInstance?.GetComponent<PrescriptionOrderButtonData>();
                if (buttonData != null && buttonData.order.Equals(newActiveOrder.Value))
                {
                    buttonData.SetHighlight(true);
                    currentlyHighlightedButton = buttonInstance;
                    break;
                }
            }
        }
        if (PlayerUIPopups.Instance != null)
        {
            if (newActiveOrder.HasValue) PlayerUIPopups.Instance.ShowPopup("Prescription Order", newActiveOrder.Value.ToString());
            else PlayerUIPopups.Instance.HidePopup("Prescription Order");
        }
        UpdateButtonStates();
    }

    private void OnUpgradePurchasedHandler(UpgradeDetailsSO purchasedUpgrade)
    {
        Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Received upgrade purchase event. Re-checking button states.");
        UpdateButtonStates();
    }

    private void OnMakeActiveButtonClick()
    {
        // --- Runtime Lookup Check ---
        if (playerPrescriptionTracker == null) playerPrescriptionTracker = PlayerPrescriptionTracker.Instance;

        if (playerPrescriptionTracker == null)
        {
            Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PlayerPrescriptionTracker not found! Cannot set active order.", this);
            return;
        }
        if (!currentDetailOrder.HasValue)
        {
            return;
        }
        playerPrescriptionTracker.SetActiveOrder(currentDetailOrder.Value);
    }

    private void OnMarkReadyButtonClick()
    {
        // --- Runtime Lookup Check ---
        if (playerPrescriptionTracker == null) playerPrescriptionTracker = PlayerPrescriptionTracker.Instance;

        if (playerPrescriptionTracker == null)
        {
            Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PlayerPrescriptionTracker not found! Cannot mark order ready.", this);
            return;
        }
        if (!playerPrescriptionTracker.ActivePrescriptionOrder.HasValue)
        {
            return;
        }

        PrescriptionOrder activeOrder = playerPrescriptionTracker.ActivePrescriptionOrder.Value;
        ItemDetails expectedItemDetails = prescriptionManager?.GetExpectedOutputItemDetails(activeOrder);
        if (expectedItemDetails == null)
        {
            return;
        }

        GameObject playerToolbarGO = GameObject.FindGameObjectWithTag("PlayerToolbarInventory");
        Inventory playerInventory = playerToolbarGO?.GetComponent<Inventory>();
        if (playerInventory?.Combiner?.InventoryState == null)
        {
            return;
        }

        Item matchingItemInstance = null;
        Item[] inventoryItems = playerInventory.Combiner.InventoryState.GetCurrentArrayState();
        for (int i = 0; i < playerInventory.Combiner.PhysicalSlotCount; i++)
        {
            Item itemInSlot = inventoryItems[i];
            if (itemInSlot?.details != null)
            {
                if (itemInSlot.details == expectedItemDetails && itemInSlot.details.itemLabel == ItemLabel.PrescriptionPrepared && itemInSlot.patientNameTag.Equals(activeOrder.patientName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingItemInstance = itemInSlot;
                    break;
                }
            }
        }

        if (matchingItemInstance != null)
        {
            prescriptionManager.MarkOrderReady(activeOrder);
            playerPrescriptionTracker.ClearActiveOrder();
            Debug.Log($"Prescription for {activeOrder.patientName} is ready for delivery!");
        }
        else
        {
            PlayerUIPopups.Instance?.ShowPopup("ToolbarPopup", $"I don't have the prescription for {activeOrder.patientName}!");
        }
    }

    private void UpdateButtonStates()
    {
        bool hasPremiumSoftware = false;
        if (upgradeManager != null && !string.IsNullOrEmpty(premiumSoftwareUpgradeName))
        {
            UpgradeDetailsSO prepUpgrade = upgradeManager.GetUpgradeDetailsByName(premiumSoftwareUpgradeName);
            if (prepUpgrade != null)
            {
                hasPremiumSoftware = upgradeManager.IsUpgradePurchased(prepUpgrade);
            }
            else
            {
                Debug.LogWarning($"PrescOrdersPanelHandler: Could not find an upgrade with the name '{premiumSoftwareUpgradeName}'. Buttons will be disabled.", this);
            }
        }

        // Ensure we check for the instance if possible
        if (playerPrescriptionTracker == null) playerPrescriptionTracker = PlayerPrescriptionTracker.Instance;

        PrescriptionOrder? currentPlayerActiveOrder = playerPrescriptionTracker?.ActivePrescriptionOrder;

        if (makeActiveButton != null)
        {
            makeActiveButton.interactable = hasPremiumSoftware && currentDetailOrder.HasValue && !currentPlayerActiveOrder.HasValue;
        }

        if (markReadyButton != null)
        {
            markReadyButton.interactable = hasPremiumSoftware && currentPlayerActiveOrder.HasValue;
        }
    }

    private void Awake()
    {
        // Get Manager singleton instances
        prescriptionManager = PrescriptionManager.Instance;
        upgradeManager = UpgradeManager.Instance;

        if (prescriptionManager == null) Debug.LogError("PrescOrdersPanelHandler: PrescriptionManager.Instance not found!", this);
        if (upgradeManager == null) Debug.LogError("PrescOrdersPanelHandler: UpgradeManager.Instance not found!", this);

        activeButtonInstances = new List<GameObject>();
        displayedOrders = new List<PrescriptionOrder>();
        currentDetailOrder = null;
        
        // Removed the check for PlayerPrescriptionTracker here because it might not be loaded yet in Awake
    }
}