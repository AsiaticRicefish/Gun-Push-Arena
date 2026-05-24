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
        : this(AiMapTheme.Bridge, DefaultEndPoint, DefaultModel)
    {
    }

    public OllamaAiMapClient(string endpoint, string model)
        : this(AiMapTheme.Bridge, endpoint, model)
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
        string styleHint = SanitizeUserPrompt(request.prompt);

        return $@"
You create a compact JSON option intent for a Unity 2D arena map.

Return only valid JSON.
Do not use markdown.
Do not explain anything.
Do not create tile arrays.

The selected map theme is ""{selectedThemeId}"".
You must keep theme exactly ""{selectedThemeId}"".
Only choose variation options inside this selected theme.

Allowed values:
- theme: ""bridge"", ""island"", ""warehouse""
- bridgeCount: integer from 0 to 3
- spawnDistance: ""close"", ""medium"", ""far""
- dangerLevel: ""low"", ""medium"", ""high""
- wallDensity: ""none"", ""low"", ""medium""
- platformScale: ""small"", ""medium"", ""large""
- seed: positive integer

Interpret Korean and English user text.
Use the optional style hint only to tune dangerLevel, wallDensity, platformScale, bridgeCount, and seed.
Do not change the selected theme.
Pick different seed values to create different map variations.

Output schema:
{{
  ""theme"": ""{selectedThemeId}"",
  ""bridgeCount"": 1,
  ""spawnDistance"": ""far"",
  ""dangerLevel"": ""medium"",
  ""wallDensity"": ""none"",
  ""platformScale"": ""medium"",
  ""seed"": 12345
}}

Optional style hint:
{styleHint}
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
        intent.bridgeCount = Mathf.Clamp(intent.bridgeCount, 0, 3);
        if (intent.seed == 0 || intent.seed == 12345)
        {
            intent.seed = Environment.TickCount;
        }

        if (intent.theme == "bridge" && intent.bridgeCount <= 0)
        {
            intent.bridgeCount = 1;
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
            case AiMapTheme.Island:
                return "island";

            case AiMapTheme.Warehouse:
                return "warehouse";

            default:
                return "bridge";
        }
    }

    private string SanitizeUserPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "default balanced arena";
        }

        string sanitized = prompt.Trim();

        if (sanitized.Length > 300)
        {
            sanitized = sanitized.Substring(0, 300);
        }

        return sanitized;
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
