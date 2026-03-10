using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI; // --- ADDED: Required for UI elements like Slider ---
using Systems.GameStates; 
using Systems.Interaction;

namespace Systems.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        public CharacterController controller;

        public enum SprintMode { Hold, Toggle }

        [Header("Movement Speeds")]
        public float walkSpeed = 7f;
        public float sprintSpeed = 12f;
        public float moveSpeed = 7f; 

        [Header("Jump & Gravity")]
        public float gravity = -9.81f;
        public float jumpHeight = 3f;
        public float jumpForce = 7.67f;

        [Header("Sprint Settings")]
        public SprintMode sprintMode = SprintMode.Hold;
        public bool isSprinting { get; private set; }

        [Header("Stamina Settings")]
        public float maxStamina = 100f;
        public float currentStamina = 100f;
        [Tooltip("How many seconds the player can sprint before stamina hits 0.")]
        public float sprintDuration = 10f; 
        [Tooltip("How many seconds it takes to regenerate from 0 to max stamina.")]
        public float regenDuration = 5f; 
        [Tooltip("Minimum percentage of stamina required to start sprinting (0.0 to 1.0).")]
        [Range(0f, 1f)] public float minStaminaToSprint = 0.30f; 
        
        // --- ADDED: Reference to the UI Slider ---
        [Tooltip("Drag your UI Slider here to display stamina.")]
        public Slider staminaSlider; 
        
        // Flag to lock out sprinting until the minimum threshold is met
        private bool canSprint = true;

        Vector3 velocity;

        [Header("Ground Check")]
        public Transform groundCheck;
        public float groundDistance = 0.1f;
        public LayerMask groundMask;
        public bool isGrounded { get; private set; } 

        private bool movementEnabled = true;

        private void Start()
        {
            currentStamina = maxStamina;

            // --- ADDED: Initialize the slider values ---
            if (staminaSlider != null)
            {
                staminaSlider.maxValue = maxStamina;
                staminaSlider.value = currentStamina;
            }
        }

        private void OnEnable()
        {
            MenuManager.OnStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            MenuManager.OnStateChanged -= HandleGameStateChanged;
        }

        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
            if (newState == MenuManager.GameState.Playing)
            {
                SetMovementEnabled(true);
            }
            else
            {
                SetMovementEnabled(false); 
            }
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!enabled)
            {
                 velocity.x = 0;
                 velocity.z = 0;
                 isSprinting = false; 
            }
        }

        private void Update()
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            Vector3 horizontalMove = Vector3.zero;

            if (movementEnabled)
            {
                float x = Input.GetAxisRaw("Horizontal");
                float z = Input.GetAxisRaw("Vertical");

                bool hasMovementInput = Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f;

                // --- STAMINA LOGIC ---
                if (!isSprinting && currentStamina < maxStamina)
                {
                    float regenRate = maxStamina / regenDuration;
                    currentStamina += regenRate * Time.deltaTime;
                    if (currentStamina > maxStamina) currentStamina = maxStamina;
                }

                if (!canSprint && currentStamina >= (maxStamina * minStaminaToSprint))
                {
                    canSprint = true;
                }

                // --- SPRINT LOGIC ---
                if (sprintMode == SprintMode.Hold)
                {
                    isSprinting = Input.GetKey(KeyCode.LeftShift) && hasMovementInput && canSprint;
                }
                else if (sprintMode == SprintMode.Toggle)
                {
                    if (Input.GetKeyDown(KeyCode.LeftShift) && hasMovementInput && canSprint)
                    {
                        isSprinting = !isSprinting;
                    }
                    else if (!hasMovementInput || !canSprint)
                    {
                        isSprinting = false;
                    }
                }

                // Drain stamina if we are actively sprinting
                if (isSprinting)
                {
                    float drainRate = maxStamina / sprintDuration;
                    currentStamina -= drainRate * Time.deltaTime;
                    
                    if (currentStamina <= 0f)
                    {
                        currentStamina = 0f;
                        isSprinting = false; 
                        canSprint = false; 
                    }
                }

                // --- ADDED: Update the UI Slider every frame ---
                if (staminaSlider != null)
                {
                    staminaSlider.value = currentStamina;
                }

                moveSpeed = isSprinting ? sprintSpeed : walkSpeed;

                Vector3 move = transform.right * x + transform.forward * z;
                if (move.magnitude > 1f) move.Normalize(); 

                horizontalMove = move * moveSpeed;

                if (Input.GetButtonDown("Jump") && isGrounded)
                {
                    velocity.y = jumpForce;
                }
            }

            velocity.y += gravity * Time.deltaTime;

            Vector3 finalMovement = horizontalMove + velocity;
            controller.Move(finalMovement * Time.deltaTime);
        }
    }
}