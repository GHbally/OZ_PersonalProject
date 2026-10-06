using System.Runtime.CompilerServices;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkRunner), typeof(NetworkEvents))]
public class NetworkInputProvider : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Mouse Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(1f, 89f)] private float maxLookAngle = 80f;

    private NetworkEvents networkEvents;
    private InputActionMap playerActions;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

    private NetworkPlayerInputData currentInput;
    private bool hasLocalPlayer;
    private bool isCursorCaptured;

    public Vector2 LookRotation => currentInput.LookRotation;

    private void Awake()
    {
        NetworkRunner networkRunner = GetComponent<NetworkRunner>();
        networkEvents = GetComponent<NetworkEvents>();

        // 플레이어 입력 수집
        networkRunner.ProvideInput = true;

        InputActionMap sourceMap = inputActions != null ? inputActions.FindActionMap("Player") : null;

        if (sourceMap == null)
        {
            Debug.LogError("NetworkInputProvider: PlayerControls와 Player 액션 맵을 확인해주세요.", this);
            enabled = false;
            return;
        }

        // 기존 입력 에셋의 상태에 영향을 주지 않도록 복사
        playerActions = sourceMap.Clone();
        moveAction = playerActions.FindAction("Move");
        lookAction = playerActions.FindAction("Look");
        jumpAction = playerActions.FindAction("Jump");

        if (moveAction == null || lookAction == null || jumpAction == null)
        {
            Debug.LogError("NetworkInputProvider: Move, Look, Jump 액션이 필요합니다.", this);
            enabled = false;
            return;
        }

        networkEvents.OnInput.AddListener(HandleInput);
        networkEvents.OnShutdown.AddListener(HandleShutdown);
    }

    public bool StartLocalInput(float initalYaw)
    {
        if(!isActiveAndEnabled ||
            moveAction == null ||
            lookAction == null ||
            jumpAction == null)
        {
            return false;
        }

        // 생성 지점의 방향으로 시점을 시작
        currentInput = default;
        currentInput.LookRotation = new Vector2(0f, initalYaw);

        hasLocalPlayer = true;
        playerActions.Enable();

        SetCursorCaptured(Application.isFocused);
        return true;
    }

    public void StopLocalInput()
    {
        // 실행되지 않은 Runner 복사본이 다른 입력을 건드리지 않게 함
        if (!hasLocalPlayer)
        {
            return;
        }

        hasLocalPlayer = false;
        currentInput.Move = Vector2.zero;
        playerActions.Disable();

        SetCursorCaptured(false);
    }

    private void Update()
    {
        if (!hasLocalPlayer || !Application.isFocused)
        {
            currentInput.Move = Vector2.zero;
            return;
        }

        // Esc를 누를 때마다 커서 잠금과 해제 전환
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorCaptured(!isCursorCaptured);
            currentInput.Move = Vector2.zero;
            return;
        }

        // 커서를 풀어놓은 동안에는 이동과 시점 입력 멈춤
        if(!isCursorCaptured || Cursor.lockState != CursorLockMode.Locked)
        {
            currentInput.Move = Vector2.zero;
            return;
        }

        currentInput.Move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (jumpAction.WasPressedThisFrame())
        {
            currentInput.JumpSequence++;
        }

        Vector2 mouseDelta = lookAction.ReadValue<Vector2>();

        // 마우스 위아래 입력을 누적하고 회전 범위 제한
        currentInput.LookRotation.x = Mathf.Clamp(
            currentInput.LookRotation.x - mouseDelta.y * mouseSensitivity, -maxLookAngle, maxLookAngle);

        // 좌우 각도는 360도 범위로 지정
        currentInput.LookRotation.y = Mathf.Repeat(
            currentInput.LookRotation.y + mouseDelta.x * mouseSensitivity, 360f);
    }

    private void HandleInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkPlayerInputData data = currentInput;
        data.IsReady = hasLocalPlayer;

        // 창을 전환하거나 커서를 풀면 즉시 이동을 끊음
        if (!hasLocalPlayer ||
            !Application.isFocused ||
            !isCursorCaptured ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            data.Move = Vector2.zero;
        }

        // Fusion이 이 입력을 틱별로 저장하고 호스트에 전달
        input.Set(data);
    }

    private void SetCursorCaptured(bool captured)
    {
        isCursorCaptured = captured;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnApplicationFocus(bool hasfocus)
    {
        if(hasLocalPlayer && !hasfocus)
        {
            currentInput.Move = Vector2.zero;
            SetCursorCaptured(false);
        }
    }

    private void HandleShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        StopLocalInput();
    }

    private void OnDisable()
    {
        StopLocalInput();
    }

    private void OnDestroy()
    {
        if (networkEvents != null)
        {
            networkEvents.OnInput.RemoveListener(HandleInput);
            networkEvents.OnShutdown.RemoveListener(HandleShutdown);
        }

        playerActions?.Dispose();
    }
}
