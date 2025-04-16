using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StairDetection : MonoBehaviour
{
    [Header("Step Climbing Settings")]
    [Tooltip("Maximum step height that can be climbed (in world units).")]
    public float stepHeight = 0.03f;
    [Tooltip("Upward force applied to help climb the step. Increase for a faster lift.")]
    public float stepSmooth = 3f;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // This method is called once per frame for every collider/rigidbody
    // that is touching the player.
    private void OnCollisionStay(Collision collision)
    {
        // Only process collisions with objects tagged "Stair"
        if (collision.gameObject.CompareTag("Stair"))
        {
            // Loop through all contact points in the collision.
            foreach (ContactPoint contact in collision.contacts)
            {
                // Calculate the vertical difference between the contact point and the player's position.
                // (Assuming the player's pivot roughly represents the base; adjust as needed.)
                float contactHeight = contact.point.y - transform.position.y;
                Debug.Log($"{contactHeight} contact height");
                // If the contact point is above the player's base, but within the maximum step height...
                if (contactHeight > 0.01f && contactHeight <= stepHeight)
                {
                    // Optionally, you can check if the contact normal is not too steep upward.
                    // This helps distinguish between climbing a step and walking on an almost flat surface.
                    if (contact.normal.y < 1f)
                    {
                        // Apply an upward force instantly to help the player "step up."
                        // ForceMode.VelocityChange applies an immediate change to the velocity.
                        rb.AddForce(Vector3.up * stepSmooth, ForceMode.VelocityChange);
                        
                        // We only need to process one valid contact per frame.
                        break;
                    }
                }
            }
        }
    }
}
