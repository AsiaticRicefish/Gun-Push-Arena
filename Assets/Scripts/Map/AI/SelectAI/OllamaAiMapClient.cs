using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class OllamaAiMapClient : IAiMapClient
{
    private const string DefaultEndPoint = "http://localhost:11434/api/generate";
    private const string DefaultModel = "qwen2.5:3b";

    private readonly string endpoint;
    private readonly string model;
    private readonly AiMapTheme selectedTheme;
    private readonly IntentBasedAiMapBuilder mapBuilder;

    public OllamaAiMapClient()
        : this(AiMapTheme.Balanced, DefaultEndPoint, DefaultModel)
    {
    }

    public OllamaAiMapClient(string endpoint, string model)
        : this(AiMapTheme.Balanced, endpoint, model)
    {
    }

    public OllamaAiMapClient(AiMapTheme selectedTheme)
        : this(selectedTheme, DefaultEndPoint, DefaultModel)
    {
    }

    public OllamaAiMapClient(AiMapTheme selectedTheme, string endpoint, string model)
    {
        this.endpoint = endpoint;
        this.model = model;
        this.selectedTheme = selectedTheme;
        mapBuilder = new IntentBasedAiMapBuilder();
    }

    public async Task<AiMapGenerateResponse> GenerateMapAsync(AiMapGenerateRequest request)
    {
        if (request == null)
        {
            return Fail("AI map request is null.");
        }

        string prompt = BuildIntentPrompt(request);
        string requestJson = JsonUtility.ToJson(new OllamaGenerateRequest
        {
            model = model,
            prompt = prompt,
            stream = false
        });

        using UnityWebRequest webRequest = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
        webRequest.timeout = 60;
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        try
        {
            await SendWebRequestAsync(webRequest);
        }
        catch (Exception ex)
        {
            return Fail($"Ollama request failed: {ex.Message}");
        }

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            return Fail($"Ollama request failed: {webRequest.error}, Body: {webRequest.downloadHandler.text}");
        }

        if (!TryParseOllamaResponse(webRequest.downloadHandler.text, out AiMapIntentDto intent, out string errorMessage))
        {
            return Fail(errorMessage);
        }

        if (request.seed != 0)
        {
            intent.seed = request.seed;
        }

        AiMapLayoutDto map = mapBuilder.Build(intent, request);

        return new AiMapGenerateResponse
        {
            success = true,
            errorMessage = string.Empty,
            map = map
        };
    }

    private string BuildIntentPrompt(AiMapGenerateRequest request)
    {
        string selectedThemeId = ToThemeId(selectedTheme);
        int seed = request != null ? request.seed : Environment.TickCount;

        return $@"
You create a compact JSON option intent for a Unity 2D arena map.

Return only valid JSON.
Do not use markdown.
Do not explain anything.
Do not create tile arrays.

The selected map structure preset is ""{selectedThemeId}"".
You must keep theme exactly ""{selectedThemeId}"".
Only choose variation options inside this selected structure preset.
Use seed {seed} for this generation.

Allowed values:
- theme: ""balanced"", ""split"", ""vertical"", ""chaos""
- routeCount: integer from 0 to 3
- spawnDistance: ""close"", ""medium"", ""far""
- dangerLevel: ""low"", ""medium"", ""high""
- wallDensity: ""none"", ""low"", ""medium""
- platformScale: ""small"", ""medium"", ""large""
- seed: positive integer

Interpret Korean and English user text.
Choose variation options that fit the selected structure preset.
Do not change the selected structure preset.
Pick different seed values to create different map variations.

Output schema:
{{
  ""theme"": ""{selectedThemeId}"",
  ""routeCount"": 1,
  ""spawnDistance"": ""far"",
  ""dangerLevel"": ""medium"",
  ""wallDensity"": ""none"",
  ""platformScale"": ""medium"",
  ""seed"": {seed}
}}
";
    }

    private bool TryParseOllamaResponse(string responseJson, out AiMapIntentDto intent, out string errorMessage)
    {
        intent = null;

        OllamaGenerateResponse ollamaResponse;

        try
        {
            ollamaResponse = JsonUtility.FromJson<OllamaGenerateResponse>(responseJson);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to parse Ollama response: {ex.Message}";
            return false;
        }

        if (ollamaResponse == null || string.IsNullOrWhiteSpace(ollamaResponse.response))
        {
            errorMessage = "Ollama response was empty.";
            return false;
        }

        if (!TryExtractJsonObject(ollamaResponse.response, out string intentJson))
        {
            errorMessage = "Failed to extract intent JSON from Ollama response.";
            return false;
        }

        try
        {
            intent = JsonUtility.FromJson<AiMapIntentDto>(intentJson);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to parse AI map intent JSON: {ex.Message}";
            return false;
        }

        if (intent == null)
        {
            errorMessage = "Parsed AI map intent was null.";
            return false;
        }

        NormalizeIntent(intent);
        errorMessage = string.Empty;
        return true;
    }

    private void NormalizeIntent(AiMapIntentDto intent)
    {
        intent.theme = ToThemeId(selectedTheme);
        intent.spawnDistance = NormalizeOption(intent.spawnDistance, "far", "close", "medium", "far");
        intent.dangerLevel = NormalizeOption(intent.dangerLevel, "medium", "low", "medium", "high");
        intent.wallDensity = NormalizeOption(intent.wallDensity, "none", "none", "low", "medium");
        intent.platformScale = NormalizeOption(intent.platformScale, "medium", "small", "medium", "large");
        intent.routeCount = Mathf.Clamp(intent.routeCount, 0, 3);
        if (intent.seed == 0 || intent.seed == 12345)
        {
            intent.seed = Environment.TickCount;
        }

        if (intent.theme == "balanced" && intent.routeCount <= 0)
        {
            intent.routeCount = 1;
        }
    }

    private string NormalizeOption(string value, string fallback, params string[] allowedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string normalized = value.Trim().ToLowerInvariant();

        for (int i = 0; i < allowedValues.Length; i++)
        {
            if (normalized == allowedValues[i])
            {
                return normalized;
            }
        }

        return fallback;
    }

    private string ToThemeId(AiMapTheme theme)
    {
        switch (theme)
        {
            case AiMapTheme.Split:
                return "split";

            case AiMapTheme.Vertical:
                return "vertical";

            case AiMapTheme.Chaos:
                return "chaos";

            default:
                return "balanced";
        }
    }

    private bool TryExtractJsonObject(string text, out string json)
    {
        json = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start < 0 || end < 0 || end <= start)
        {
            return false;
        }

        json = text.Substring(start, end - start + 1);
        return true;
    }

    private AiMapGenerateResponse Fail(string message)
    {
        return new AiMapGenerateResponse
        {
            success = false,
            map = null,
            errorMessage = message
        };
    }

    private static Task SendWebRequestAsync(UnityWebRequest request)
    {
        TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        operation.completed += _ =>
        {
            completionSource.SetResult(true);
        };

        return completionSource.Task;
    }

    [Serializable]
    private class OllamaGenerateRequest
    {
        public string model;
        public string prompt;
        public bool stream;
    }

    [Serializable]
    private class OllamaGenerateResponse
    {
        public string response;
        public bool done;
    }
}
