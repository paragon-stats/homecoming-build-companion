using System.Numerics;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Tests.Diagnostics;

public sealed class PyTextTests
{
    [Theory]
    [InlineData("plain", "'plain'")]
    [InlineData("it's", "\"it's\"")]
    [InlineData("a'b\"c", "'a\\'b\"c'")]
    [InlineData("back\\slash", "'back\\\\slash'")]
    [InlineData("tab\t\n\r\u0001\u007f", "'tab\\t\\n\\r\\x01\\x7f'")]
    public void Repr_quotes_a_string_as_python_does(string value, string expected)
    {
        Assert.Equal(expected, PyText.Repr(value));
    }

    [Theory]
    [InlineData(null, "None")]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    [InlineData("49", "49")]
    [InlineData(49, "49")]
    [InlineData(5L, "5")]
    [InlineData(2.5, "2.5")]
    public void Str_prints_a_json_value_as_python_does(object? value, string expected)
    {
        Assert.Equal(expected, PyText.Str(value));
    }

    [Fact]
    public void Repr_of_an_object_quotes_only_strings()
    {
        Assert.Equal("'x'", PyText.Repr((object)"x"));
        Assert.Equal("49", PyText.Repr((object)49));
        Assert.Equal("123456789012345678901234567890", PyText.Str(BigInteger.Parse("123456789012345678901234567890", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Str_refuses_a_value_with_no_python_form()
    {
        Assert.Throws<ArgumentException>(() => PyText.Str(new object()));
    }

    [Fact]
    public void Lists_print_as_python_lists()
    {
        Assert.Equal("[1, 2]", PyText.List(Enumerable.Range(1, 2)));
        Assert.Equal("[]", PyText.List(Enumerable.Empty<int>()));
        Assert.Equal("['a', \"b'\"]", PyText.List("a,b'".Split(',')));
        Assert.Throws<ArgumentNullException>(() => PyText.List((IEnumerable<int>)null!));
        Assert.Throws<ArgumentNullException>(() => PyText.List((IEnumerable<string>)null!));
        Assert.Throws<ArgumentNullException>(() => PyText.Repr((string)null!));
    }

    [Theory]
    [InlineData(0.0, "0.0")]
    [InlineData(-0.0, "-0.0")]
    [InlineData(1.0, "1.0")]
    [InlineData(45.0, "45.0")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(14.469999313354492, "14.469999313354492")]
    [InlineData(0.5, "0.5")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(0.00001, "1e-05")]
    [InlineData(1.5e-7, "1.5e-07")]
    [InlineData(1e16, "1e+16")]
    [InlineData(123456789012345680.0, "1.2345678901234568e+17")]
    [InlineData(1234567890123456.0, "1234567890123456.0")]
    [InlineData(100.0, "100.0")]
    [InlineData(double.NaN, "nan")]
    [InlineData(double.PositiveInfinity, "inf")]
    [InlineData(double.NegativeInfinity, "-inf")]
    public void Float_prints_the_python_repr(double value, string expected)
    {
        Assert.Equal(expected, PyText.NumberRepr(value));
    }
}
