using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    private const int MaxPlayers = 4;

    [Header("Player")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints = new Transform[MaxPlayers];

    // 각 참가자가 사용하는 생성 위치를 기록하여 중복 생성을 방지함.
    private readonly Dictionary<PlayerRef, int> playerSlots = new();
    private readonly NetworkObject[] playerObjects = new NetworkObject[MaxPlayers];

    // 참가자 목록과 퇴장 목록은 매 틱 재사용한다.
    private readonly HashSet<PlayerRef> activePlayers = new();
    private readonly List<PlayerRef> departedPlayers = new();

    private bool isConfigured;

    public override void Spawned()
    {
        // 캐릭터를 생성하는 호스트만 설정을 검사
        if (!HasStateAuthority)
        {
            return;
        }

        // 모든 검사가 끝나기 전까지 캐릭터 생성을 막음
        isConfigured = false;

        // 플레이어 프리팹과 최대 인원만큼의 생성 지점 필요
        if (playerPrefab == null ||
            spawnPoints == null ||
            spawnPoints.Length != MaxPlayers)
        {
            Debug.LogError(
                $"NetworkPlayerSpawner: 플레이어 프리팹과 생성 지점 {MaxPlayers}개를 연결해주세요.",this);
            return;
        }

        // 배열의 첫 번째부터 마지막 생성 지점까지 하나씩 검사
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            // Inspector에서 연결하지 않은 항목이 있으면 중단
            if (spawnPoints[i] == null)
            {
                Debug.LogError(
                    $"NetworkPlayerSpawner: Spawn Points의 Element {i}에 생성 지점을 연결해주세요.",this);
                return;
            }

            // 현재 항목을 앞서 검사한 항목들과 비교
            for (int j = 0; j < i; j++)
            {
                if (spawnPoints[i] == spawnPoints[j])
                {
                    Debug.LogError(
                        $"NetworkPlayerSpawner: Element {j}와 Element {i}에 같은 생성 지점이 연결되어 있습니다.",this);
                    return;
                }
            }
        }
        isConfigured = true;
    }

    public override void FixedUpdateNetwork()
    {
        // Host Mode에서는 호스트만 생성과 제거를 처리함
        if (!HasStateAuthority || !isConfigured)
        {
            return;
        }

        activePlayers.Clear();

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            activePlayers.Add(player);
        }

        RemoveDepartedPlayers();

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (playerSlots.ContainsKey(player))
            {
                continue;
            }

            int slot = FindAvailableSlot();

            if (slot < 0)
            {
                break;
            }

            Transform spawnPoint = spawnPoints[slot];

            NetworkObject playerObject = Runner.Spawn(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                inputAuthority: player
            );

            if (playerObject == null)
            {
                isConfigured = false;
                Debug.LogError("NetworkPlayerSpawner: 플레이어 생성에 실패했습니다.", this);
                return;
            }

            playerObjects[slot] = playerObject;
            playerSlots.Add(player, slot);

            // 참가자와 캐릭터를 연결하여 이후 시스템에서 조회할 수 있게 한다.
            Runner.SetPlayerObject(player, playerObject);

        }
    }

    private int FindAvailableSlot()
    {
        for (int i = 0; i < playerObjects.Length; i++)
        {
            if (playerObjects[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void RemoveDepartedPlayers()
    {
        departedPlayers.Clear();


        foreach (PlayerRef player in playerSlots.Keys)
        {
            if (!activePlayers.Contains(player))
            {
                departedPlayers.Add(player);
            }
        }

        // 순회 중인 Dictionary를 직접 변경하지 않도록 목록을 나눠 처리한다.
        foreach (PlayerRef player in departedPlayers)
        {
            int slot = playerSlots[player];

            if (playerObjects[slot] != null)
            {
                Runner.Despawn(playerObjects[slot]);
            }

            playerObjects[slot] = null;
            playerSlots.Remove(player);
        }
    }
}
