using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(NetworkCharacterController))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Local Camera")]
    [SerializeField] private Transform cameraTransform;

    private NetworkCharacterController networkController;
    private NetworkInputProvider inputProvider;
    private bool isLocalPlayer;

    [Networked] private uint LastJumpSequence { get; set; }

    [Networked] private NetworkBool IsJumping { get; set; }

    private void Awake()
    {
        networkController = GetComponent<NetworkCharacterController>();

        networkController.rotationSpeed = 0f;
    }

    public override void Spawned()
    {
        isLocalPlayer = HasInputAuthority;

        // 다른 사람의 캐릭터는 이 프로그램의 입력을 사용하지 않음
        if (!isLocalPlayer)
        {
            return;
        }

        inputProvider = Runner.GetComponent<NetworkInputProvider>();

        if (cameraTransform == null ||
            inputProvider == null ||
            !inputProvider.StartLocalInput(transform.eulerAngles.y))
        {
            Debug.LogError("NetworkPlayerMovement: Camera Transform과 Runner의 입력 설정을 확인하세요.", this);

            isLocalPlayer = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority && !HasInputAuthority)
        {
            return;
        }

        // 이번 틱의 점프를 적용하기 전에 불필요한 상승 속도를 제거한다.
        StabilizeVerticalVelocity();

        Vector3 moveDirection = Vector3.zero;

        if (GetInput(out NetworkPlayerInputData data) && data.IsReady)
        {
            Vector2 move = Vector2.ClampMagnitude(data.Move, 1f);

            Quaternion facingRotation =
                Quaternion.Euler(0f, data.LookRotation.y, 0f);

            transform.rotation = facingRotation;
            moveDirection =
                facingRotation * new Vector3(move.x, 0f, move.y);

            // 새로운 요청 번호가 들어왔을 때만 점프를 시도한다.
            bool jumpRequested =
                data.JumpSequence != LastJumpSequence;

            // 공중에서 누른 요청도 처리한 것으로 기록한다.
            // 그래야 착지하자마자 뒤늦게 점프하지 않는다.
            LastJumpSequence = data.JumpSequence;

            if (jumpRequested && networkController.Grounded)
            {
                IsJumping = true;
                networkController.Jump();
            }
        }

        // 이동은 이 호출 한 곳에서 처리한다.
        // 입력이 없는 틱에도 중력과 감속이 계속 적용된다.
        networkController.Move(moveDirection);
    }

    // 지형의 턱이나 경사를 오르면서 생긴 상승 속도가
    // 다음 틱의 가짜 점프로 이어지지 않도록 보정한다.
    // 직접 요청한 점프 중에는 공중의 상승 속도를 유지한다.
    private void StabilizeVerticalVelocity()
    {
        Vector3 velocity = networkController.Velocity;

        if (networkController.Grounded)
        {
            // 땅에 닿으면 이전 점프를 종료하고 수직 속도를 초기화한다.
            // 실제 점프 요청은 이 함수 다음에 적용된다.
            IsJumping = false;
            velocity.y = 0f;
        }
        else if (!IsJumping && velocity.y > 0f)
        {
            // 점프하지 않았는데 생긴 위쪽 속도만 제거한다.
            // 아래로 떨어지는 속도는 유지하므로 낙하는 정상 작동한다.
            velocity.y = 0f;
        }

        networkController.Velocity = velocity;
    }

    private void LateUpdate()
    {
        if(!isLocalPlayer ||
            inputProvider == null ||
            cameraTransform == null)
        {
            return;
        }

        Vector2 lookRotation = inputProvider.LookRotation;

        // 자신의 카메라는 매 화면 프레임에 최신 각도를 표시
        cameraTransform.rotation = Quaternion.Euler(lookRotation.x, lookRotation.y, 0f);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (isLocalPlayer && inputProvider != null)
        {
            inputProvider.StopLocalInput();
        }

        isLocalPlayer = false;
    }
}
