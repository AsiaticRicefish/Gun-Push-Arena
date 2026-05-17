using UnityEngine;

/// <summary>
/// IAiMapClient로 AI 맵 응답 받기
/// -> AiMapLayoutParser로 MapLayoutData 변환
/// -> MapLayoutValidator로 검증
/// -> 실패하면 DefaultMapGenerator 사용
/// </summary>
public class AiMapGenerator : IMapGenerator
{
     private readonly IAiMapClient aiMapClient; // AI 맵 응답을 받아오는 역할
    private readonly MapLayoutValidator validator; // AI가 만든 맵이 실제 게임 규칙을 만족하는지 검사
    private readonly DefaultMapGenerator defaultMapGenerator; // AI 응답 실패, 파싱 실패, 검증 실패 시 사용할 fallback
    private readonly AiMapGenerateRequest request; // 방장 프롬프트, roomId, width, height 같은 요청 데이터

    public AiMapGenerator(
        IAiMapClient aiMapClient,
        MapLayoutValidator validator,
        DefaultMapGenerator defaultMapGenerator,
        AiMapGenerateRequest request)
    {
        this.aiMapClient = aiMapClient;
        this.validator = validator;
        this.defaultMapGenerator = defaultMapGenerator;
        this.request = request;
    }

    public MapLayoutData Generate()
    {
        AiMapGenerateResponse response = aiMapClient.GenerateMap(request);

        if (response == null)
        {
            Debug.LogWarning("AI 맵 생성 응답이 null입니다. 기본 맵을 사용합니다.");
            return defaultMapGenerator.Generate();
        }

        if (!response.success)
        {
            Debug.LogWarning($"AI 맵 생성에 실패했습니다. 기본 맵을 사용합니다. Reason: {response.errorMessage}");
            return defaultMapGenerator.Generate();
        }

        if (!AiMapLayoutParser.ToMapLayoutData(response.map, out MapLayoutData layout, out string parseErrorMessage))
        {
            Debug.LogWarning($"AI 맵 데이터 변환에 실패했습니다. 기본 맵을 사용합니다. Reason: {parseErrorMessage}");
            return defaultMapGenerator.Generate();
        }

        if (!validator.Validate(layout, out string validationErrorMessage))
        {
            Debug.LogWarning($"AI 맵 검증에 실패했습니다. 기본 맵을 사용합니다. Reason: {validationErrorMessage}");
            return defaultMapGenerator.Generate();
        }

        return layout;
    }
}