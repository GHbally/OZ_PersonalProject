using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction lookAction;

    private float verticalVelocity;
    private float cameraPitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        PlayerInput playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions.FindAction(
            "Move",
            throwIfNotFound: true
        );

        lookAction = playerInput.actions.FindAction(
            "Look",
            throwIfNotFound: true
        );

        if (cameraTransform == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();

            if (playerCamera != null)
            {
                cameraTransform = playerCamera.transform;
            }
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMovement();
        HandleMouseLook();
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 moveDirection =
            transform.right * input.x +
            transform.forward * input.y;

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Vector2 mouseDelta = lookAction.ReadValue<Vector2>();

        float horizontalRotation = mouseDelta.x * mouseSensitivity;
        float verticalRotation = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * horizontalRotation);

        cameraPitch -= verticalRotation;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}