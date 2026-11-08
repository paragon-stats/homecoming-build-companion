using System.Text.Json;

using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_mids_parity.py</c>: the Maths.mhd loader against MidsReborn's own in-memory tables, dumped as
/// <c>maths.json</c>, cell by cell at single precision, and the dumped server constants.
/// </summary>
[Trait("Category", "Parity")]
public sealed class MidsParityTests
{
    private static MathTables Tables => Fixtures.Store.Maths;

    public static TheoryData<string> GradeTables => ["MultTO", "MultDO", "MultSO", "MultHO"];

    [Fact]
    public void Mult_ed_matches_mids()
    {
        using JsonDocument maths = Load("maths.json");
        for (int schedule = 0; schedule < 4; schedule++)
        {
            for (int threshold = 0; threshold < 3; threshold++)
            {
                ParityAssert.Equal(maths.RootElement.GetProperty("MultED")[schedule][threshold].GetDouble(), Tables.MultEd[schedule][threshold], $"MultED[{schedule}][{threshold}]");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GradeTables))]
    public void Grade_tables_match_mids(string name)
    {
        IReadOnlyList<double> row = name switch
        {
            "MultTO" => Tables.MultTo,
            "MultDO" => Tables.MultDo,
            "MultSO" => Tables.MultSo,
            _ => Tables.MultHo,
        };
        using JsonDocument maths = Load("maths.json");
        for (int schedule = 0; schedule < 4; schedule++)
        {
            ParityAssert.Equal(maths.RootElement.GetProperty(name)[0][schedule].GetDouble(), row[schedule], $"{name}[0][{schedule}]");
        }
    }

    [Fact]
    public void Mult_io_matches_mids()
    {
        using JsonDocument maths = Load("maths.json");
        JsonElement rows = maths.RootElement.GetProperty("MultIO");
        Assert.Equal(53, rows.GetArrayLength());
        Assert.Equal(53, Tables.MultIo.Count);
        for (int row = 0; row < 53; row++)
        {
            for (int schedule = 0; schedule < 4; schedule++)
            {
                ParityAssert.Equal(rows[row][schedule].GetDouble(), Tables.MultIo[row][schedule], $"MultIO[{row}][{schedule}]");
            }
        }
    }

    [Fact]
    public void Server_data_constants()
    {
        using JsonDocument server = Load("server_data.json");
        Assert.Equal(0.75, server.RootElement.GetProperty("BaseToHit").GetDouble());
        Assert.Equal(67, server.RootElement.GetProperty("MaxSlots").GetInt32());
        Assert.False(server.RootElement.GetProperty("EnableInherentSlotting").GetBoolean());
    }

    [Fact]
    public void Mids_dump_is_version_stamped()
    {
        using JsonDocument version = Load("version.json");
        Assert.Equal("Homecoming", version.RootElement.GetProperty("DatabaseName").GetString());
        Assert.False(string.IsNullOrEmpty(version.RootElement.GetProperty("DatabaseVersion").ToString()));
    }

    private static JsonDocument Load(string file)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures.Store.MidsDirectory, file)));
    }
}
