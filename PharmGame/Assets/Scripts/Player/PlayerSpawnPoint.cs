using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        // Draw a visual representation in the editor so you can see where the spawn is
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw forward direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}