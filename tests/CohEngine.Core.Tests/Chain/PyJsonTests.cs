using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Tests.Chain;

// Expected text is json.dumps(value, indent=2) from CPython 3.14.
public sealed class PyJsonTests
{
    public static TheoryData<object?, string> Values => new()
    {
        { null, "null" },
        { true, "true" },
        { false, "false" },
        { 7L, "7" },
        { 1.0, "1.0" },
        { 0.1, "0.1" },
        { double.PositiveInfinity, "Infinity" },
        { double.NegativeInfinity, "-Infinity" },
        { double.NaN, "NaN" },
        { "a\"b\\c\nd\re\tf\bg\fh", "\"a\\\"b\\\\c\\nd\\re\\tf\\bg\\fh\"" },
        { "café \u0001 \u007f", "\"caf\\u00e9 \\u0001 \\u007f\"" },
        { new PyObject(), "{}" },
        { new List<object?>(), "[]" },
        { new List<object?> { 1L, "x" }, "[\n  1,\n  \"x\"\n]" },
        { new PyObject { { "a", 1L }, { "b", new List<object?> { new PyObject { { "c", null } } } } }, "{\n  \"a\": 1,\n  \"b\": [\n    {\n      \"c\": null\n    }\n  ]\n}" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void Dumps_matches_python_json_with_indent_2(object? value, string expected)
    {
        Assert.Equal(expected, PyJson.Dumps(value));
    }

    [Fact]
    public void Dumps_writes_ints_big_integers_and_int_and_string_lists()
    {
        PyObject value = new() { { "a", 7 }, { "b", System.Numerics.BigInteger.Pow(10, 20) }, { "c", new List<int> { 1, 2 } }, { "d", new List<string> { "x" } } };

        Assert.Equal("{\n  \"a\": 7,\n  \"b\": 100000000000000000000,\n  \"c\": [\n    1,\n    2\n  ],\n  \"d\": [\n    \"x\"\n  ]\n}", PyJson.Dumps(value));
    }

    [Fact]
    public void Dumps_can_write_non_finite_floats_as_python_repr_text()
    {
        Assert.Equal("[\n  nan,\n  inf,\n  -inf\n]", PyJson.Dumps(new List<object?> { double.NaN, double.PositiveInfinity, double.NegativeInfinity }, pythonReprNonFinite: true));
    }

    [Fact]
    public void Dumps_refuses_a_value_with_no_json_form()
    {
        Assert.Equal("no JSON form for DateTime (Parameter 'value')", Assert.Throws<ArgumentException>(() => PyJson.Dumps(DateTime.UnixEpoch)).Message);
    }

    [Fact]
    public void A_diagnostic_renders_its_required_keys_only_when_nothing_else_is_set()
    {
        Diagnostic diagnostic = new("P-X-001", "info", "message") { Path = "build" };

        Assert.Equal("rule_id,severity,path,line,column,message", string.Join(",", ChainReport.DiagnosticJson(diagnostic).Select(pair => pair.Key)));
    }

    [Fact]
    public void A_diagnostic_adds_fix_expected_and_actual_when_set()
    {
        Diagnostic diagnostic = new("P-X-001", "warning", "message") { Path = "build", Fix = "do this", Expected = ">= 1", Actual = "0" };

        Assert.Equal("rule_id,severity,path,line,column,message,fix,expected,actual", string.Join(",", ChainReport.DiagnosticJson(diagnostic).Select(pair => pair.Key)));
    }

    [Fact]
    public void A_diagnostic_serializes_with_integer_line_and_column()
    {
        Diagnostic diagnostic = new("P-X-001", "info", "m") { Path = "b" };

        Assert.Equal("{\n  \"rule_id\": \"P-X-001\",\n  \"severity\": \"info\",\n  \"path\": \"b\",\n  \"line\": 1,\n  \"column\": 1,\n  \"message\": \"m\"\n}", PyJson.Dumps(ChainReport.DiagnosticJson(diagnostic)));
    }
}
