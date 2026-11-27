using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.PatchNotes;

/// <summary>
/// A site's robots.txt rules, read as RFC 9309 sets them and as Python's <c>urllib.robotparser</c> applies them: the
/// group for the agent, else the <c>*</c> group; within it the longest matching rule wins, and on a tie, allow.
/// </summary>
internal sealed partial class RobotsRules
{
    private readonly List<Group> groups = [];

    /// <summary>Initializes a new instance of the <see cref="RobotsRules"/> class from the file's lines.</summary>
    /// <param name="lines">The robots.txt lines; none for a site without rules.</param>
    public RobotsRules(IEnumerable<string> lines)
    {
        Group entry = new();
        int state = 0;
        foreach ((string key, string value) in Fields(lines))
        {
            if (key is "user-agent")
            {
                if (state == 2)
                {
                    Add(entry);
                    entry = new Group();
                }

                entry.Agents.Add(value);
                state = 1;
            }
            else if (key is "disallow" or "allow" or "crawl-delay" or "request-rate" && state != 0)
            {
                state = 2;
                if (Rule.TryCreate(key, value) is { } rule)
                {
                    entry.Rules.Add(rule);
                }
            }
        }

        if (state != 0)
        {
            Add(entry);
        }
    }

    [GeneratedRegex("[^=&]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QueryRun { get; }

    [GeneratedRegex("[^*$]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PatternRun { get; }

    [GeneratedRegex("[^=&*$]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PatternQueryRun { get; }

    [GeneratedRegex(@"\*{2,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Stars { get; }

    [GeneratedRegex(@"\$[$*]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Dollars { get; }

    /// <summary>Whether <paramref name="agent"/> may fetch <paramref name="url"/>.</summary>
    /// <param name="agent">The user agent.</param>
    /// <param name="url">The absolute URL.</param>
    /// <returns>True when allowed.</returns>
    public bool CanFetch(string agent, string url)
    {
        string target = NormalizeUri(PathOf(url));
        if (target.Length == 0)
        {
            target = "/";
        }

        if (target is "/robots.txt")
        {
            return true;
        }

        Group? group = Find(agent);
        return group is null || group.Allows(target);
    }

    /// <summary>Each <c>key: value</c> line, comment dropped, key in lower case; a line with no colon is skipped.</summary>
    private static IEnumerable<(string Key, string Value)> Fields(IEnumerable<string> lines)
    {
        foreach (string raw in lines)
        {
            int hash = raw.IndexOf('#', StringComparison.Ordinal);
            string line = PyText.Strip(hash >= 0 ? raw[..hash] : raw);
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon >= 0)
            {
                yield return (PyText.Strip(line[..colon]).ToLowerInvariant(), PyText.Strip(line[(colon + 1)..]));
            }
        }
    }

    /// <summary>Percent-decodes then percent-encodes, so equal paths compare equal: letters, digits, <c>_.-~</c> and <c>/</c> stay as they are.</summary>
    private static string Normalize(string text)
    {
        List<byte> bytes = [];
        int index = 0;
        while (index < text.Length)
        {
            int width;
            if (text[index] == '%' && index + 2 < text.Length && byte.TryParse(text.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte value))
            {
                bytes.Add(value);
                width = 3;
            }
            else
            {
                width = char.IsSurrogatePair(text, index) ? 2 : 1;
                bytes.AddRange(Encoding.UTF8.GetBytes(text.Substring(index, width)));
            }

            index += width;
        }

        StringBuilder builder = new();
        foreach (byte value in bytes)
        {
            char letter = (char)value;
            builder.Append(char.IsAsciiLetterOrDigit(letter) || letter is '_' or '.' or '-' or '~' or '/' ? letter.ToString() : "%" + value.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>A request path: the path normalized, and each run of a query between <c>=</c> and <c>&amp;</c>.</summary>
    private static string NormalizeUri(string path)
    {
        int question = path.IndexOf('?', StringComparison.Ordinal);
        return question < 0 ? Normalize(path) : Normalize(path[..question]) + "?" + QueryRun.Replace(path[(question + 1)..], match => Normalize(match.Value));
    }

    /// <summary>A rule path: as <see cref="NormalizeUri"/>, leaving <c>*</c> and <c>$</c> as they are.</summary>
    private static string NormalizePattern(string path)
    {
        int question = path.IndexOf('?', StringComparison.Ordinal);
        string head = PatternRun.Replace(question < 0 ? path : path[..question], match => Normalize(match.Value));
        return question < 0 ? head : head + "?" + PatternQueryRun.Replace(path[(question + 1)..], match => Normalize(match.Value));
    }

    /// <summary>The URL from its path on: path, then <c>?query</c> and <c>#fragment</c> when present.</summary>
    private static string PathOf(string url)
    {
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0)
        {
            return url;
        }

        int start = url.IndexOfAny(['/', '?', '#'], scheme + 3);
        return start < 0 ? string.Empty : url[start..];
    }

    /// <summary>Adds a group; a second group for the same agent merges into the first.</summary>
    private void Add(Group entry)
    {
        foreach (string agent in entry.Agents.Select(agent => agent.ToLowerInvariant()))
        {
            int index = groups.FindIndex(group => string.Equals(group.Key, agent, StringComparison.Ordinal));
            if (index < 0)
            {
                groups.Add(entry with { Key = agent });
            }
            else
            {
                Group merged = new() { Key = agent };
                merged.Agents.AddRange(groups[index].Agents.Where(entry.Agents.Contains));
                merged.Rules.AddRange([.. groups[index].Rules, .. entry.Rules]);
                groups[index] = merged;
            }
        }
    }

    private Group? Find(string agent)
    {
        string lowered = agent.ToLowerInvariant();
        return groups.Find(group => string.Equals(group.Key, lowered, StringComparison.Ordinal))
            ?? groups.Find(group => group.AppliesTo(agent))
            ?? groups.Find(group => group.Key is "*");
    }

    /// <summary>One group: its agents and rules, filed under one lower-case agent name.</summary>
    private sealed record Group
    {
        public string Key { get; init; } = string.Empty;

        public List<string> Agents { get; init; } = [];

        public List<Rule> Rules { get; init; } = [];

        /// <summary>Whether a named agent of the group is part of the agent's product token.</summary>
        public bool AppliesTo(string agent)
        {
            string token = agent.Split('/')[0].ToLowerInvariant();
            return Agents.Exists(name => name is not "*" && token.Contains(name.ToLowerInvariant(), StringComparison.Ordinal));
        }

        /// <summary>The longest matching rule's verdict; allow on a tie and when none matches.</summary>
        public bool Allows(string target)
        {
            int best = -1;
            bool allowance = true;
            foreach (Rule rule in Rules)
            {
                int length = rule.Match(target);
                if (length > 0 && length > best)
                {
                    best = length;
                    allowance = rule.Allowance;
                }
                else if (length > 0 && length == best && !allowance)
                {
                    allowance = rule.Allowance;
                }
            }

            return allowance;
        }
    }

    /// <summary>One allow or disallow line.</summary>
    private sealed record Rule(string Path, bool Allowance, bool FullMatch, Regex? Pattern)
    {
        /// <summary>The rule for a line, or null when the line is not an allow or disallow line or a <c>$</c> sits before its value's end.</summary>
        public static Rule? TryCreate(string key, string value)
        {
            if (key is not ("allow" or "disallow"))
            {
                return null;
            }

            bool allowance = key is "allow";
            string path = NormalizePattern(Dollars.Replace(Stars.Replace(value, "*"), "$"));
            bool full = path.EndsWith('$');
            path = path.TrimEnd('$');
            if (path.Contains('$', StringComparison.Ordinal))
            {
                return null;
            }

            Regex? pattern = path.Contains('*', StringComparison.Ordinal) ? new Regex(Translate(path, full), RegexOptions.Singleline, TimeSpan.FromSeconds(1)) : null;
            return new Rule(path, allowance || value.Length == 0, full, pattern);
        }

        /// <summary>The matched length plus one, or 0 when the rule does not match.</summary>
        public int Match(string target)
        {
            if (Pattern is not null)
            {
                Match match = Pattern.Match(target);
                return match.Success ? match.Length + 1 : 0;
            }

            bool matches = FullMatch ? string.Equals(target, Path, StringComparison.Ordinal) : target.StartsWith(Path, StringComparison.Ordinal);
            return matches ? Path.Length + 1 : 0;
        }

        private static string Translate(string path, bool full)
        {
            string[] parts = [.. path.Split('*').Select(Regex.Escape)];
            for (int index = 1; index < parts.Length - 1; index++)
            {
                parts[index] = $"(?>.*?{parts[index]})";
            }

            parts[^1] = ".*" + parts[^1];
            return @"\A" + string.Concat(parts) + (full ? @"\z" : string.Empty);
        }
    }
}
