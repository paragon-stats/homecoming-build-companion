using CohEngine.Core.Data;

namespace CohEngine.Core.Tests;

/// <summary>Synthetic records with the Python suite's <c>conftest.py</c> defaults; override with <c>with</c>.</summary>
internal static class Make
{
    public static Effect Effect { get; } = new()
    {
        Index = 0,
        EffectType = "Defense",
        DamageType = "Smashing",
        MezType = "None",
        EtModifies = "None",
        Scale = 1.0,
        NMagnitude = 1.0,
        NDuration = 0.0,
        AttribType = "Magnitude",
        Aspect = "Cur",
        ModifierTable = "Ones",
        NModifierTable = 0,
        ToWho = "Self",
        PvMode = "Any",
        Stacking = "No",
        Suppression = 0,
        Buffable = true,
        Resistible = false,
        IgnoreEd = false,
        IgnoreScaling = false,
        VariableModified = false,
        BaseProbability = 1.0,
        Probability = 1.0,
        ProcsPerMinute = 0.0,
        Ticks = 0,
        DelayedTime = 0.0,
        EffectClass = "Primary",
        SpecialCase = "None",
        NIdClassName = -1,
        AbsorbedEffect = false,
        AbsorbedPowerType = "Auto_",
        AbsorbedClassNId = -1,
        ActiveConditionalsCount = 0,
        ExprMagnitude = string.Empty,
        ExprDuration = string.Empty,
        ExprProbability = string.Empty,
        DisplayPercentage = false,
    };

    /// <summary>A power holding <paramref name="effects"/>; its pick level is its level, as <c>make_power</c> sets it.</summary>
    public static Power Power(params Effect[] effects)
    {
        return new Power
        {
            BuildIndex = 0,
            NidPower = 1,
            FullName = "Test.Test_Set.Test_Power",
            StaticIndex = 1,
            PowerType = "Toggle",
            ForcedClass = string.Empty,
            ClickBuff = false,
            Level = 1,
            EndCost = 0.0,
            ActivatePeriod = 0.0,
            ToggleCost = 0.0,
            VariableEnabled = false,
            StatInclude = true,
            VariableValue = 0,
            Effects = effects,
            SetTypes = [],
            Enhancements = [],
            PickLevel = 1,
            RechargeTime = 0.0,
            CastTime = 0.0,
            InterruptTime = 0.0,
            IgnoreEnh = [],
        };
    }

    public static KeyValuePair<string, string> Condition(string key, string value)
    {
        return new(key, value);
    }
}
