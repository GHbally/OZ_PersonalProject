using Fusion;
using UnityEngine;

public struct NetworkPlayerInputData : INetworkInput
{
    // 캐릭터 생성 후 준비 확인
    public NetworkBool IsReady;

    public Vector2 Move;
    public Vector2 LookRotation;
    public uint JumpSequence;
}
