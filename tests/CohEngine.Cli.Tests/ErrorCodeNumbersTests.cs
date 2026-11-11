using System.Reflection;

using CohEngine.Ai;
using CohEngine.Cli.Ask;
using CohEngine.Core.CombatLog;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Cli.Tests;

public sealed class ErrorCodeNumbersTests
{
    [Fact]
    public void No_error_code_number_is_defined_twice_across_the_code_files()
    {
        Type[] codeFiles = [typeof(ErrorCodes), typeof(CombatLogErrorCodes), typeof(AiErrorCodes), typeof(AskErrorCodes)];

        List<(string Name, string Number)> codes = [.. codeFiles.SelectMany(Constants)];

        Assert.Equal(94, codes.Count);
        Assert.Contains(("AiErrorCodes+Anthropic.IncompleteStream", "E141"), codes);
        Assert.Empty(codes.GroupBy(code => code.Number, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"{group.Key}: {string.Join(", ", group.Select(code => code.Name))}"));
    }

    private static IEnumerable<(string Name, string Number)> Constants(Type type)
    {
        IEnumerable<(string Name, string Number)> own = type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.GetRawConstantValue() is string text && text.Length > 1 && text[0] == 'E' && text[1..].All(char.IsAsciiDigit))
            .Select(field => ($"{type.Name}.{field.Name}", (string)field.GetRawConstantValue()!));
        return own.Concat(type.GetNestedTypes().SelectMany(nested => Constants(nested).Select(code => ($"{type.Name}+{code.Name}", code.Number))));
    }
}
