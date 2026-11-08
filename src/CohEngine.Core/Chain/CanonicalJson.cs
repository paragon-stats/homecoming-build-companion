using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CohEngine.Core.Chain;

/// <summary>Looks a power or entity up by name; null when the data has none.</summary>
/// <param name="name">A power full name (dotted) or an entity definition name.</param>
/// <returns>The record, or null.</returns>
public delegate JsonElement? PowerResolver(string name);

/// <summary>Reads and navigates the canonical data records (parsed JSON), with the Python mapping semantics the planner relies on.</summary>
public static class CanonicalJson
{
    /// <summary>The value of a key that must exist.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    public static JsonElement Req(this JsonElement element, string key)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out JsonElement value) ? value : throw new MissingKeyException(key);
    }

    /// <summary>The value of a key that may be absent or null.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null.</returns>
    public static JsonElement? Opt(this JsonElement element, string key)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value : null;
    }

    /// <summary>The items of an array key; empty when absent or null.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The items.</returns>
    public static IReadOnlyList<JsonElement> Items(this JsonElement element, string key)
    {
        JsonElement? value = element.Opt(key);
        return value is null ? [] : [.. value.Value.EnumerateArray()];
    }

    /// <summary>The items of an array key that must exist.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The items.</returns>
    public static IReadOnlyList<JsonElement> ReqItems(this JsonElement element, string key)
    {
        return [.. element.Req(key).EnumerateArray()];
    }

    /// <summary>The strings of an array key; empty when absent or null.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The strings.</returns>
    public static IReadOnlyList<string> Strings(this JsonElement element, string key)
    {
        return [.. element.Items(key).Select(item => item.GetString() ?? string.Empty)];
    }

    /// <summary>A string key with a default when absent or null.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The text.</returns>
    public static string Str(this JsonElement element, string key, string fallback = "")
    {
        return element.Opt(key)?.GetString() ?? fallback;
    }

    /// <summary>A number key with a default when absent or null.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The number.</returns>
    public static double Num(this JsonElement element, string key, double fallback)
    {
        return element.Opt(key)?.GetDouble() ?? fallback;
    }

    /// <summary>A number key that must exist.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The number.</returns>
    public static double ReqNum(this JsonElement element, string key)
    {
        return element.Req(key).GetDouble();
    }

    /// <summary>A string key that must exist.</summary>
    /// <param name="element">The record.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text.</returns>
    public static string ReqStr(this JsonElement element, string key)
    {
        return element.Req(key).GetString() ?? string.Empty;
    }

    /// <summary>Parses a JSON file (BOM tolerated); a corrupt file raises the given code naming the path.</summary>
    /// <param name="path">The file.</param>
    /// <param name="code">The error code.</param>
    /// <returns>The root element.</returns>
    public static JsonElement ReadJson(string path, string code)
    {
        string text = File.ReadAllText(path, new UTF8Encoding(false));
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            throw new ChainException($"{code}: {path} is not valid JSON ({error.Message}); re-run tools/mids-dump or restore the file", error);
        }
    }

    /// <summary>The canonical file path for a power full name.</summary>
    /// <param name="powersRoot">The powers directory.</param>
    /// <param name="fullName">The full name.</param>
    /// <returns>The path.</returns>
    public static string CanonicalPowerPath(string powersRoot, string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        string[] segments = [.. fullName.Split('.').Select(segment => segment.ToLowerInvariant().Replace(':', '_'))];
        return Path.Combine([powersRoot, .. segments[..^1], segments[^1] + ".json"]);
    }

    /// <summary>Loads the canonical power JSON for each full name.</summary>
    /// <param name="powersRoot">The powers directory.</param>
    /// <param name="fullNames">The names.</param>
    /// <returns>The records by name, in input order.</returns>
    /// <exception cref="ChainException">E35 naming every name with no non-empty file; E38 for a corrupt file when no other candidate matches.</exception>
    public static IReadOnlyDictionary<string, JsonElement> LoadCanonicalPowers(string powersRoot, IEnumerable<string> fullNames)
    {
        ArgumentNullException.ThrowIfNull(fullNames);
        Dictionary<string, JsonElement> found = new(StringComparer.Ordinal);
        List<string> missing = [];
        foreach (string name in fullNames)
        {
            string derived = CanonicalPowerPath(powersRoot, name);
            IEnumerable<string> candidates = [derived];
            if (!File.Exists(derived) || new FileInfo(derived).Length == 0)
            {
                candidates = [.. Directory.EnumerateFiles(powersRoot, Path.GetFileName(derived), SearchOption.AllDirectories).Where(file => new FileInfo(file).Length > 0)];
            }

            JsonElement? data = null;
            ChainException? corrupt = null;
            foreach (string candidate in candidates)
            {
                JsonElement parsed;
                try
                {
                    parsed = ReadJson(candidate, "E38");
                }
                catch (ChainException error)
                {
                    corrupt = error;
                    continue;
                }

                if (string.Equals(parsed.Opt("full_name")?.GetString(), name, StringComparison.Ordinal))
                {
                    data = parsed;
                    break;
                }
            }

            if (data is null && corrupt is not null)
            {
                throw corrupt;
            }

            if (data is null)
            {
                missing.Add(name);
            }
            else
            {
                found[name] = data.Value;
            }
        }

        if (missing.Count > 0)
        {
            throw new ChainException(
                $"E35: no canonical power file under {powersRoot} for {PyText.Repr(missing)}; "
                + "check the full_name spelling or restore the file (0-byte files are skipped)");
        }

        return found;
    }

    /// <summary>A cached lookup from name to canonical record that gives null instead of raising.</summary>
    /// <param name="powersRoot">The powers directory; entity definitions sit in <c>entities/</c> beside it.</param>
    /// <returns>The resolver.</returns>
    public static PowerResolver CanonicalResolver(string powersRoot)
    {
        Dictionary<string, JsonElement?> cache = new(StringComparer.Ordinal);

        JsonElement? Load(string name)
        {
            if (name.Contains('.', StringComparison.Ordinal))
            {
                string path = CanonicalPowerPath(powersRoot, name);
                return new FileInfo(path).Length > 0 ? LoadCanonicalPowers(powersRoot, [name])[name] : null;
            }

            string entity = Path.Combine(Path.GetDirectoryName(powersRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty, "entities", name.ToLowerInvariant() + ".json");
            return new FileInfo(entity).Length > 0 ? ReadJson(entity, "E38") : null;
        }

        return name =>
        {
            if (!cache.TryGetValue(name, out JsonElement? value))
            {
                try
                {
                    value = Load(name);
                }
                catch (Exception error) when (error is ChainException or IOException or UnauthorizedAccessException)
                {
                    value = null;
                }

                cache[name] = value;
            }

            return value;
        };
    }
}

/// <summary>Python <c>repr</c> and truthiness helpers for strings and lists.</summary>
public static class PyText
{
    /// <summary>Python <c>repr(str)</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The quoted text.</returns>
    public static string Repr(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        char quote = text.Contains('\'', StringComparison.Ordinal) && !text.Contains('"', StringComparison.Ordinal) ? '"' : '\'';
        StringBuilder builder = new();
        builder.Append(quote);
        foreach (char letter in text)
        {
            builder.Append(letter == quote || letter == '\\' ? "\\" + letter : letter == '\n' ? "\\n" : letter == '\t' ? "\\t" : letter == '\r' ? "\\r" : letter.ToString());
        }

        return builder.Append(quote).ToString();
    }

    /// <summary>Python <c>repr(list[str])</c>.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The list text.</returns>
    public static string Repr(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return "[" + string.Join(", ", items.Select(Repr)) + "]";
    }

    /// <summary>Python <c>repr(int)</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Repr(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>A required key missing from an input record; the Python port's <c>KeyError</c>.</summary>
public sealed class MissingKeyException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MissingKeyException"/> class.</summary>
    /// <param name="key">The missing key.</param>
    public MissingKeyException(string key)
        : base(PyText.Repr(key))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MissingKeyException"/> class.</summary>
    public MissingKeyException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MissingKeyException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public MissingKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
