using System;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSessionController : MonoBehaviour
{
    // 호스트를 포함한 전체 참가 인원.
    public const int MaxPlayers = 4;

    [Header("Runner")]
    [SerializeField] private NetworkRunner runnerPrefab;

    private NetworkRunner runner;
    private NetworkEvents networkEvents;

    private int initialSceneIndex;
    private bool shutdownPending;
    private bool isQuitting;
    private ShutdownReason shutdownReason;

    // 씬을 다시 불러온 뒤에도 종료 안내 문구를 전달한다.
    private static string nextLobbyMessage;

    public bool IsBusy { get; private set; }
    public bool IsInRoom { get; private set; }
    public string RoomCode { get; private set; } = "";
    public string StatusMessage { get; private set; } = "";

    public int PlayerCount =>
        runner != null && runner.IsRunning
            ? runner.ActivePlayers.Count()
            : 0;

    // 현재 씬을 로비 복귀 지점으로 기록하고 대기 화면의 커서를 복원한다.
    private void Awake()
    {
        initialSceneIndex = gameObject.scene.buildIndex;

        StatusMessage = string.IsNullOrEmpty(nextLobbyMessage)
            ? "Create a room or enter a room code."
            : nextLobbyMessage;

        nextLobbyMessage = null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 새 6자리 방 코드를 만들고 호스트로 접속한다.
    public Task CreateRoomAsync()
    {
        string code = Guid.NewGuid()
            .ToString("N")
            .Substring(0, 6)
            .ToUpperInvariant();

        return StartSessionAsync(GameMode.Host, code);
    }

    // 사용자가 입력한 방 코드의 공백과 대소문자를 정리한 뒤 참가한다.
    public Task JoinRoomAsync(string code)
    {
        if (IsBusy || runner != null)
        {
            return Task.CompletedTask;
        }

        code = code?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            StatusMessage = "Enter a 6-character room code.";
            return Task.CompletedTask;
        }

        return StartSessionAsync(GameMode.Client, code);
    }

    // 자신의 연결을 종료한다. 클라이언트가 나가면 다른 참가자는 계속 플레이한다.
    public Task LeaveRoomAsync()
    {
        if (IsBusy || runner == null)
        {
            return Task.CompletedTask;
        }

        return ReturnToLobbyAsync("You left the room.");
    }

    // 새 Runner로 접속하고, 실패하면 사용한 Runner를 정리한 뒤 로비로 돌아간다.
    private async Task StartSessionAsync(GameMode mode, string code)
    {
        if (IsBusy || runner != null)
        {
            return;
        }

        // 현재 구성에 필요한 컴포넌트가 프리팹에 있는지 먼저 검사한다.
        if (initialSceneIndex < 0 ||
            runnerPrefab == null ||
            runnerPrefab.GetComponent<NetworkEvents>() == null ||
            runnerPrefab.GetComponent<NetworkSceneManagerDefault>() == null ||
            runnerPrefab.GetComponent<NetworkInputProvider>() == null)
        {
            StatusMessage = "Check the Runner prefab and build scene settings.";

            Debug.LogError(
                "NetworkSessionController: Runner 프리팹의 컴포넌트와 Build Profiles의 씬 목록을 확인하세요.",
                this);

            return;
        }

        IsBusy = true;
        shutdownPending = false;
        RoomCode = code;

        StatusMessage = mode == GameMode.Host
            ? "Creating room..."
            : "Joining room...";

        StartGameResult result;

        try
        {
            // 프리팹은 원본으로 보관하고 실제 연결에는 새 복사본을 사용한다.
            runner = Instantiate(runnerPrefab);
            runner.name = "GameRunner";
            DontDestroyOnLoad(runner.gameObject);

            networkEvents = runner.GetComponent<NetworkEvents>();
            networkEvents.OnShutdown.AddListener(HandleShutdown);

            NetworkSceneManagerDefault sceneManager =
                runner.GetComponent<NetworkSceneManagerDefault>();

            // 이미 열려 있는 테스트 씬을 사용하여 같은 씬의 중복 로드를 막는다.
            sceneManager.IsSceneTakeOverEnabled = true;

            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(
                SceneRef.FromIndex(initialSceneIndex),
                LoadSceneMode.Additive);

            result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = code,
                PlayerCount = MaxPlayers,
                Scene = sceneInfo,
                SceneManager = sceneManager,

                // 클라이언트는 없는 방을 새로 만들지 않고 기존 방에만 참가한다.
                EnableClientSessionCreation = false,

                // 방 목록에는 노출하지 않지만 방 코드로 참가할 수 있다.
                IsVisible = false,
                IsOpen = true
            });
        }
        catch (Exception exception)
        {
            if (this == null || isQuitting || !Application.isPlaying)
            {
                return;
            }

            Debug.LogException(exception, this);
            await ReturnToLobbyAsync("Connection failed. Please try again.");
            return;
        }

        if (this == null || isQuitting || !Application.isPlaying)
        {
            return;
        }

        // 접속 실패 또는 접속 도중 종료된 경우 게임 화면으로 넘어가지 않는다.
        if (!result.Ok || shutdownPending)
        {
            ShutdownReason reason = result.Ok
                ? shutdownReason
                : result.ShutdownReason;

            await ReturnToLobbyAsync(GetShutdownMessage(reason));
            return;
        }

        IsInRoom = true;
        IsBusy = false;
        StatusMessage = "Connected.";
    }

    // Fusion 종료 콜백에서는 종료 사실만 기록한다.
    // 콜백 실행 중에 씬을 교체하지 않고 다음 Update에서 정리한다.
    private void HandleShutdown(
        NetworkRunner stoppedRunner,
        ShutdownReason reason)
    {
        if (stoppedRunner != runner || isQuitting)
        {
            return;
        }

        shutdownReason = reason;
        shutdownPending = true;
    }

    // 호스트 종료나 예기치 않은 연결 종료를 감지하여 로비 복귀를 시작한다.
    private async void Update()
    {
        if (!shutdownPending || IsBusy || isQuitting)
        {
            return;
        }

        shutdownPending = false;

        try
        {
            await ReturnToLobbyAsync(GetShutdownMessage(shutdownReason));
        }
        catch (Exception exception)
        {
            StatusMessage = "Could not return to lobby. Restart the game.";
            Debug.LogException(exception, this);
        }
    }

    // 입력을 멈추고 Runner 종료를 기다린 뒤 현재 씬을 다시 불러온다.
    // 씬을 새로 불러와 생성 지점 기록과 로비 UI도 초기 상태로 복원한다.
    private async Task ReturnToLobbyAsync(string message)
    {
        IsBusy = true;
        IsInRoom = false;
        StatusMessage = "Returning to lobby...";

        if (networkEvents != null)
        {
            networkEvents.OnShutdown.RemoveListener(HandleShutdown);
        }

        NetworkRunner oldRunner = runner;

        if (oldRunner != null)
        {
            oldRunner.GetComponent<NetworkInputProvider>()?.StopLocalInput();

            await oldRunner.Shutdown();

            // 종료 과정에서 예약된 GameObject 제거까지 기다린다.
            while (oldRunner != null)
            {
                if (this == null || isQuitting || !Application.isPlaying)
                {
                    return;
                }

                await Task.Yield();
            }
        }

        if (this == null || isQuitting || !Application.isPlaying)
        {
            return;
        }

        runner = null;
        networkEvents = null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        nextLobbyMessage = message;
        SceneManager.LoadSceneAsync(initialSceneIndex, LoadSceneMode.Single);
    }

    // 연결 종료 원인을 사용자가 이해할 수 있는 안내 문구로 바꾼다.
    private string GetShutdownMessage(ShutdownReason reason)
    {
        return reason switch
        {
            ShutdownReason.GameNotFound =>
                "Room not found. Check the room code.",

            ShutdownReason.GameIsFull =>
                "The room is full.",

            ShutdownReason.GameClosed =>
                "The room has been closed.",

            ShutdownReason.GameIdAlreadyExists =>
                "Room creation failed. Please try again.",

            ShutdownReason.ConnectionTimeout or
            ShutdownReason.PhotonCloudTimeout or
            ShutdownReason.OperationTimeout =>
                "Connection timed out. Please try again.",

            _ => "Connection ended. Create or join a room again."
        };
    }

    // 프로그램을 닫는 동안에는 종료 콜백이 로비 씬을 다시 열지 않게 한다.
    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // 이 컴포넌트가 제거되면 자신이 등록했던 이벤트 연결도 해제한다.
    private void OnDestroy()
    {
        if (networkEvents != null)
        {
            networkEvents.OnShutdown.RemoveListener(HandleShutdown);
        }
    }
}