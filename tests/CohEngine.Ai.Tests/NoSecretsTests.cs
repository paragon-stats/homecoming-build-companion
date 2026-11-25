using System.Text.RegularExpressions;

namespace CohEngine.Ai.Tests;

public sealed partial class NoSecretsTests
{
    private static readonly string[] TextExtensions = [".cs", ".csproj", ".props", ".json", ".yml", ".yaml", ".md", ".txt", ".xml", ".slnx", ".sh", ".ps1"];

    private static readonly Regex[] Patterns = [ProviderKey, KeyAssignment];

    [GeneratedRegex("sk-[A-Za-z0-9]{20,}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex ProviderKey { get; }

    // The pattern is assembled from parts so this file does not match itself.
    [GeneratedRegex("(ANTHROPIC|OPENAI)" + "_API_KEY=\\S+", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex KeyAssignment { get; }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [Fact]
    public void No_api_key_appears_in_source_tests_or_workflows()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Join(root.FullName, "CohEngine.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        List<string> hits = [];
        foreach (string folder in new[] { "src", "tests", ".github" })
        {
            hits.AddRange(Directory.EnumerateFiles(Path.Join(root.FullName, folder), "*", SearchOption.AllDirectories)
                .Where(file => TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) && !IsBuildOutput(file) && Patterns.Any(pattern => pattern.IsMatch(File.ReadAllText(file)))));
        }

        Assert.Empty(hits);
    }
}
