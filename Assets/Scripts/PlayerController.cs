using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// RPG-style character controller with WASD movement and mouse look-at.
/// Character always faces the mouse position on the ground.
/// Shift key toggles between walk (slow) and run (fast) speeds.
/// Uses Unity's new Input System.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Fast movement speed (run/jog)")]
    public float runSpeed = 5f;
    
    [Tooltip("Slow movement speed (walk)")]
    public float walkSpeed = 2.5f;
    
    [Tooltip("How quickly the character rotates to face mouse")]
    public float rotationSpeed = 15f;

    [Header("Ground Detection")]
    [Tooltip("Layer mask for ground raycast (for mouse look-at)")]
    public LayerMask groundLayer;
    
    [Tooltip("Gravity applied to the character")]
    public float gravity = -9.81f;

    [Header("Animation")]
    [Tooltip("Smoothing for animation parameter transitions")]
    public float animationSmoothTime = 0.1f;

    [Header("Position Boundaries")]
    [Tooltip("Minimum allowed X position")]
    public float minX = 0f;
    
    [Tooltip("Maximum allowed X position")]
    public float maxX = 15f;

    // Components
    private CharacterController characterController;
    private Animator animator;
    private Camera mainCamera;

    // Input System
    private Keyboard keyboard;
    private Mouse mouse;

    // Animation parameter hashes for performance
    private static readonly int AnimX = Animator.StringToHash("x");
    private static readonly int AnimY = Animator.StringToHash("y");
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimRaiseHand = Animator.StringToHash("RaiseHand");

    // Current animation values (for smoothing)
    private float currentAnimX;
    private float currentAnimY;
    private float animXVelocity;
    private float animYVelocity;

    // Vertical velocity for gravity
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;

        // Get input devices
        keyboard = Keyboard.current;
        mouse = Mouse.current;

        // Ensure ground layer is set - default to "Default" layer if not configured
        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
            Debug.LogWarning("PlayerController: Ground Layer not set. Defaulting to 'Default' layer. Please configure in Inspector.");
        }
    }

    private void Update()
    {
        if (keyboard == null || mouse == null)
        {
            keyboard = Keyboard.current;
            mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
        }

        HandleRotation();
        HandleMovement();
        HandleActions();
        ApplyGravity();
    }

    /// <summary>
    /// Handles action inputs like raising hand
    /// </summary>
    private void HandleActions()
    {
        // R key to raise hand
        if (keyboard.rKey.wasPressedThisFrame)
        {
            animator.SetTrigger(AnimRaiseHand);
            
            // Trigger camera zoom
            RPGCameraFollow cameraFollow = mainCamera.GetComponent<RPGCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.ZoomInTemporary(3f);
            }
        }
    }

    /// <summary>
    /// Rotates the character to face the mouse position on the ground
    /// </summary>
    private void HandleRotation()
    {
        Vector2 mousePos = mouse.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 lookDirection = hit.point - transform.position;
            lookDirection.y = 0; // Keep rotation on horizontal plane only
            
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Handles WASD movement input and applies movement relative to character facing
    /// </summary>
    private void HandleMovement()
    {
        // Get input from new Input System
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.wKey.isPressed) vertical += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;

        // Normalize diagonal movement
        Vector2 input = new Vector2(horizontal, vertical);
        if (input.magnitude > 1f) input.Normalize();
        horizontal = input.x;
        vertical = input.y;

        // Determine speed multiplier (0.5 for walk, 1.0 for run)
        bool isWalking = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        float speedMultiplier = isWalking ? 0.5f : 1f;
        float currentSpeed = isWalking ? walkSpeed : runSpeed;

        // Notify camera about running state for dynamic zoom
        bool isMoving = horizontal != 0 || vertical != 0;
        RPGCameraFollow cameraFollow = mainCamera.GetComponent<RPGCameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.SetRunning(isMoving && !isWalking);
        }

        // For a proper RPG camera where character faces mouse:
        // We want WASD to be relative to the character's facing direction
        // W = forward (positive Y in blend tree)
        // S = backward (negative Y in blend tree)
        // A = strafe left (negative X in blend tree)
        // D = strafe right (positive X in blend tree)
        
        float targetAnimX = horizontal * speedMultiplier;
        float targetAnimY = vertical * speedMultiplier;

        // Smooth animation parameters
        currentAnimX = Mathf.SmoothDamp(currentAnimX, targetAnimX, ref animXVelocity, animationSmoothTime);
        currentAnimY = Mathf.SmoothDamp(currentAnimY, targetAnimY, ref animYVelocity, animationSmoothTime);

        // Apply to animator
        animator.SetFloat(AnimX, currentAnimX);
        animator.SetFloat(AnimY, currentAnimY);
        
        // Speed is the magnitude - used for Idle <-> Locomotion transition
        float speed = Mathf.Sqrt(currentAnimX * currentAnimX + currentAnimY * currentAnimY);
        animator.SetFloat(AnimSpeed, speed);

        // Calculate world-space movement direction
        // Movement is relative to character's facing direction
        Vector3 moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;
        
        // Apply movement
        if (moveDirection.magnitude > 0.1f)
        {
            Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
            movement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(movement);
        }
        else
        {
            // Still apply gravity when not moving
            characterController.Move(new Vector3(0, verticalVelocity * Time.deltaTime, 0));
        }

        // Clamp X position to boundaries
        Vector3 clampedPos = transform.position;
        clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
        transform.position = clampedPos;
    }

    /// <summary>
    /// Applies gravity to keep character grounded
    /// </summary>
    private void ApplyGravity()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = -2f; // Small downward force to keep grounded
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}
