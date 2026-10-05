using Fusion;
using UnityEngine;

public class NetworkPlayerView : NetworkBehaviour
{
    [Header("Local View")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener playerAudioListener;
    [SerializeField] private Renderer bodyRenderer;

    private bool isLocalPlayer;

    private Camera sceneCamera;
    private AudioListener sceneAudioListener;
    private bool sceneCameraWasEnabled;
    private bool sceneAudioWasEnabled;

    public override void Spawned()
    {
        if (playerCamera == null ||
            playerAudioListener == null ||
            bodyRenderer == null)
        {
            Debug.LogError("NetworkPlayerView: Camera, AudioListener, Body Renderer를 연결해주세요.", this);
            return;
        }

        isLocalPlayer = HasInputAuthority;

        if (isLocalPlayer)
        {
            // 플레이어 카메라를 켜기 전에 기존 대기 화면 카메라를 정리함.
            sceneCamera = Camera.main;

            if (sceneCamera != null && sceneCamera != playerCamera)
            {
                sceneCameraWasEnabled = sceneCamera.enabled;
                sceneAudioListener = sceneCamera.GetComponent<AudioListener>();

                sceneCamera.enabled = false;

                if (sceneAudioListener != null)
                {
                    sceneAudioWasEnabled = sceneAudioListener.enabled;
                    sceneAudioListener.enabled = false;
                }
            }
        }

        playerCamera.enabled = isLocalPlayer;
        playerAudioListener.enabled = isLocalPlayer;

        // 본인 body는 시야를 가리지 않도록 숨기고 상대방 몸체는 표시한다.
        bodyRenderer.enabled = !isLocalPlayer;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        if (playerAudioListener != null)
        {
            playerAudioListener.enabled = false;
        }

        if (!isLocalPlayer)
        {
            return;
        }

        // 로컬 캐릭터가 제거되면 기존 대기 화면 카메라 상태를 복원한다.
        if (sceneCamera != null && sceneCamera != playerCamera)
        {
            sceneCamera.enabled = sceneCameraWasEnabled;
        }


        if (sceneAudioListener != null)
        {
            sceneAudioListener.enabled = sceneAudioWasEnabled;
        }
    }
}
