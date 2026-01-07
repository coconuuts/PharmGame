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
        if (playerTransform == null || storeCenterPoint == null)
        {
            RefreshReferences();

            // If we still can't find them (e.g., during the exact frame of a scene load), 
            // return immediately to prevent the crash.
            if (playerTransform == null || storeCenterPoint == null) return;
        }
        
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

    /// <summary>
    /// Attempts to re-find references if they are lost/destroyed (e.g. after Scene Load).
    /// </summary>
    private void RefreshReferences()
    {
        // 1. Find Customer Manager
        if (customerManager == null) customerManager = CustomerManager.Instance;

        // 2. Find Player (Standard Tag Search)
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // 3. Find Store Center (Search by Name if reference is lost)
        // Note: Since this reference breaks on scene load, we try to find an object named "StoreCenter"
        // You can also change this to use a Tag if you prefer.
        if (storeCenterPoint == null)
        {
            GameObject storeObj = GameObject.Find("StoreCenter"); 
            // OR use a tag: GameObject.FindGameObjectWithTag("StoreCenter");
            
            if (storeObj != null) storeCenterPoint = storeObj.transform;
        }
    }
}

// --- END OF FILE PlayerStoreProximityMonitor.cs ---