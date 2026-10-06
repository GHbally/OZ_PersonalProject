using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class LobbyUI : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] private NetworkSessionController sessionController;

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject gamePanel;

    [Header("Lobby")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Game")]
    [SerializeField] private Button leaveButton;
    [SerializeField] private TMP_Text roomCodeText;

    // Inspector 연결을 검사하고 버튼에 실행할 함수를 등록한다.
    private void Awake()
    {
        if (sessionController == null ||
            lobbyPanel == null ||
            gamePanel == null ||
            roomCodeInput == null ||
            createButton == null ||
            joinButton == null ||
            statusText == null ||
            leaveButton == null ||
            roomCodeText == null)
        {
            Debug.LogError("LobbyUI: Inspector의 모든 항목을 연결하세요.", this);
            enabled = false;
            return;
        }

        roomCodeInput.characterLimit = 6;

        createButton.onClick.AddListener(HandleCreateClicked);
        joinButton.onClick.AddListener(HandleJoinClicked);
        leaveButton.onClick.AddListener(HandleLeaveClicked);
    }

    // 접속 상태에 맞춰 화면을 전환하고 처리 중에는 중복 클릭을 막는다.
    private void Update()
    {
        bool inRoom = sessionController.IsInRoom;
        bool busy = sessionController.IsBusy;

        lobbyPanel.SetActive(!inRoom);
        gamePanel.SetActive(inRoom);

        createButton.interactable = !busy;
        joinButton.interactable = !busy;
        roomCodeInput.interactable = !busy;
        leaveButton.interactable = inRoom && !busy;

        statusText.text = sessionController.StatusMessage;

        if (inRoom)
        {
            roomCodeText.text =
                $"Room: {sessionController.RoomCode}   " +
                $"Players: {sessionController.PlayerCount}/" +
                $"{NetworkSessionController.MaxPlayers}";
        }
    }

    // Create Room 버튼을 누르면 호스트 방 생성을 요청한다.
    private void HandleCreateClicked()
    {
        RunOperation(sessionController.CreateRoomAsync());
    }

    // Join Room 버튼을 누르면 입력란의 방 코드로 참가를 요청한다.
    private void HandleJoinClicked()
    {
        RunOperation(sessionController.JoinRoomAsync(roomCodeInput.text));
    }

    // Leave Room 버튼을 누르면 자신의 연결 종료를 요청한다.
    private void HandleLeaveClicked()
    {
        RunOperation(sessionController.LeaveRoomAsync());
    }

    // 버튼에서 시작한 비동기 처리를 기다리고 예상하지 못한 오류를 기록한다.
    private async void RunOperation(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    // UI가 제거될 때 자신이 등록했던 버튼 이벤트를 해제한다.
    private void OnDestroy()
    {
        if (createButton != null)
        {
            createButton.onClick.RemoveListener(HandleCreateClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(HandleJoinClicked);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
        }
    }
}