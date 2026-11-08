using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Maths;

// Expected cells were printed once by the Python port: load_maths("tests/fixtures/Maths.mhd").
public sealed class MathTablesTests
{
    [Fact]
    public void Load_reads_the_homecoming_tables_as_the_python_does()
    {
        MathTables tables = Fixtures.Store.Maths;

        Assert.Equal([0.699999988079071, 0.8999999761581421, 1.0], tables.MultEd[0]);
        Assert.Equal([0.4000000059604645, 0.5, 0.6000000238418579], tables.MultEd[1]);
        Assert.Equal([0.800000011920929, 1.0, 1.2000000476837158], tables.MultEd[2]);
        Assert.Equal([1.2000000476837158, 1.5, 1.7999999523162842], tables.MultEd[3]);
        Assert.Equal([0.3330000042915344, 0.20000000298023224, 0.4000000059604645, 0.6000000238418579], tables.MultSo);
        Assert.Equal([0.42399999499320984, 0.2549999952316284, 0.5090000033378601, 0.7639999985694885], tables.MultIo[49]);
        Assert.Equal(53, tables.MultIo.Count);
        Assert.Equal(4, tables.MultTo.Count);
        Assert.Equal(4, tables.MultDo.Count);
        Assert.Equal(4, tables.MultHo.Count);
    }

    [Theory]
    [InlineData("'abc", "'abc")]
    [InlineData("' x ", "' x")]
    [InlineData("\"1.5\" ", "1.5")]
    [InlineData("plain", "plain")]
    public void Strip_keeps_the_mids_quirk(string token, string expected)
    {
        Assert.Equal(expected, MathTables.Strip(token));
    }

    [Theory]
    [InlineData("0.1", 0.10000000149011612)]
    [InlineData("'0.5", 0.0)]
    [InlineData("", 0.0)]
    public void TryParseFloat_quantizes_or_yields_zero(string token, double expected)
    {
        Assert.Equal(expected, MathTables.TryParseFloat(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Version:\n")]
    [InlineData("Version:\nEDRT\n")]
    [InlineData("Version:\nEDRT\nT1\t1\t2\t3\t4\nT2\t1\nT3\t1\t2\t3\t4\n")]
    public void Parse_refuses_a_truncated_file(string text)
    {
        using StringReader reader = new(text);

        Assert.Throws<InvalidDataException>(() => MathTables.Parse(reader));
    }
}
