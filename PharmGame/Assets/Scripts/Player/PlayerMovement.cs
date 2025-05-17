using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Interaction;

namespace Systems.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        public CharacterController controller;

        public float moveSpeed = 7f;
        public float gravity = -9.81f;
        public float jumpHeight = 3f;
        public float jumpForce = 7.67f;

        Vector3 velocity;

        public Transform groundCheck;
        public float groundDistance = 0.1f;
        public LayerMask groundMask;
        bool isGrounded;

        // --- ADDED: Flag to control movement ---
        private bool movementEnabled = true;
        // -------------------------------------


        private void OnEnable()
        {
            // Subscribe to the state change event
            MenuManager.OnStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from the state change event
            MenuManager.OnStateChanged -= HandleGameStateChanged;
        }

        // --- ADDED: Handler for state changes ---
        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
            // Enable movement only in the Playing state
            if (newState == MenuManager.GameState.Playing)
            {
                SetMovementEnabled(true);
            }
            else
            {
                SetMovementEnabled(false); // Disable movement in all other states
            }
        }
        // ----------------------------------------

        // --- ADDED: Public method to set movement enabled state ---
        /// <summary>
        /// Enables or disables player movement input processing.
        /// Called by StateAction Scriptable Objects or event handlers.
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
             // Optional: If disabling movement, stop any ongoing velocity immediately
            if (!enabled)
            {
                 velocity.x = 0;
                 velocity.z = 0;
                 // For CharacterController, setting velocity directly might not work as expected for vertical.
                 // The gravity logic below will handle vertical velocity decay when not grounded.
                 // For horizontal, setting move * moveSpeed = 0 implicitly handles it in Update.
            }
            Debug.Log($"PlayerMovement: Movement enabled set to {movementEnabled}.");
        }
        // ---------------------------------------------------------


        private void Update()
        {
            // Check if the player is grounded
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            // --- MODIFIED: Only process movement input if movement is enabled ---
            if (movementEnabled)
            {
                // Get input axes
                float x = Input.GetAxisRaw("Horizontal");
                float z = Input.GetAxisRaw("Vertical");

                // Calculate movement direction
                Vector3 move = transform.right * x + transform.forward * z;
                controller.Move(move * moveSpeed * Time.deltaTime);

                // Jump functionality
                if (Input.GetButtonDown("Jump") && isGrounded)
                {
                    velocity.y = jumpForce;
                }
            }
            // -----------------------------------------------------------------


            // Apply gravity over time (always applies, even if movement input is disabled)
            velocity.y += gravity * Time.deltaTime;

            // Move the controller vertically (always applies)
            controller.Move(velocity * Time.deltaTime);
        }

         // Removed obsolete methods related to old MenuManager direct calls if any existed
    }
}