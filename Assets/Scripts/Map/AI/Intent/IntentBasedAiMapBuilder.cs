public sealed class IntentBasedAiMapBuilder
{
    private readonly IAiThemeMapBuilder[] builders;

    public IntentBasedAiMapBuilder()
    {
        builders = new IAiThemeMapBuilder[]
        {
            new BalancedMapBuilder(),
            new SplitMapBuilder(),
            new VerticalMapBuilder(),
            new ChaosMapBuilder()
        };
    }

    public AiMapLayoutDto Build(AiMapIntentDto intent, AiMapGenerateRequest request)
    {
        string theme = AiThemeMapBuilderUtility.Normalize(intent?.theme, "balanced");

        for (int i = 0; i < builders.Length; i++)
        {
            if (builders[i].Theme == theme)
            {
                return builders[i].Build(intent, request);
            }
        }

        return builders[0].Build(intent, request);
    }
}
