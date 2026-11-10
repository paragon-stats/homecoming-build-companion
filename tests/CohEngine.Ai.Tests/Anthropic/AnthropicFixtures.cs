namespace CohEngine.Ai.Tests.Anthropic;

/// <summary>The hand-written Messages API fixtures under <c>tests/fixtures/ai/anthropic</c>.</summary>
internal static class AnthropicFixtures
{
    private static readonly Lazy<string> Directory = new(Locate);

    public static string PathOf(string name)
    {
        return Path.Join(Directory.Value, name);
    }

    public static string Read(string name)
    {
        return File.ReadAllText(PathOf(name));
    }

    private static string Locate()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Join(current.FullName, "CohEngine.slnx")))
        {
            current = current.Parent;
        }

        string root = current?.FullName ?? throw new InvalidOperationException("CohEngine.slnx not found above the test output.");
        return Path.Join(root, "tests", "fixtures", "ai", "anthropic");
    }
}
