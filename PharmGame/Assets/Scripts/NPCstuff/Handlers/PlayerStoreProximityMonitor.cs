// --- START OF FILE PlayerStoreProximityMonitor.cs ---

using UnityEngine;
using CustomerManagement; // Needed for CustomerManager and StorePauseSource

/// <summary>
/// Monitors the player's distance to a central store point.
/// If the player moves too far away, it requests that CustomerManager
/// pause active NPC spawning.
/// </summary>
public class PlayerStoreProximityMonitor : MonoBehaviour
{
    [Header("Monitoring Setup")]
    [Tooltip("The central point of the store to measure distance from.")]
    [SerializeField] private Transform storeCenterPoint;

    [Tooltip("The distance (in meters) beyond which customer spawning should be paused.")]
    [SerializeField] private float pauseDistance = 100f;

    [Header("Dependencies")]
    [Tooltip("Reference to the player's transform. If not set, will be found by tag 'Player'.")]
    [SerializeField] private Transform playerTransform;

    // Internal State
    private CustomerManager customerManager;
    private bool isPausedByProximity = false;
    private float pauseDistanceSqr; // For more performant distance checks

    void Start()
    {
        customerManager = CustomerManager.Instance;
        if (customerManager == null)
        {
            Debug.LogError("PlayerStoreProximityMonitor: CustomerManager not found! Disabling component.", this);
            enabled = false;
            return;
        }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogError("PlayerStoreProximityMonitor: Player Transform not assigned and not found by tag 'Player'! Disabling component.", this);
                enabled = false;
                return;
            }
        }

        if (storeCenterPoint == null)
        {
            Debug.LogError("PlayerStoreProximityMonitor: Store Center Point is not assigned! Disabling component.", this);
            enabled = false;
            return;
        }

        // Pre-calculate the squared distance for efficiency
        pauseDistanceSqr = pauseDistance * pauseDistance;
    }

    void Update()
    {
        // Calculate the squared distance between the player and the store
        float distanceToStoreSqr = (playerTransform.position - storeCenterPoint.position).sqrMagnitude;

        // Check if the player is far away
        bool isPlayerFar = distanceToStoreSqr > pauseDistanceSqr;

        // If the player's status has changed (e.g., they just went from near to far)
        if (isPlayerFar && !isPausedByProximity)
        {
            // Player just moved out of range, so pause spawning
            Debug.Log($"Player is far from the store ({Mathf.Sqrt(distanceToStoreSqr):F1}m). Pausing customer spawning.", this);
            customerManager.SetStoreSimulationActive(true, StorePauseSource.Proximity);
            isPausedByProximity = true;
        }
        else if (!isPlayerFar && isPausedByProximity)
        {
            // Player just moved back into range, so resume spawning
            Debug.Log($"Player is near the store ({Mathf.Sqrt(distanceToStoreSqr):F1}m). Resuming customer spawning.", this);
            customerManager.SetStoreSimulationActive(false, StorePauseSource.Proximity);
isPausedByProximity = false;
        }
    }
}

// --- END OF FILE PlayerStoreProximityMonitor.cs ---