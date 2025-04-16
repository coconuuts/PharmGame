using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;

    public float moveSpeed = 7f;
    public float gravity = -9.81f;
    // The jumpHeight variable is now mainly for reference – we use jumpForce for the actual jump.
    public float jumpHeight = 3f;
    
    // Instead of computing jump velocity with sqrt at runtime, we predefine jumpForce.
    // For example, with gravity=-9.81 and jumpHeight=3, a good starting value is about 7.67.
    public float jumpForce = 7.67f;

    Vector3 velocity;

    public Transform groundCheck;
    public float groundDistance = 0.1f;
    public LayerMask groundMask;
    bool isGrounded;

    private void Update()
    {
        // Check if the player is grounded
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if(isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Get input axes
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        // Calculate movement direction
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Jump functionality without using sqrt:
        // When the jump button is pressed and the player is grounded,
        // we simply assign the precomputed jumpForce to the vertical velocity.
        if(Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = jumpForce;
        }

        // Apply gravity over time
        velocity.y += gravity * Time.deltaTime;

        // Move the controller vertically
        controller.Move(velocity * Time.deltaTime);
    }
}
