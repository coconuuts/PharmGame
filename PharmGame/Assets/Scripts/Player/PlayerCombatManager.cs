// --- PlayerCombatManager.cs ---
using UnityEngine;
using Game.Events; // Needed for publishing NpcAttackedEvent

public class PlayerCombatManager : MonoBehaviour
{
    [Tooltip("The transform representing the player's 'camera' or attack origin.")]
    [SerializeField] private Transform attackOrigin;

    [Tooltip("The maximum distance for the raycast attack.")]
    [SerializeField] private float attackDistance = 2f;

    [Tooltip("The layer mask to filter what the raycast can hit (e.g., include 'NPC' layer).")]
    [SerializeField] private LayerMask hitLayerMask;

    [Tooltip("The tag required on a GameObject to consider it an NPC target.")]
    [SerializeField] private string npcTag = "NPC"; // Ensure your NPC prefabs have this tag


    private void Awake()
    {
        if (attackOrigin == null)
        {
            // Default to camera if origin is not set
            attackOrigin = Camera.main?.transform;
            if (attackOrigin == null)
            {
                Debug.LogError("PlayerCombatManager: Attack Origin transform not assigned and no main camera found! Self-disabling.", this);
                enabled = false;
                return;
            }
        }
    }

    private void Update()
    {
        // Check for Left Mouse Button click
        if (Input.GetMouseButtonDown(0)) // Assuming Left Mouse Button is the primary attack input
        {
            AttemptAttack();
        }
    }

    private void AttemptAttack()
    {
        if (attackOrigin == null) return;

        Debug.Log("PlayerCombatManager: Attempting attack raycast.");

        Ray ray = new Ray(attackOrigin.position, attackOrigin.forward);
        RaycastHit hit;

        // Perform the raycast
        if (Physics.Raycast(ray, out hit, attackDistance, hitLayerMask))
        {
            Debug.Log($"PlayerCombatManager: Raycast hit {hit.collider.gameObject.name}.", hit.collider.gameObject);

            // Check if the hit object has the NPC tag
            if (hit.collider.gameObject.CompareTag(npcTag))
            {
                Debug.Log($"PlayerCombatManager: Hit object has NPC tag. Triggering Combat for {hit.collider.gameObject.name}.", hit.collider.gameObject);
                // --- PUBLISH THE NPC ATTACKED EVENT ---
                // We don't need to get the NPC's state machine here.
                // We just announce that this NPC GameObject was attacked.
                // The Runner on the NPC will be subscribed and handle the rest.
                EventManager.Publish(new NpcAttackedEvent(hit.collider.gameObject, this.gameObject)); // Pass the NPC GameObject and the player GameObject (this script is on the player)
                // --------------------------------------
            }
        }
        else
        {
            Debug.Log("PlayerCombatManager: Raycast hit nothing or nothing on the target layer.");
        }
    }

    // Optional: Add a debug visualization of the raycast
    private void OnDrawGizmosSelected()
    {
        if (attackOrigin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(attackOrigin.position, attackOrigin.forward * attackDistance);
        }
    }
}