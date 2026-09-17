using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Pulo")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckExtraDistance = 0.15f;

    [Header("Gravidade")]
    [SerializeField] private float extraGravity = 25f;
    [SerializeField] private float maxFallSpeed = 25f;

    [Header("Mortal")]
    [SerializeField] private float flipSpeed = 720f;

    [Header("Camera")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private float cameraHeight = 2f;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;

    private Rigidbody rb;
    private Collider playerCollider;
    private PlayerControls controls;

    private bool isGrounded;
    private bool sprinting;
    private int jumpsRemaining;

    private bool doingFlip;
    private float flipRotation;

    private float cameraHorizontal;
    private float cameraVertical;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();

        rb.freezeRotation = true;

        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Enable();

        controls.Movement.Jump.performed += Jump;
        controls.Movement.Sprint.performed += SprintStart;
        controls.Movement.Sprint.canceled += SprintEnd;
    }

    private void OnDisable()
    {
        controls.Movement.Jump.performed -= Jump;
        controls.Movement.Sprint.performed -= SprintStart;
        controls.Movement.Sprint.canceled -= SprintEnd;

        controls.Disable();
    }

    private void Start()
    {
        jumpsRemaining = maxJumps;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        CheckGround();
        UpdateCamera();
    }

    private void FixedUpdate()
    {
        Move();
        ApplyExtraGravity();
        ApplyFlip();
    }

    // ============================
    // MOVIMENTO
    // ============================

    private void Move()
    {
        // Lê o Move diretamente do Input System
        Vector2 moveInput =
            controls.Movement.Move.ReadValue<Vector2>();

        if (cameraPivot == null)
            return;

        Vector3 forward = cameraPivot.forward;
        Vector3 right = cameraPivot.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * moveInput.y +
            right * moveInput.x;

        if (direction.magnitude > 1f)
            direction.Normalize();

        float speed =
            sprinting ? sprintSpeed : walkSpeed;

        rb.velocity = new Vector3(
            direction.x * speed,
            rb.velocity.y,
            direction.z * speed
        );

        if (direction.sqrMagnitude > 0.01f &&
            !doingFlip)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                );
        }
    }

    // ============================
    // PULO
    // ============================

    private void Jump(InputAction.CallbackContext context)
    {
        if (jumpsRemaining <= 0)
            return;

        bool secondJump =
            !isGrounded &&
            jumpsRemaining == 1;

        // Zera somente a velocidade vertical
        rb.velocity = new Vector3(
            rb.velocity.x,
            0f,
            rb.velocity.z
        );

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );

        jumpsRemaining--;

        if (secondJump)
        {
            doingFlip = true;
            flipRotation = 0f;
        }
    }

    // ============================
    // CHÃO
    // ============================

    private void CheckGround()
    {
        if (playerCollider == null)
            return;

        Vector3 origin =
            playerCollider.bounds.center;

        float distance =
            playerCollider.bounds.extents.y +
            groundCheckExtraDistance;

        bool groundedNow = Physics.Raycast(
            origin,
            Vector3.down,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (groundedNow && !isGrounded)
        {
            jumpsRemaining = maxJumps;

            doingFlip = false;
            flipRotation = 0f;

            Vector3 rot = transform.eulerAngles;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    rot.y,
                    0f
                );
        }

        // Também garante os pulos enquanto estiver no chão
        if (groundedNow)
        {
            jumpsRemaining = maxJumps;
        }

        isGrounded = groundedNow;
    }

    // ============================
    // GRAVIDADE
    // ============================

    private void ApplyExtraGravity()
    {
        if (isGrounded)
            return;

        // Força adicional para não ficar "flutuando"
        rb.AddForce(
            Vector3.down * extraGravity,
            ForceMode.Acceleration
        );

        if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector3(
                rb.velocity.x,
                -maxFallSpeed,
                rb.velocity.z
            );
        }
    }

    // ============================
    // MORTAL
    // ============================

    private void ApplyFlip()
    {
        if (!doingFlip)
            return;

        float amount =
            flipSpeed * Time.fixedDeltaTime;

        transform.Rotate(
            Vector3.right,
            amount,
            Space.Self
        );

        flipRotation += amount;

        if (flipRotation >= 360f)
        {
            doingFlip = false;
            flipRotation = 0f;

            Vector3 rot = transform.eulerAngles;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    rot.y,
                    0f
                );
        }
    }

    // ============================
    // CORRIDA
    // ============================

    private void SprintStart(
        InputAction.CallbackContext context)
    {
        sprinting = true;
    }

    private void SprintEnd(
        InputAction.CallbackContext context)
    {
        sprinting = false;
    }

    // ============================
    // CÂMERA
    // ============================

    private void UpdateCamera()
    {
        if (cameraPivot == null ||
            cameraTransform == null)
            return;

        Vector2 lookInput =
            controls.Movement.Look.ReadValue<Vector2>();

        cameraHorizontal +=
            lookInput.x * mouseSensitivity;

        cameraVertical -=
            lookInput.y * mouseSensitivity;

        cameraVertical = Mathf.Clamp(
            cameraVertical,
            minVerticalAngle,
            maxVerticalAngle
        );

        cameraPivot.position =
            transform.position +
            Vector3.up * cameraHeight;

        cameraPivot.rotation =
            Quaternion.Euler(
                cameraVertical,
                cameraHorizontal,
                0f
            );

        cameraTransform.position =
            cameraPivot.position -
            cameraPivot.forward * cameraDistance;

        cameraTransform.LookAt(
            cameraPivot.position
        );
    }

    // ============================
    // GIZMOS
    // ============================

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();

        if (col == null)
            return;

        Gizmos.color =
            isGrounded ? Color.green : Color.red;

        Vector3 origin = col.bounds.center;

        float distance =
            col.bounds.extents.y +
            groundCheckExtraDistance;

        Gizmos.DrawLine(
            origin,
            origin + Vector3.down * distance
        );
    }
}