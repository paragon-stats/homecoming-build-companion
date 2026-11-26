using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Tests.Chain;

// Expected text is json.dumps(value, indent=2) from CPython 3.14.
public sealed class PyJsonTests
{
    // Rows by index: the values are not serializable, so xUnit enumerates the cases by their position.
    private static readonly (object? Value, string Expected)[] Cases =
    [
        (null, "null"),
        (true, "true"),
        (false, "false"),
        (7L, "7"),
        (1.0, "1.0"),
        (0.1, "0.1"),
        (double.PositiveInfinity, "Infinity"),
        (double.NegativeInfinity, "-Infinity"),
        (double.NaN, "NaN"),
        ("a\"b\\c\nd\re\tf\bg\fh", "\"a\\\"b\\\\c\\nd\\re\\tf\\bg\\fh\""),
        ("café \u0001 \u007f", "\"caf\\u00e9 \\u0001 \\u007f\""),
        (new PyObject(), "{}"),
        (new List<object?>(), "[]"),
        (new List<object?> { 1L, "x" }, "[\n  1,\n  \"x\"\n]"),
        (new PyObject { { "a", 1L }, { "b", new List<object?> { new PyObject { { "c", null } } } } }, "{\n  \"a\": 1,\n  \"b\": [\n    {\n      \"c\": null\n    }\n  ]\n}"),
    ];

    public static TheoryData<int> Values => [.. Enumerable.Range(0, Cases.Length)];

    [Theory]
    [MemberData(nameof(Values))]
    public void Dumps_matches_python_json_with_indent_2(int row)
    {
        (object? value, string expected) = Cases[row];
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
    public void Dumps_writes_one_line_and_keeps_non_ascii_text_as_json_dumps_does_by_default()
    {
        PyObject value = new() { { "a", new List<object?> { 1L, "é\u007f\u0001" } }, { "b", new PyObject() }, { "c", new List<object?>() } };

        Assert.Equal("{\"a\": [1, \"é\u007f\\u0001\"], \"b\": {}, \"c\": []}", PyJson.Dumps(value, indented: false, ensureAscii: false));
        Assert.Equal("[\n  \"\\u00e9\"\n]", PyJson.Dumps(new List<object?> { "é" }, indented: true, ensureAscii: true));
    }

    [Fact]
    public void Loads_keeps_key_order_number_kinds_and_the_last_value_of_a_repeated_key()
    {
        PyObject value = (PyObject)PyJson.Loads("{\"b\": 1, \"a\": [1.5, \"x\", true, false, null], \"b\": 2}")!;

        Assert.Equal("{\"b\": 2, \"a\": [1.5, \"x\", true, false, null]}", PyJson.Dumps(value, indented: false, ensureAscii: false));
        Assert.IsType<long>(value["b"]);
        Assert.Throws<KeyNotFoundException>(() => value["absent"]);
        Assert.Equal("fallback", value.Get("absent", "fallback"));
    }

    [Theory]
    [InlineData("null", false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("\"\"", false)]
    [InlineData("\"x\"", true)]
    [InlineData("0", false)]
    [InlineData("3", true)]
    [InlineData("0.0", false)]
    [InlineData("0.5", true)]
    [InlineData("[]", false)]
    [InlineData("[0]", true)]
    [InlineData("{}", false)]
    public void Truthy_is_python_s_truth_value(string json, bool expected)
    {
        Assert.Equal(expected, PyObject.Truthy(PyJson.Loads(json)));
    }

    [Fact]
    public void Truthy_counts_nan_an_int_and_any_other_value_as_python_does()
    {
        Assert.True(PyObject.Truthy(double.NaN));
        Assert.False(PyObject.Truthy(0));
        Assert.True(PyObject.Truthy(DateTime.UnixEpoch));
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

        Assert.Equal("rule_id,severity,path,line,column,message", string.Join(',', ChainReport.DiagnosticJson(diagnostic).Select(pair => pair.Key)));
    }

    [Fact]
    public void A_diagnostic_adds_fix_expected_and_actual_when_set()
    {
        Diagnostic diagnostic = new("P-X-001", "warning", "message") { Path = "build", Fix = "do this", Expected = ">= 1", Actual = "0" };

        Assert.Equal("rule_id,severity,path,line,column,message,fix,expected,actual", string.Join(',', ChainReport.DiagnosticJson(diagnostic).Select(pair => pair.Key)));
    }

    [Fact]
    public void A_diagnostic_serializes_with_integer_line_and_column()
    {
        Diagnostic diagnostic = new("P-X-001", "info", "m") { Path = "b" };

        Assert.Equal("{\n  \"rule_id\": \"P-X-001\",\n  \"severity\": \"info\",\n  \"path\": \"b\",\n  \"line\": 1,\n  \"column\": 1,\n  \"message\": \"m\"\n}", PyJson.Dumps(ChainReport.DiagnosticJson(diagnostic)));
    }
}
