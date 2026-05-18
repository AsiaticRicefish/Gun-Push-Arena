using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// AI 맵 생성 요청을 보내고 응답 받기
/// </summary>
public interface IAiMapClient
{
    Task<AiMapGenerateResponse> GenerateMapAsync(AiMapGenerateRequest request);
}