using UnityEngine;
using TMPro; // Although not used in this specific modification, keeping it from the original script
using System.Collections; // Required for using Coroutines

public class PlayerUIPopups : MonoBehaviour
{
    [SerializeField] private GameObject invalidItem;

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
        // Ensure the item is initially inactive
        if (invalidItem != null)
        {
            invalidItem.SetActive(false);
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
}