using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_enhancement_parity.py</c>: enhanced per-power values and slotted totals. The generic IOs the
/// fixtures slot carry no set bonus, so the enhancement flows through the per-effect multiplier path only.
/// </summary>
[Trait("Category", "Parity")]
public sealed class EnhancementParityTests
{
    private const string Slotted = "shield_scrapper_slotted";
    private const string Hasten2Rech = "hasten_2rech";
    private const int HastenIndex = 6;

    [Theory]
    [InlineData(Slotted, 0.9908)]
    [InlineData(Hasten2Rech, 0.8332)]
    public void Hasten_buffed_recharge_matches(string name, double rounded)
    {
        IReadOnlyList<SlotRef> slots = Fixtures.Store.Build(name).Slots.Single(entry => entry.BuildIndex == HastenIndex).Slots;
        double aggregate = Pipeline.AggregateAndEd(slots, "RechargeTime", Fixtures.Store.EnhancementEffects, 50, Fixtures.Store.Maths);
        Assert.Equal(rounded, Math.Round(aggregate, 4));

        EnhancedPower dumped = Fixtures.Store.Build(name).EnhancedPowers.Single(power => power.BuildIndex == HastenIndex);
        double buffed = F32.Round(dumped.Base.RechargeTime / F32.Round(1.0 + aggregate));
        ParityAssert.Equal(dumped.RechargeTime, buffed, $"{name}.Hasten.RechargeTime");
    }
}
