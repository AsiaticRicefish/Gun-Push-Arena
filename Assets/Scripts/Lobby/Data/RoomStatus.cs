using UnityEngine;

/// <summary>
/// 방의 현재 단계를 표현합니다.
/// </summary>

public enum RoomStatus
{
    Waiting, // 방 생성 후 대기 중
    GeneratingMap, // 방장이 AI 맵 생성 중
    MapReady, // 최종 맵 생성 완료
    Starting, // 게임 시작 요청됨, Relay/Netcode 연결 준비
    InGame, // 실제 게임 진행 중
    Closed // 방 종료됨
}