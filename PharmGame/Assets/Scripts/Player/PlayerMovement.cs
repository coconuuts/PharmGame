using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        public float sprintDuration = 10f; 
        public float regenDuration = 5f; 
        [Range(0f, 1f)] public float minStaminaToSprint = 0.30f; 
        public Slider staminaSlider; 
        
        private bool canSprint = true;
        Vector3 velocity;

        [Header("Ground Check")]
        public Transform groundCheck;
        public float groundDistance = 0.1f;
        public LayerMask groundMask;
        public bool isGrounded { get; private set; } 

        private bool movementEnabled = true;

        [Header("Developer Debug")]
        public bool isDebugModeActive = false;
        public bool isFlying = false;
        public float flySpeedMultiplier = 3f;
        public float flyVerticalSpeed = 10f;

        private void Start()
        {
            currentStamina = maxStamina;
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

            // Do not snap to ground if we are flying
            if (isGrounded && velocity.y < 0 && !isFlying)
            {
                velocity.y = -2f;
            }

            Vector3 horizontalMove = Vector3.zero;

            if (movementEnabled)
            {
                // Listen for Fly Mode Toggle (Z) if debug mode is active
                if (isDebugModeActive && Input.GetKeyDown(KeyCode.Z))
                {
                    isFlying = !isFlying;
                    if (isFlying) velocity.y = 0f; // Reset falling momentum immediately
                    Debug.Log("Fly mode: " + isFlying);
                }

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

                if (isSprinting && !isFlying) // Don't drain stamina if flying
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

                if (staminaSlider != null) staminaSlider.value = currentStamina;

                // Move Speed assignment
                moveSpeed = isSprinting ? sprintSpeed : walkSpeed;
                if (isFlying) moveSpeed *= flySpeedMultiplier;

                Vector3 move = transform.right * x + transform.forward * z;
                if (move.magnitude > 1f) move.Normalize(); 

                horizontalMove = move * moveSpeed;

                // Jump or Fly Vertical Movement
                if (isFlying)
                {
                    velocity.y = 0f; // Neutralize gravity
                    if (Input.GetKey(KeyCode.Space))
                    {
                        velocity.y = flyVerticalSpeed;
                    }
                    else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    {
                        velocity.y = -flyVerticalSpeed;
                    }
                }
                else
                {
                    if (Input.GetButtonDown("Jump") && isGrounded)
                    {
                        velocity.y = jumpForce;
                    }
                }
            }

            // Only apply gravity if we aren't flying
            if (!isFlying)
            {
                velocity.y += gravity * Time.deltaTime;
            }

            Vector3 finalMovement = horizontalMove + velocity;
            controller.Move(finalMovement * Time.deltaTime);
        }
    }
}