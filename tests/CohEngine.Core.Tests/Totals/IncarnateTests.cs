using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals.Incarnate;

using IncarnatePower = CohEngine.Core.Data.Incarnate;

namespace CohEngine.Core.Tests.Totals;

/// <summary>
/// Port of <c>test_incarnate.py</c>: routing, gating and the refusals, over the tiny tables whose Ones table makes an
/// effect's magnitude its scale.
/// </summary>
public sealed class IncarnateTests
{
    private static readonly Effect Base = Make.Effect;

    [Theory]
    [InlineData("RechargeTime", new string[0], "RechargeTime")]
    [InlineData("RechargeTime", new[] { "RechargeTime" }, null)]
    [InlineData("InterruptTime", new[] { "Interrupt" }, "Interrupt")]
    [InlineData("Range", new string[0], "Range")]
    [InlineData("Accuracy", new string[0], "Accuracy")]
    [InlineData("EnduranceDiscount", new[] { "EnduranceDiscount" }, null)]
    [InlineData("Defense", new string[0], null)]
    public void Scalar_aspect_routes_or_drops(string etModifies, string[] ignoreEnh, string? want)
    {
        Effect effect = Base with { EffectType = "Enhancement", EtModifies = etModifies };
        Assert.Equal(want, IncarnateResolver.ScalarAspect(effect, Make.Power() with { IgnoreEnh = ignoreEnh }));
    }

    [Theory]
    [InlineData("Enhancement", "Defense", "Smashing", "Defense", "Smashing", true, true)]
    [InlineData("Enhancement", "Defense", "Smashing", "Defense", "Fire", true, false)]
    [InlineData("DamageBuff", "None", "Smashing", "Resistance", "Smashing", true, true)]
    [InlineData("DamageBuff", "None", "Smashing", "Resistance", "Fire", true, false)]
    [InlineData("DamageBuff", "None", "Smashing", "Defense", "Smashing", true, false)]
    [InlineData("Enhancement", "Recovery", "None", "Recovery", "Smashing", true, true)]
    [InlineData("Enhancement", "Recovery", "Smashing", "Recovery", "Smashing", false, false)]
    [InlineData("Enhancement", "Recovery", "Smashing", "Defense", "Smashing", true, false)]
    public void Matches_effect_as_handle_default_incarnate_enh(string incType, string etModifies, string incDamage, string targetType, string targetDamage, bool buffable, bool want)
    {
        Effect inc = Base with { EffectType = incType, EtModifies = etModifies, DamageType = incDamage };
        Effect target = Base with { EffectType = targetType, DamageType = targetDamage, Buffable = buffable };
        Assert.Equal(want, IncarnateResolver.MatchesEffect(target, inc));
    }

    [Theory]
    [InlineData("DamageBuff", "Damage")]
    [InlineData("Enhancement", "Defense")]
    public void Supported_paths_pass(string effectType, string etModifies)
    {
        IncarnateResolver.GuardSupported(Base with { EffectType = effectType, EtModifies = etModifies }, "Sub");
    }

    [Theory]
    [InlineData("GrantPower", "None")]
    [InlineData("Enhancement", "Mez")]
    public void Unported_paths_are_refused(string effectType, string etModifies)
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => IncarnateResolver.GuardSupported(Base with { EffectType = effectType, EtModifies = etModifies }, "Sub"));
        Assert.StartsWith("E20", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pre_and_post_accumulate_independently()
    {
        Dictionary<string, (double PreEd, double PostEd)> bucket = new(StringComparer.Ordinal);
        IncarnateResolver.AddSplit(bucket, "k", 0.11, false);
        IncarnateResolver.AddSplit(bucket, "k", 0.22, true);
        IncarnateResolver.AddSplit(bucket, "k", 0.05, false);
        Assert.Equal((F32.Round(0.16), F32.Round(0.22)), bucket["k"]);
    }

    [Fact]
    public void Scalar_addend_split()
    {
        Effect pre = Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", Scale = 0.11 };
        Effect post = Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", Scale = 0.22, IgnoreEd = true };
        IncarnateAddends addends = Resolve(Sub(pre, post), Make.Power() with { Enhancements = [5] });
        Assert.Equal((F32.Round(0.11), F32.Round(0.22)), addends.Scalar[(0, "RechargeTime")]);
        Assert.Empty(addends.Effect);
    }

    [Fact]
    public void Effect_addend_is_raw_pre_post()
    {
        Effect pre = Base with { EffectType = "Enhancement", EtModifies = "Defense", DamageType = "Smashing", Scale = 0.0667 };
        Effect post = Base with { EffectType = "Enhancement", EtModifies = "Defense", DamageType = "Smashing", Scale = 0.1333, IgnoreEd = true };
        Effect target = Base with { Index = 0, EffectType = "Defense", DamageType = "Smashing", Buffable = true };
        IncarnateAddends addends = Resolve(Sub(pre, post), Make.Power(target) with { Enhancements = [5] });
        Assert.Equal((F32.Round(0.0667), F32.Round(0.1333)), addends.Effect[(0, 0)]);
        Assert.Empty(addends.Scalar);
    }

    [Fact]
    public void Gate_miss_contributes_nothing()
    {
        Effect effect = Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", Scale = 0.11 };
        IncarnateAddends addends = Resolve(Sub(effect), Make.Power() with { Enhancements = [18] });
        Assert.Empty(addends.Scalar);
        Assert.Empty(addends.Effect);
    }

    private static IncarnateSubPower Sub(params Effect[] effects)
    {
        return new IncarnateSubPower(new HashSet<int> { 5 }) { FullName = "Incarnate.Alpha_Silent.Test", Effects = effects };
    }

    private static IncarnateAddends Resolve(IncarnateSubPower sub, Power power)
    {
        IncarnatePower incarnate = new() { BuildIndex = 99, FullName = "Incarnate.Alpha.Test", SubPowers = [sub] };
        return IncarnateResolver.Resolve([incarnate], [power with { BuildIndex = 0 }], EffectMagnitudeTests.TinyMods, EffectMagnitudeTests.TinyClasses, 0);
    }
}
