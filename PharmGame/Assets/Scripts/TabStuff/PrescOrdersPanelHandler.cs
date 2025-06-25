// --- START OF FILE PrescOrdersPanelHandler.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Systems.UI;
using Game.Prescriptions; // Needed for PrescriptionOrder and PrescriptionManager
using System.Linq; // Needed for LINQ sorting
using System.Text;
using Random = UnityEngine.Random; // Specify UnityEngine.Random to avoid ambiguity
using Utils.Pooling; // Needed for PoolingManager
using Systems.Player; // Needed for PlayerPrescriptionTracker
using System; // Needed for Action, Nullable

// Make sure this script is in a namespace if you are using them consistently
// namespace Systems.UI // Example namespace
// {

    /// <summary>
    /// Handles the logic for the Prescription Orders panel in the computer UI.
    /// Displays a list of orders and a detail view for selected orders.
    /// MODIFIED: Fetches only currently active orders from PrescriptionManager.
    /// MODIFIED: Subscribes to PlayerPrescriptionTracker events for highlighting. // <-- Added note
    /// </summary>
    public class PrescOrdersPanelHandler : MonoBehaviour, IPanelActivatable
    {
        // NEW: Enums to define sorting criteria and direction
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

        // --- Doctor's Notes Library ---
        [Header("Doctor's Notes")]
        [Tooltip("A list of possible doctor's notes to randomly select from for the detail view.")]
        [SerializeField] private List<string> doctorsNotesLibrary = new List<string>();

        // --- Controls ---
        [Header("Controls")]
        [Tooltip("Button to refresh the list of orders.")]
        [SerializeField] private Button refreshButton;

        // MODIFIED: References for the sorting buttons - now assigned in Inspector
        [Tooltip("Button to sort the list by Patient Name (Assign in Inspector).")]
        [SerializeField] private Button patientNameSortButton;
        [Tooltip("Button to sort the list by Prescribed Drug (Assign in Inspector).")]
        [SerializeField] private Button prescribedDrugSortButton;
        [Tooltip("Button to sort the list by Amount (Assign in Inspector).")]
        [SerializeField] private Button amountSortButton;

        // MODIFIED: References for the arrow text components - now assigned in Inspector
        [Tooltip("TextMeshProUGUI for the arrow on the Patient Name sort button (Assign in Inspector).")]
        [SerializeField] private TextMeshProUGUI patientNameArrowText;
        [Tooltip("TextMeshProUGUI for the arrow on the Prescribed Drug sort button (Assign in Inspector).")]
        [SerializeField] private TextMeshProUGUI prescribedDrugArrowText;
        [Tooltip("TextMeshProUGUI for the arrow on the Amount sort button (Assign in Inspector).")]
        [SerializeField] private TextMeshProUGUI amountArrowText;


        // --- Manager References ---
        private PrescriptionManager prescriptionManager; // Reference to the PrescriptionManager singleton
        private PlayerPrescriptionTracker playerPrescriptionTracker; // Reference to PlayerPrescriptionTracker

        // --- Internal Tracking ---
        // List to keep track of INSTANTIATED/POOLED buttons currently active in the scene
        private List<GameObject> activeButtonInstances = new List<GameObject>();

        // NEW: Internal list to hold the orders currently being displayed/sorted
        private List<PrescriptionOrder> displayedOrders;

        // NEW: Fields to track the current sorting state
        private SortCriterion currentSortCriterion = SortCriterion.PatientName; // Default sort criterion
        private SortDirection currentSortDirection = SortDirection.Ascending;   // Default sort direction

        // NEW: Field to track the currently highlighted button
        private GameObject currentlyHighlightedButton = null;


        // --- IPanelActivatable Implementation ---

        /// <summary>
        /// Called by the TabManager when this panel becomes active.
        /// This is where we will populate the list of orders using pooling.
        /// MODIFIED: Subscribes to PlayerPrescriptionTracker.OnActiveOrderChanged. // <-- Added note
        /// </summary>
        public void OnPanelActivated()
        {
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Panel Activated. Attempting to populate order list.", this);

            // --- Validation: Check essential UI references ---
             // Keep checks for references that MUST be assigned for basic functionality
             if (orderDetailArea == null || orderListContentParent == null || prescriptionOrderButtonPrefab == null)
             {
                 Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Essential UI references missing in OnPanelActivated. Cannot proceed.", this);
                 if (orderDetailArea != null) orderDetailArea.SetActive(false);
                 return;
             }

            // REMOVED: Call to FindSortButtonReferences() - references are now assigned in Inspector

            // Ensure detail area is hidden when the panel is first activated
            orderDetailArea.SetActive(false);

            // Clear any existing buttons before repopulating (returns them to pool)
            ClearOrderList(); // Call the cleanup method

            // NEW: Fetch, sort, and populate the list
            FetchAndPopulateOrders(); // Call the new method

            // --- Add Listener for Refresh Button ---
             if (refreshButton != null)
             {
                  refreshButton.onClick.AddListener(RefreshOrderList);
                  Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed Refresh button listener.");
             }
             else
             {
                  Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Refresh Button reference is null! Cannot subscribe listener.", this);
             }

             // NEW: Add Listeners for Sort Buttons - Now uses Inspector-assigned references
             AddSortButtonListeners(); // Call the helper method

             // NEW: Subscribe to PlayerPrescriptionTracker event
             if (playerPrescriptionTracker != null)
             {
                 PlayerPrescriptionTracker.OnActiveOrderChanged += OnActiveOrderChangedHandler;
                 Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed to PlayerPrescriptionTracker.OnActiveOrderChanged.");
             }
             else
             {
                 Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: PlayerPrescriptionTracker reference is null! Cannot subscribe to active order changes.", this);
             }
        }

        /// <summary>
        /// Called by the TabManager when this panel becomes inactive.
        /// This is where we will return the generated buttons to the pool.
        /// MODIFIED: Unsubscribes from PlayerPrescriptionTracker.OnActiveOrderChanged. // <-- Added note
        /// </summary>
        public void OnPanelDeactivated()
        {
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Panel Deactivated. Returning order list buttons to pool.", this);

            // Hide the detail area when the panel is deactivated
             if (orderDetailArea != null)
             {
                  orderDetailArea.SetActive(false);
             }
             else
             {
                  Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Order Detail Area reference is null! Cannot hide detail area.", this);
             }

            // --- Remove Listener for Refresh Button ---
            if (refreshButton != null)
            {
                 refreshButton.onClick.RemoveAllListeners(); // Remove all listeners added to this button
                 Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed Refresh button listeners.");
            }
            else
            {
                 Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Refresh Button reference is null! Cannot unsubscribe listener.", this);
            }

            // NEW: Remove Listeners for Sort Buttons - Now uses Inspector-assigned references
            RemoveSortButtonListeners(); // Call the helper method

            // NEW: Unsubscribe from PlayerPrescriptionTracker event
            if (playerPrescriptionTracker != null)
            {
                PlayerPrescriptionTracker.OnActiveOrderChanged -= OnActiveOrderChangedHandler;
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed from PlayerPrescriptionTracker.OnActiveOrderChanged.");
            }

            // Return instantiated buttons to the pool
            ClearOrderList(); // Call the cleanup method

            // NEW: Clear the internal displayedOrders list
            if (displayedOrders != null)
            {
                displayedOrders.Clear();
            }

            // NEW: Clear the currently highlighted button reference
            currentlyHighlightedButton = null;

            // REMOVED: Call to ClearSortButtonReferences() - references are not cleared as they are Inspector assigned
        }

        // REMOVED: FindSortButtonReferences() method - references are now assigned in Inspector
        // private void FindSortButtonReferences() { ... }

        // REMOVED: ClearSortButtonReferences() method - references are not cleared as they are Inspector assigned
        // private void ClearSortButtonReferences() { ... }


        // MODIFIED: Helper method to add listeners to sort buttons - uses Inspector-assigned references
        private void AddSortButtonListeners()
        {
            // Add listeners only if the references were assigned in the Inspector
            if (patientNameSortButton != null)
            {
                patientNameSortButton.onClick.AddListener(() => OnSortButtonClick(SortCriterion.PatientName));
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed Patient Name sort button listener.");
            }
            else Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Patient Name Sort Button reference is null! Cannot subscribe listener.", this);

            if (prescribedDrugSortButton != null)
            {
                prescribedDrugSortButton.onClick.AddListener(() => OnSortButtonClick(SortCriterion.PrescribedDrug));
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed Prescribed Drug sort button listener.");
            }
            else Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Prescribed Drug Sort Button reference is null! Cannot subscribe listener.", this);

            if (amountSortButton != null)
            {
                amountSortButton.onClick.AddListener(() => OnSortButtonClick(SortCriterion.Amount));
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Subscribed Amount sort button listener.");
            }
            else Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Amount Sort Button reference is null! Cannot subscribe listener.", this);
        }

        // MODIFIED: Helper method to remove listeners from sort buttons - uses Inspector-assigned references
        private void RemoveSortButtonListeners()
        {
            // Remove listeners only if the references were assigned in the Inspector
            if (patientNameSortButton != null)
            {
                patientNameSortButton.onClick.RemoveAllListeners();
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed Patient Name sort button listeners.");
            }
            if (prescribedDrugSortButton != null)
            {
                prescribedDrugSortButton.onClick.RemoveAllListeners();
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed Prescribed Drug sort button listeners.");
            }
            if (amountSortButton != null)
            {
                amountSortButton.onClick.RemoveAllListeners();
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Unsubscribed Amount sort button listeners.");
            }
        }


        /// <summary>
        /// Fetches orders, applies current sorting, and populates the list UI.
        /// Called on panel activation and refresh.
        /// MODIFIED: Now uses GetCurrentlyActiveOrders().
        /// </summary>
        private void FetchAndPopulateOrders()
        {
             // --- Validation: Check essential references ---
             if (orderListContentParent == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order List Content Parent Transform is null! Cannot populate list.", this);
                  // Initialize displayedOrders as empty list to avoid null reference later
                  displayedOrders = new List<PrescriptionOrder>();
                  return;
             }
             if (prescriptionManager == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PrescriptionManager reference is null! Cannot get order data.", this);
                  // Initialize displayedOrders as empty list to avoid null reference later
                  displayedOrders = new List<PrescriptionOrder>();
                  return;
             }
             if (prescriptionOrderButtonPrefab == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Prescription Order Button Prefab is not assigned! Cannot instantiate buttons.", this);
                  // Initialize displayedOrders as empty list to avoid null reference later
                  displayedOrders = new List<PrescriptionOrder>();
                  return;
             }
             if (PoolingManager.Instance == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PoolingManager instance is null! Cannot get pooled objects.", this);
                  // Initialize displayedOrders as empty list to avoid null reference later
                  displayedOrders = new List<PrescriptionOrder>();
                  return;
             }

             // Fetch orders from the manager
             // --- MODIFIED: Use the new method to get only active orders ---
             List<PrescriptionOrder> fetchedOrders = prescriptionManager.GetCurrentlyActiveOrders();
             // --- END MODIFIED ---


             if (fetchedOrders == null || fetchedOrders.Count == 0)
             {
                  Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: No currently active prescription orders to display.", this);
                  displayedOrders = new List<PrescriptionOrder>(); // Ensure list is not null, even if empty
                  // Optionally display a "No orders" message in the list area
             }
             else
             {
                 // Store the fetched orders in the internal list
                 displayedOrders = new List<PrescriptionOrder>(fetchedOrders); // Create a copy

                 // Apply the current sorting
                 ApplySorting(); // Call the new sorting method

                 Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Found {displayedOrders.Count} active orders. Getting pooled buttons.");
             }

             // Update the sort arrows display
             UpdateSortArrows(); // Call the new arrow update method

             // Populate the UI list with the sorted orders
             PopulateOrderList();
        }


        /// <summary>
        /// Populates the order list UI by getting pooled buttons and setting their data.
        /// Assumes displayedOrders list is already populated and sorted.
        /// MODIFIED: Checks and applies highlighting based on the player's active order.
        /// </summary>
        private void PopulateOrderList()
        {
             // --- Validation: Check essential references ---
             if (orderListContentParent == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order List Content Parent Transform is null! Cannot populate list.", this);
                  return;
             }
             if (prescriptionOrderButtonPrefab == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Prescription Order Button Prefab is not assigned! Cannot instantiate buttons.", this);
                  return;
             }
             if (PoolingManager.Instance == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PoolingManager instance is null! Cannot get pooled objects.", this);
                  return;
             }
             if (displayedOrders == null) // Check the internal list
             {
                 Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: displayedOrders list is null! Cannot populate UI.", this);
                 return;
             }

             // NEW: Get the player's current active order once before the loop
             PrescriptionOrder? currentPlayerActiveOrder = playerPrescriptionTracker?.ActivePrescriptionOrder;
             currentlyHighlightedButton = null; // Reset the tracking reference


             // Ensure the tracking list is empty before populating
             // This should be handled by ClearOrderList being called first
             // activeButtonInstances.Clear();


             // MODIFIED: Use a for loop to get the index
             for (int i = 0; i < displayedOrders.Count; i++)
             {
                 var order = displayedOrders[i]; // Get the order at the current index

                 GameObject buttonInstance = PoolingManager.Instance.GetPooledObject(prescriptionOrderButtonPrefab);

                 if (buttonInstance == null)
                 {
                      Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Failed to get a pooled instance of '{prescriptionOrderButtonPrefab.name}'. Skipping order: {order.patientName}", this);
                      continue; // Skip to the next order
                 }

                 // Set the parent of the pooled object AFTER getting it from the pool
                 buttonInstance.transform.SetParent(orderListContentParent, false); // Use SetParent(parent, worldPositionStays)

                 // NEW: Explicitly set the sibling index
                 buttonInstance.transform.SetSiblingIndex(i); // Set the position in the hierarchy based on the loop index


                 // --- Add the instance to our tracking list ---
                 activeButtonInstances.Add(buttonInstance);


                 // Get the helper script and assign the order data
                 PrescriptionOrderButtonData buttonData = buttonInstance.GetComponent<PrescriptionOrderButtonData>();
                 if (buttonData != null)
                 {
                     buttonData.order = order;

                     // NEW: Check if this button's order matches the player's active order and apply highlighting
                     if (currentPlayerActiveOrder.HasValue && buttonData.order.Equals(currentPlayerActiveOrder.Value)) // Assuming PrescriptionOrder has Equals or == override
                     {
                         buttonData.SetHighlight(true);
                         currentlyHighlightedButton = buttonInstance; // Track this button as highlighted
                         Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Highlighted button for active order: {order.patientName}", buttonInstance);
                     }
                     else
                     {
                         buttonData.SetHighlight(false); // Ensure non-active buttons are not highlighted
                     }
                 }
                 else
                 {
                     Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Pooled button instance '{buttonInstance.name}' is missing PrescriptionOrderButtonData component! Cannot assign order data or manage highlight.", buttonInstance);
                 }

                 // Find the TextMeshProUGUI components and set their text
                 // NOTE: This part assumes the text components have consistent names within the prefab
                 TextMeshProUGUI[] texts = buttonInstance.GetComponentsInChildren<TextMeshProUGUI>();
                 TextMeshProUGUI patientNameText = null;
                 TextMeshProUGUI drugNameText = null;
                 TextMeshProUGUI worthText = null;

                 foreach (var text in texts)
                 {
                     if (text.gameObject.name == "PatientNameText") patientNameText = text;
                     else if (text.gameObject.name == "DrugNameText") drugNameText = text;
                     else if (text.gameObject.name == "WorthText") worthText = text;
                 }

                 if (patientNameText != null) patientNameText.text = order.patientName;
                 else Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: PatientNameText not found on button instance.", buttonInstance);

                 if (drugNameText != null) drugNameText.text = order.prescribedDrug;
                 else Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: DrugNameText not found on button instance.", buttonInstance);

                 if (worthText != null) worthText.text = $"${order.moneyWorth:F2}"; // Format money
                 else Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: WorthText not found on button instance.", buttonInstance);


                 // Get the Button component and add the click listener
                 Button button = buttonInstance.GetComponent<Button>();
                 if (button != null)
                 {
                     if (buttonData != null)
                     {
                          // Note: Using the 'order' variable directly inside the lambda captures the *last* value
                          // from the loop in older C# versions. Using a local variable copy is safer.
                          // However, since we're using the buttonData.order which is set uniquely for each instance,
                          // this specific lambda capture issue is avoided here.
                          button.onClick.AddListener(() => ShowOrderDetails(buttonData.order));
                     }
                     else
                     {
                          Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Cannot add click listener, buttonData is null for instance!", buttonInstance);
                     }
                 }
                 else
                 {
                     Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Button component not found on button instance! Cannot add click listener.", buttonInstance);
                 }
             }

             Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Finished populating order list using pooling. Active instances tracked: {activeButtonInstances.Count}.");
        }

        /// <summary>
        /// Returns all instantiated order button GameObjects managed by this handler to the pool.
        /// </summary>
        private void ClearOrderList()
        {
             // --- Validation: Check PoolingManager instance ---
             if (PoolingManager.Instance == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PoolingManager instance is null! Cannot return pooled objects. Destroying tracked objects instead.", this);
                  // Fallback to destroying if pooling manager is missing (less efficient)
                  DestroyAllTrackedOrderListChildren(); // Call a new helper method for destruction fallback
                  return; // Stop here
             }

             Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Returning {activeButtonInstances.Count} tracked buttons to pool.", this);

             // Return all tracked instances to the pool
             // Iterate over a copy (ToList()) is safer if the Return call might somehow affect the original list
             foreach (GameObject child in activeButtonInstances.ToList()) // Use ToList()
             {
                 if (child != null)
                 {
                     // The PoolingManager.ReturnPooledObject handles setting inactive and reparenting
                     PoolingManager.Instance.ReturnPooledObject(child);
                 }
                 else
                 {
                     Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Found a null entry in activeButtonInstances list.", this);
                 }
             }

             // --- Clear the tracking list AFTER returning all objects ---
             activeButtonInstances.Clear();

             // NEW: Clear the currently highlighted button reference when clearing the list
             currentlyHighlightedButton = null;

             Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Returned order list buttons to pool. Active instances tracked: {activeButtonInstances.Count}.");
        }

        /// <summary>
        /// Fallback method to destroy all children currently tracked by this handler.
        /// Used if pooling is not available.
        /// </summary>
        private void DestroyAllTrackedOrderListChildren() // Renamed for clarity
        {
             Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Destroying {activeButtonInstances.Count} tracked children as fallback.", this);

             // Iterate over a copy (ToList()) to safely destroy objects
             foreach (GameObject child in activeButtonInstances.ToList()) // Use ToList()
             {
                 if (child != null)
                 {
                     Destroy(child);
                 }
             }

             // Clear the tracking list after destroying
             activeButtonInstances.Clear();

             // NEW: Clear the currently highlighted button reference when destroying
             currentlyHighlightedButton = null;

             Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Destroyed tracked children. Active instances tracked: {activeButtonInstances.Count}.", this);
        }


        /// <summary>
        /// Displays the full details of a selected prescription order in the detail area.
        /// </summary>
        /// <param name="order">The PrescriptionOrder struct to display.</param>
        public void ShowOrderDetails(PrescriptionOrder order)
        {
             Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Showing details for order: {order.patientName} - {order.prescribedDrug}", this);

             // --- Validation: Check essential UI references ---
             if (orderDetailArea == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order Detail Area reference is null! Cannot show details.", this);
                  return;
             }
             if (orderDetailText == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order Detail TextMeshProUGUI reference is null! Cannot show details.", this);
                  return;
             }

             // Activate the detail area GameObject
             orderDetailArea.SetActive(true);

             // Select a random doctor's note
             string randomNote = "No notes available."; // Default if library is empty
             // --- Validation: Check doctor's notes library ---
             if (doctorsNotesLibrary != null && doctorsNotesLibrary.Count > 0)
             {
                  randomNote = doctorsNotesLibrary[Random.Range(0, doctorsNotesLibrary.Count)];
             }
             else
             {
                  Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Doctor's notes library is empty. Using default note.", this);
             }

             // Build the detail text string
             StringBuilder detailBuilder = new StringBuilder();
             detailBuilder.AppendLine("--- Prescription Details ---");
             detailBuilder.AppendLine($"Patient: {order.patientName}");
             detailBuilder.AppendLine($"Drug: {order.prescribedDrug}");
             detailBuilder.AppendLine($"Dose: {order.dosePerDay} per day");
             detailBuilder.AppendLine($"Treatment Length: {order.lengthOfTreatmentDays} days");
             detailBuilder.AppendLine($"Estimated Worth: ${order.moneyWorth:F2}");
             detailBuilder.AppendLine("\n--- Doctor's Notes ---"); // Add a separator
             detailBuilder.AppendLine(randomNote); // Add the random note

             // Set the text component's text
             orderDetailText.text = detailBuilder.ToString();

             Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Order details displayed.");
        }


        /// <summary>
        /// Clears and repopulates the order list. Called by the Refresh button.
        /// </summary>
        public void RefreshOrderList()
        {
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Refreshing order list.", this);

            // --- Validation: Check essential UI references ---
             if (orderDetailArea == null || orderListContentParent == null || prescriptionOrderButtonPrefab == null || PoolingManager.Instance == null)
             {
                 Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Essential references missing in RefreshOrderList. Cannot proceed.", this);
                 // Still try to hide detail area if possible
                 if (orderDetailArea != null) orderDetailArea.SetActive(false);
                 return; // Stop if critical references are missing
             }

            // Hide the detail area when refreshing
            orderDetailArea.SetActive(false);

            // Clear existing buttons (returns them to pool)
            ClearOrderList();

            // NEW: Fetch, sort (keeping current sort state), and repopulate the list
            FetchAndPopulateOrders(); // Use the new method

            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Order list refreshed. New active instances tracked: {activeButtonInstances.Count}.");
        }

        // NEW: Method to handle sort button clicks
        private void OnSortButtonClick(SortCriterion clickedCriterion)
        {
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Sort button clicked for criterion: {clickedCriterion}.", this);

            // If the same criterion is clicked, toggle direction
            if (currentSortCriterion == clickedCriterion)
            {
                currentSortDirection = (currentSortDirection == SortDirection.Ascending) ? SortDirection.Descending : SortDirection.Ascending;
                Debug.Log($"Toggling sort direction to: {currentSortDirection}");
            }
            // If a different criterion is clicked, set it and default to Ascending
            else
            {
                currentSortCriterion = clickedCriterion;
                currentSortDirection = SortDirection.Ascending;
                Debug.Log($"Changing sort criterion to: {currentSortCriterion}, defaulting direction to: {currentSortDirection}");
            }

            // Hide detail area when sorting
            if (orderDetailArea != null)
            {
                 orderDetailArea.SetActive(false);
            }

            // Apply the new sorting, clear and repopulate the UI
            ApplySorting(); // Sort the internal list
            ClearOrderList(); // Return old buttons to pool
            PopulateOrderList(); // Create new buttons from sorted list
            UpdateSortArrows(); // Update the arrow indicators
        }

        // NEW: Method to apply the current sorting to the displayedOrders list
        private void ApplySorting()
        {
            if (displayedOrders == null || displayedOrders.Count <= 1)
            {
                // No need to sort if list is null, empty, or has only one item
                return;
            }

            Debug.Log($"Applying sort: Criterion={currentSortCriterion}, Direction={currentSortDirection}");

            // Use LINQ to sort the list
            IOrderedEnumerable<PrescriptionOrder> sortedOrders;

            switch (currentSortCriterion)
            {
                case SortCriterion.PatientName:
                    sortedOrders = (currentSortDirection == SortDirection.Ascending) ?
                                   displayedOrders.OrderBy(order => order.patientName) :
                                   displayedOrders.OrderByDescending(order => order.patientName);
                    break;
                case SortCriterion.PrescribedDrug:
                    sortedOrders = (currentSortDirection == SortDirection.Ascending) ?
                                   displayedOrders.OrderBy(order => order.prescribedDrug) :
                                   displayedOrders.OrderByDescending(order => order.prescribedDrug);
                    break;
                case SortCriterion.Amount:
                    sortedOrders = (currentSortDirection == SortDirection.Ascending) ?
                                   displayedOrders.OrderBy(order => order.moneyWorth) :
                                   displayedOrders.OrderByDescending(order => order.moneyWorth);
                    break;
                default:
                    // Default to Patient Name Ascending if somehow an invalid criterion is set
                    Debug.LogWarning($"Unknown sort criterion: {currentSortCriterion}. Defaulting to Patient Name Ascending.", this);
                    currentSortCriterion = SortCriterion.PatientName;
                    currentSortDirection = SortDirection.Ascending;
                    sortedOrders = displayedOrders.OrderBy(order => order.patientName);
                    break;
            }

            // Assign the sorted list back to displayedOrders
            displayedOrders = sortedOrders.ToList();
        }

        // NEW: Method to update the arrow text on the sort buttons
        private void UpdateSortArrows()
        {
            const string UP_ARROW = " ▲"; // Unicode triangle up with a leading space
            const string DOWN_ARROW = " ▼"; // Unicode triangle down with a leading space
            const string NO_ARROW = "";

            // Clear all arrows first, checking if references are assigned
            if (patientNameArrowText != null) patientNameArrowText.text = NO_ARROW;
            if (prescribedDrugArrowText != null) prescribedDrugArrowText.text = NO_ARROW;
            if (amountArrowText != null) amountArrowText.text = NO_ARROW;

            // Set the arrow for the current sort criterion, checking if references are assigned
            string arrow = (currentSortDirection == SortDirection.Ascending) ? UP_ARROW : DOWN_ARROW;

            switch (currentSortCriterion)
            {
                case SortCriterion.PatientName:
                    if (patientNameArrowText != null) patientNameArrowText.text = arrow;
                    break;
                case SortCriterion.PrescribedDrug:
                    if (prescribedDrugArrowText != null) prescribedDrugArrowText.text = arrow;
                    break;
                case SortCriterion.Amount:
                    if (amountArrowText != null) amountArrowText.text = arrow;
                    break;
            }
        }

        /// <summary>
        /// Handler for the PlayerPrescriptionTracker.OnActiveOrderChanged event.
        /// Updates button highlighting based on the new active order.
        /// </summary>
        /// <param name="newActiveOrder">The new active order, or null if cleared.</param>
        private void OnActiveOrderChangedHandler(PrescriptionOrder? newActiveOrder)
        {
            Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Received OnActiveOrderChanged event. New active order: {(newActiveOrder.HasValue ? newActiveOrder.Value.ToString() : "None")}.", this);

            // 1. Turn off highlighting on the previously highlighted button, if any
            if (currentlyHighlightedButton != null)
            {
                PrescriptionOrderButtonData oldButtonData = currentlyHighlightedButton.GetComponent<PrescriptionOrderButtonData>();
                if (oldButtonData != null)
                {
                    oldButtonData.SetHighlight(false);
                    Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Turned off highlight on previous button.", currentlyHighlightedButton);
                }
                else
                {
                    Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: currentlyHighlightedButton ({currentlyHighlightedButton.name}) is missing PrescriptionOrderButtonData!", currentlyHighlightedButton);
                }
                currentlyHighlightedButton = null; // Clear the reference
            }

            // 2. If a new order was set, find its corresponding button and highlight it
            if (newActiveOrder.HasValue)
            {
                PrescriptionOrder orderToHighlight = newActiveOrder.Value;
                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Searching for button matching new active order: {orderToHighlight.patientName}.", this);

                // Iterate through the currently active button instances
                foreach (GameObject buttonInstance in activeButtonInstances)
                {
                    if (buttonInstance != null)
                    {
                        PrescriptionOrderButtonData buttonData = buttonInstance.GetComponent<PrescriptionOrderButtonData>();
                        if (buttonData != null)
                        {
                            // Compare the order stored in the button data with the new active order
                            // Assuming PrescriptionOrder has a proper Equals implementation or unique identifier
                            if (buttonData.order.Equals(orderToHighlight)) // <-- Requires PrescriptionOrder.Equals or ==
                            {
                                buttonData.SetHighlight(true);
                                currentlyHighlightedButton = buttonInstance; // Track the new highlighted button
                                Debug.Log($"PrescOrdersPanelHandler on {gameObject.name}: Found and highlighted button for new active order: {orderToHighlight.patientName}.", buttonInstance);
                                break; // Found the button, stop searching
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Button instance '{buttonInstance.name}' is missing PrescriptionOrderButtonData component!", buttonInstance);
                        }
                    }
                    else
                    {
                         Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Found null entry in activeButtonInstances list during highlight search.", this);
                    }
                }

                if (currentlyHighlightedButton == null)
                {
                     Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Could not find button matching new active order '{orderToHighlight.patientName}' in the current list.", this);
                }
            }
            // If newActiveOrder is null, we just turned off the old highlight and cleared the reference, which is correct.
        }


        private void Awake()
        {
             // --- Initial UI reference validation in Awake ---
             if (orderListContentParent == null) Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order List Content Parent Transform is not assigned in Awake!", this);
             if (orderDetailArea == null) Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order Detail Area GameObject is not assigned in Awake!", this);
             if (orderDetailText == null) Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Order Detail TextMeshProUGUI is not assigned in Awake!", this);
             if (prescriptionOrderButtonPrefab == null) Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Prescription Order Button Prefab is not assigned in Awake!", this);
             if (refreshButton == null) Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: Refresh Button is not assigned in Awake!", this);

             // NEW: Add warnings for sort button references if NOT assigned in Inspector
             // These are critical for sorting functionality
             if (patientNameSortButton == null) Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Patient Name Sort Button is not assigned in Inspector!", this);
             if (prescribedDrugSortButton == null) Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Prescribed Drug Sort Button is not assigned in Inspector!", this);
             if (amountSortButton == null) Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Amount Sort Button is not assigned in Inspector!", this);
             if (patientNameArrowText == null) Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Patient Name Arrow Text is not assigned in Inspector!", this);
             if (prescribedDrugArrowText == null) Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Prescribed Drug Arrow Text is not assigned in Inspector!", this);
             if (amountArrowText == null) Debug.LogWarning($"PrescOrdersPanelHandler on {gameObject.name}: Amount Arrow Text is not assigned in Inspector!", this);


             // Get Manager singletons instance
             prescriptionManager = PrescriptionManager.Instance;
             if (prescriptionManager == null)
             {
                 Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PrescriptionManager.Instance not found in Awake! Cannot get order data.", this);
                 // Consider disabling the component if this is a critical dependency
                 // enabled = false;
             }

             // NEW: Get PlayerPrescriptionTracker singleton instance (assuming it's a singleton or findable)
             // If PlayerPrescriptionTracker is NOT a singleton, you'll need another way to get its reference,
             // e.g., FindObjectOfType or passed in from a higher-level manager.
             playerPrescriptionTracker = FindObjectOfType<PlayerPrescriptionTracker>(); // Assuming FindObjectOfType works or it's a singleton
             if (playerPrescriptionTracker == null)
             {
                  Debug.LogError($"PrescOrdersPanelHandler on {gameObject.name}: PlayerPrescriptionTracker not found in Awake! Cannot track active order for highlighting.", this);
             }


             // Initialize the tracking lists
             activeButtonInstances = new List<GameObject>();
             displayedOrders = new List<PrescriptionOrder>();
        }

        // --- Placeholder for any other necessary methods ---
    }

// } // End namespace (if using one)