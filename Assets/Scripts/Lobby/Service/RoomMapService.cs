using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 현재 방 상태를 기준으로 AI 맵을 생성하고, 검증된 최종 맵을 Firestore room에 저장하는 서비스입니다.
/// Presenter가 Ollama, Validator, AiMapGenerator 같은 맵 생성 내부 구현을 직접 알지 않도록 분리합니다.
/// </summary>
public class RoomMapService
{
    // 현재 방 상태, 방장 권한, 최종 맵 저장은 RoomService를 통해 처리합니다.
    private readonly RoomService roomService;

    // AI가 생성한 맵이나 fallback 맵이 게임 규칙을 만족하는지 검사할 설정입니다.
    private readonly MapValidationSettings validationSettings;

    // 선택된 테마에 맞는 IAiMapClient를 만드는 함수입니다.
    // Controller에서 Ollama/Fake 등 원하는 구현체로 바꿔 끼울 수 있게 합니다.
    private readonly Func<AiMapTheme, IAiMapClient> aiMapClientFactory;

    public RoomMapService(
        RoomService roomService,
        MapValidationSettings validationSettings,
        Func<AiMapTheme, IAiMapClient> aiMapClientFactory)
    {
        this.roomService = roomService;
        this.validationSettings = validationSettings;
        this.aiMapClientFactory = aiMapClientFactory;
    }

    public async UniTask<bool> GenerateAndSaveMapAsync()
    {
        // 맵 생성은 현재 들어가 있는 방 정보를 기준으로 진행합니다.
        RoomState room = roomService.CurrentRoom;

        // 방 밖에서는 맵을 생성할 수 없습니다.
        if (room == null)
        {
            Debug.LogWarning("[RoomMapService] Current room is null.");
            return false;
        }

        // 맵 생성/저장은 방장 전용 작업입니다.
        if (!roomService.CanHostControlRoom())
        {
            Debug.LogWarning("[RoomMapService] Only host can generate map.");
            return false;
        }

        // Firestore에는 theme이 문자열로 저장되므로 AiMapTheme enum으로 파싱합니다.
        if (!TryParseTheme(room.SelectedTheme, out AiMapTheme theme))
        {
            Debug.LogWarning($"[RoomMapService] Invalid theme: {room.SelectedTheme}");
            return false;
        }

        // 다른 클라이언트가 "맵 생성 중" 상태를 볼 수 있도록 room status를 먼저 갱신합니다.
        await roomService.SetGeneratingMapAsync();

        // AiMapGenerator에 넘길 요청 데이터입니다.
        // prompt는 자유 맵 프롬프트가 아니라 선택 테마 안에서의 옵션 힌트로 사용합니다.
        AiMapGenerateRequest request = new AiMapGenerateRequest
        {
            roomId = room.RoomId,
            width = room.MapWidth,
            height = room.MapHeight,
            playerCount = room.PlayerCount
        };

        // 선택된 테마에 맞는 AI 클라이언트를 생성합니다.
        // 예: theme => new OllamaAiMapClient(theme)
        IAiMapClient aiMapClient = aiMapClientFactory(theme);

        // 맵 검증기와 fallback 생성기를 준비합니다.
        MapLayoutValidator validator = new MapLayoutValidator(validationSettings);
        DefaultMapGenerator defaultMapGenerator = new DefaultMapGenerator();

        // 기존 AI 맵 파이프라인을 그대로 사용합니다.
        // AI 응답 실패, 파싱 실패, 검증 실패 시 AiMapGenerator 내부에서 fallback을 사용할 수 있습니다.
        AiMapGenerator generator = new AiMapGenerator(
            aiMapClient,
            validator,
            defaultMapGenerator,
            request);

        // 최종적으로 게임에서 사용할 MapLayoutData를 생성합니다.
        MapLayoutData layout = await generator.GenerateAsync();

        // 방어 코드입니다. 정상적인 AiMapGenerator라면 fallback이라도 반환해야 합니다.
        if (layout == null)
        {
            Debug.LogWarning("[RoomMapService] Generated layout is null.");
            return false;
        }

        // AiMapGenerator 내부에서도 검증하지만, Firestore에 저장하기 직전에 한 번 더 확인합니다.
        if (!validator.Validate(layout, out string validationError))
        {
            Debug.LogWarning($"[RoomMapService] Final layout validation failed: {validationError}");
            return false;
        }

        // Firestore와 다른 클라이언트가 공유할 수 있는 DTO로 변환합니다.
        AiMapLayoutDto finalMap = AiMapLayoutConverter.ToDto(layout);

        // DTO 변환 실패 시 저장하지 않습니다.
        if (finalMap == null)
        {
            Debug.LogWarning("[RoomMapService] Failed to convert layout to dto.");
            return false;
        }

        // 최종 맵을 room 문서에 저장합니다.
        // 이 값이 모든 클라이언트가 공통으로 사용하는 확정 맵입니다.
        await roomService.SaveFinalMapAsync(finalMap);

        return true;
    }

    private bool TryParseTheme(string themeText, out AiMapTheme theme)
    {
        // Firestore string 값을 AiMapTheme enum 값으로 안전하게 변환합니다.
        return Enum.TryParse(themeText, out theme);
    }
}
