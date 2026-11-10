using System.Text.RegularExpressions;

namespace CohEngine.Ai.Tests;

public sealed class NoSecretsTests
{
    private static readonly string[] TextExtensions = [".cs", ".csproj", ".props", ".json", ".yml", ".yaml", ".md", ".txt", ".xml", ".slnx", ".sh", ".ps1"];

    // The second pattern is assembled from parts so this file does not match itself.
    private static readonly Regex[] Patterns =
    [
        new("sk-[A-Za-z0-9]{20,}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5)),
        new("(ANTHROPIC|OPENAI)" + "_API_KEY=\\S+", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5)),
    ];

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
                .Where(file => TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) && !IsBuildOutput(file))
                .Where(file => Patterns.Any(pattern => pattern.IsMatch(File.ReadAllText(file)))));
        }

        Assert.Empty(hits);
    }
}
