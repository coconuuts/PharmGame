// --- START OF FILE PlayerUIPopups.cs ---

// --- START OF FILE PlayerUIPopups.cs ---

using UnityEngine;
using TMPro; // Required for TMP_Text
using System.Collections; // Required for using Coroutines
using Game.Prescriptions; // Needed for PrescriptionOrder

public class PlayerUIPopups : MonoBehaviour
{
    [Header("Invalid Item Popup")]
    [Tooltip("The root GameObject for the generic 'Invalid Item' popup.")] // Added tooltip for clarity
    [SerializeField] private GameObject invalidItem;

    // --- NEW: Wrong Prescription Popup ---
    [Header("Wrong Prescription Popup")]
    [Tooltip("The root GameObject for the 'Wrong Prescription' popup.")]
    [SerializeField] private GameObject wrongPrescriptionPopupRoot;
    [Tooltip("The TMP Text component to display the 'Wrong Prescription' message.")]
    [SerializeField] private TMPro.TMP_Text wrongPrescriptionMessageText;
    [Tooltip("Duration (in seconds) the 'Wrong Prescription' popup stays visible.")]
    [SerializeField] private float wrongPrescriptionPopupDuration = 3f; // Configurable duration
    // --- END NEW ---


    [Header("Prescription Order UI")]
    [Tooltip("The root GameObject for the prescription order details UI.")]
    [SerializeField] private GameObject prescriptionOrderUIRoot;
    [Tooltip("The TMP Text component to display the prescription order details.")]
    [SerializeField] private TMPro.TMP_Text prescriptionOrderDetailsText;


    private static PlayerUIPopups instance; // Keeping from original script

    private Coroutine disableInvalidItemCoroutine; // To keep track of the running coroutine
    private Coroutine disableWrongPrescriptionCoroutine; // <-- NEW: Coroutine for wrong prescription popup

    private void Awake()
    {
        // Singleton pattern to ensure only one instance exists
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject); // Destroy duplicate
            return;
        }
    }

    public void Start()
    {
        // Ensure popups are initially inactive
        if (invalidItem != null)
        {
            invalidItem.SetActive(false);
        }
        // --- NEW: Ensure wrong prescription UI is initially inactive ---
        if (wrongPrescriptionPopupRoot != null)
        {
            wrongPrescriptionPopupRoot.SetActive(false);
        }
        // --- END NEW ---
        // Ensure prescription UI is initially inactive
        if (prescriptionOrderUIRoot != null)
        {
            prescriptionOrderUIRoot.SetActive(false);
        }
    }

    public static PlayerUIPopups Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("PlayerUIPopups Instance is null.  There needs to be one in the scene.");
            }
            return instance;
        }
    }

    /// <summary>
    /// Sets the invalid item popup active for a specified duration (currently 5 seconds)
    /// or deactivates it immediately.
    /// </summary>
    /// <param name="isActive">True to show the popup for the duration, false to hide immediately.</param>
    public void SetInvalidItemActive(bool isActive)
    {
        if (invalidItem == null)
        {
            Debug.LogWarning("InvalidItem GameObject is not assigned in PlayerUIPopups.");
            return;
        }

        if (isActive)
        {
            // If the popup is already active and the coroutine is running, stop it
            // before starting a new one. This prevents multiple coroutines running
            // and ensures the popup stays for the full 5 seconds from the *last* call.
            if (disableInvalidItemCoroutine != null)
            {
                StopCoroutine(disableInvalidItemCoroutine);
            }

            invalidItem.SetActive(true);
            disableInvalidItemCoroutine = StartCoroutine(DisablePopUpAfterDelay(invalidItem, 3f)); // Pass the GameObject and duration
        }
        else
        {
            // If SetActive(false) is called, stop any running coroutine
            // and immediately deactivate the popup.
            if (disableInvalidItemCoroutine != null)
            {
                StopCoroutine(disableInvalidItemCoroutine);
                disableInvalidItemCoroutine = null; // Nullify the reference
            }
            invalidItem.SetActive(false);
        }
    }

    /// <summary>
    /// Generic coroutine to wait for a specified duration and then deactivate a GameObject.
    /// </summary>
    /// <param name="gameObjectToDisable">The GameObject to deactivate.</param>
    /// <param name="delay">The duration in seconds to wait before deactivating.</param>
    /// <returns>The Coroutine instance.</returns>
    private IEnumerator DisablePopUpAfterDelay(GameObject gameObjectToDisable, float delay) // Made generic
    {
        yield return new WaitForSeconds(delay);

        // After the delay, deactivate the item
        if (gameObjectToDisable != null)
        {
            gameObjectToDisable.SetActive(false);
        }

        // Clear the specific coroutine reference based on which popup it was
        if (gameObjectToDisable == invalidItem) disableInvalidItemCoroutine = null;
        else if (gameObjectToDisable == wrongPrescriptionPopupRoot) disableWrongPrescriptionCoroutine = null;
        // Add other popup references here if they use this generic coroutine
    }


    // --- NEW: Methods for Wrong Prescription Popup ---
    /// <summary>
    /// Displays a specific message in the 'Wrong Prescription' UI popup for a set duration.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public void ShowWrongPrescriptionMessage(string message)
    {
        if (wrongPrescriptionPopupRoot == null || wrongPrescriptionMessageText == null)
        {
            Debug.LogWarning("Wrong Prescription UI references (Root or Text) are not assigned in PlayerUIPopups. Cannot display message.");
            return;
        }

        // Stop any existing coroutine for this popup to reset the timer
        if (disableWrongPrescriptionCoroutine != null)
        {
            StopCoroutine(disableWrongPrescriptionCoroutine);
        }

        wrongPrescriptionMessageText.text = message;
        wrongPrescriptionPopupRoot.SetActive(true);
        Debug.Log($"PlayerUIPopups: Displaying wrong prescription message: '{message}'", this);

        // Start the coroutine to hide the popup after the configured duration
        disableWrongPrescriptionCoroutine = StartCoroutine(DisablePopUpAfterDelay(wrongPrescriptionPopupRoot, wrongPrescriptionPopupDuration));
    }

    /// <summary>
    /// Hides the 'Wrong Prescription' UI popup immediately.
    /// </summary>
    public void HideWrongPrescriptionMessage()
    {
        if (wrongPrescriptionPopupRoot == null) return;

        // Stop the coroutine if it's running
        if (disableWrongPrescriptionCoroutine != null)
        {
            StopCoroutine(disableWrongPrescriptionCoroutine);
            disableWrongPrescriptionCoroutine = null; // Nullify the reference
        }

        if (wrongPrescriptionPopupRoot.activeSelf)
        {
             wrongPrescriptionPopupRoot.SetActive(false);
             Debug.Log("PlayerUIPopups: Hiding wrong prescription message UI.", this);
        }
    }
    // --- END NEW ---


    // --- Methods for Prescription Order UI ---

    /// <summary>
    /// Displays the prescription order details in the UI popup.
    /// </summary>
    /// <param name="order">The PrescriptionOrder to display.</param>
    public void DisplayPrescriptionOrder(PrescriptionOrder order)
    {
        if (prescriptionOrderUIRoot == null || prescriptionOrderDetailsText == null)
        {
            Debug.LogWarning("Prescription Order UI references (Root or Text) are not assigned in PlayerUIPopups. Cannot display order details.");
            return;
        }

        prescriptionOrderDetailsText.text = order.ToString(); // Use the struct's ToString for display
        prescriptionOrderUIRoot.SetActive(true);
        Debug.Log($"PlayerUIPopups: Displaying prescription order details for: {order.patientName}", this);
    }

    /// <summary>
    /// Hides the prescription order details UI popup and clears the text.
    /// </summary>
    public void HidePrescriptionOrder()
    {
        if (prescriptionOrderUIRoot == null || prescriptionOrderDetailsText == null)
        {
             // Log as warning only if we expected it to be active
             if (prescriptionOrderUIRoot != null && prescriptionOrderUIRoot.activeSelf)
             {
                 Debug.LogWarning("PlayerUIPopups: Prescription Order UI references (Root or Text) are not assigned, but HidePrescriptionOrder was called while UI was active. Cannot hide/clear.", this);
             }
            return;
        }

        prescriptionOrderDetailsText.text = ""; // Clear the text
        prescriptionOrderUIRoot.SetActive(false);
        Debug.Log("PlayerUIPopups: Hiding prescription order details UI.", this);
    }
}
// --- END OF FILE PlayerUIPopups.cs ---