public interface IAiThemeMapBuilder
{
    string Theme { get; }
    AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request);
}
