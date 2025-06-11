// --- START OF FILE PlayerUIPopups.cs ---

using UnityEngine;
using TMPro; // Required for TMP_Text
using System.Collections; // Required for using Coroutines
using Game.Prescriptions; // Needed for PrescriptionOrder // <-- NEW: Added using directive

public class PlayerUIPopups : MonoBehaviour
{
    [Header("Invalid Item Popup")] // <-- NEW HEADER
    [SerializeField] private GameObject invalidItem;

    [Header("Prescription Order UI")] // <-- NEW HEADER
    [Tooltip("The root GameObject for the prescription order details UI.")] // <-- NEW FIELD
    [SerializeField] private GameObject prescriptionOrderUIRoot;
    [Tooltip("The TMP Text component to display the prescription order details.")] // <-- NEW FIELD
    [SerializeField] private TMPro.TMP_Text prescriptionOrderDetailsText;


    private static PlayerUIPopups instance; // Keeping from original script

    private Coroutine disableInvalidItemCoroutine; // To keep track of the running coroutine

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
        // --- NEW: Ensure prescription UI is initially inactive --- // <-- NEW LOGIC
        if (prescriptionOrderUIRoot != null)
        {
            prescriptionOrderUIRoot.SetActive(false);
        }
        // --- END NEW ---
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
            disableInvalidItemCoroutine = StartCoroutine(DisablePopUpAfterDelay(3f)); // Start the coroutine
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
    /// Coroutine to wait for a specified duration and then deactivate the invalid item GameObject.
    /// </summary>
    /// <param name="delay">The duration in seconds to wait before deactivating.</param>
    private IEnumerator DisablePopUpAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // After the delay, deactivate the item and clear the coroutine reference
        if (invalidItem != null)
        {
            invalidItem.SetActive(false);
        }
        disableInvalidItemCoroutine = null; // Indicate the coroutine has finished
    }

    // --- NEW: Methods for Prescription Order UI --- // <-- NEW LOGIC

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

    // --- END NEW ---
}
// --- END OF FILE PlayerUIPopups.cs ---