using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Tests.Diagnostics;

public sealed class DiagnosticTests
{
    private static Diagnostic Full()
    {
        return new("H-ENH-003", "error", "enhancement class not accepted by power")
        {
            Line = 42,
            Fix = "remove the piece or move it to a power that accepts this class",
            Expected = new[] { 18 },
            Actual = new[] { 8, 11, 7, 19 },
        };
    }

    [Fact]
    public void Text_line_matches_the_error_output_format()
    {
        Assert.Equal("build.json:42:1: error: H-ENH-003: enhancement class not accepted by power", Full().FormatText());
    }

    [Fact]
    public void Json_object_carries_every_field_in_key_order()
    {
        string expected = string.Join(
            "\n",
            "[",
            "  {",
            "    \"rule_id\": \"H-ENH-003\",",
            "    \"severity\": \"error\",",
            "    \"path\": \"build.json\",",
            "    \"line\": 42,",
            "    \"column\": 1,",
            "    \"message\": \"enhancement class not accepted by power\",",
            "    \"fix\": \"remove the piece or move it to a power that accepts this class\",",
            "    \"expected\": [",
            "      18",
            "    ],",
            "    \"actual\": [",
            "      8,",
            "      11,",
            "      7,",
            "      19",
            "    ]",
            "  }",
            "]");

        Assert.Equal(expected, DiagnosticFormatter.FormatJson([Full()]));
    }

    [Fact]
    public void Json_omits_blank_optional_fields()
    {
        string json = DiagnosticFormatter.FormatJson([new("P-SLOT-001", "warning", "m")]);

        Assert.DoesNotContain("fix", json, StringComparison.Ordinal);
        Assert.DoesNotContain("expected", json, StringComparison.Ordinal);
        Assert.Contains("\"line\": 1,", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_of_nothing_is_an_empty_array()
    {
        Assert.Equal("[]", DiagnosticFormatter.FormatJson([]));
    }

    [Fact]
    public void Default_location_is_build_json_line_1()
    {
        Assert.Equal("build.json:1:1: error: H-ENH-002: m", new Diagnostic("H-ENH-002", "error", "m").FormatText());
    }

    [Fact]
    public void An_invalid_severity_is_rejected_as_e10()
    {
        EngineException exception = Assert.Throws<EngineException>(() => new Diagnostic("X", "fatal", "m"));

        Assert.StartsWith("E10: diagnostic severity 'fatal' is not one of ['error', 'warning', 'info']", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_joins_a_list_one_per_line()
    {
        Diagnostic[] diagnostics = [new("A", "error", "first"), new("B", "warning", "second")];

        Assert.Equal("build.json:1:1: error: A: first\nbuild.json:1:1: warning: B: second", DiagnosticFormatter.FormatText(diagnostics));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("info", 0)]
    [InlineData("warning", 2)]
    [InlineData("info,warning", 2)]
    [InlineData("error", 1)]
    [InlineData("warning,error", 1)]
    public void Exit_code_prioritizes_error_then_warning(string severities, int expected)
    {
        Diagnostic[] diagnostics = [.. severities.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(severity => new Diagnostic("R", severity, "m"))];

        Assert.Equal(expected, DiagnosticFormatter.ExitCode(diagnostics));
    }

    [Fact]
    public void Json_writes_scalars_escapes_and_python_floats()
    {
        Diagnostic diagnostic = new("R", "info", "café \"q\" \\ \n\r\t\b\f \u0001")
        {
            Expected = 2.083,
            Actual = true,
        };
        Diagnostic other = new("R", "info", "m") { Expected = 7L, Actual = Array.Empty<string>() };
        Diagnostic strings = new("R", "info", "m") { Expected = new[] { "a" }, Actual = 3.0 };

        string json = DiagnosticFormatter.FormatJson([diagnostic, other, strings]);

        Assert.Contains("caf\\u00e9 \\\"q\\\" \\\\ \\n\\r\\t\\b\\f \\u0001", json, StringComparison.Ordinal);
        Assert.Contains("\"expected\": 2.083,", json, StringComparison.Ordinal);
        Assert.Contains("\"actual\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"expected\": 7,", json, StringComparison.Ordinal);
        Assert.Contains("\"actual\": []", json, StringComparison.Ordinal);
        Assert.Contains("\"actual\": 3.0", json, StringComparison.Ordinal);
        Assert.Contains("\"a\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_refuses_a_value_it_has_no_form_for()
    {
        Diagnostic diagnostic = new("R", "info", "m") { Expected = new object() };

        Assert.Throws<ArgumentException>(() => DiagnosticFormatter.FormatJson([diagnostic]));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => DiagnosticFormatter.FormatText(null!));
        Assert.Throws<ArgumentNullException>(() => DiagnosticFormatter.FormatJson(null!));
        Assert.Throws<ArgumentNullException>(() => DiagnosticFormatter.ExitCode(null!));
        Assert.Throws<ArgumentNullException>(() => new Diagnostic(null!, "error", "m"));
        Assert.Throws<ArgumentNullException>(() => new Diagnostic("R", null!, "m"));
        Assert.Throws<ArgumentNullException>(() => new Diagnostic("R", "error", null!));
    }

    [Fact]
    public void The_engine_exception_carries_its_message_and_cause()
    {
        InvalidOperationException cause = new("inner");

        Assert.Equal("E99: x", new EngineException("E99: x").Message);
        Assert.Same(cause, new EngineException("E99: x", cause).InnerException);
        Assert.NotNull(new EngineException().Message);
    }
}
