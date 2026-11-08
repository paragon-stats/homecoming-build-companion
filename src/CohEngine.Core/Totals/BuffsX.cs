using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Totals;

/// <summary>
/// The working accumulator <c>Enums.BuffsX</c>, the subset the ported passes fill. <see cref="EffectAux"/> is the debuff
/// half of the enhancement pass's speed-scalar split.
/// </summary>
internal sealed class BuffsX
{
    /// <summary>Initializes a new instance of the <see cref="BuffsX"/> class, every bucket sized by its enum.</summary>
    /// <param name="enums">The enum maps.</param>
    public BuffsX(EnumMaps enums)
    {
        int effects = enums.Size("eEffectType");
        int mez = enums.Size("eMez");
        int damage = enums.Size("eDamage");
        Effect = new double[effects];
        EffectAux = new double[effects];
        Mez = new double[mez];
        MezRes = new double[mez];
        Damage = new double[damage];
        Defense = new double[damage];
        Resistance = new double[damage];
        Elusivity = new double[damage];
        StatusProtection = new double[mez];
        StatusResistance = new double[mez];
        DebuffResistance = new double[effects];
        Boosts = new double[effects];
        BoostsMez = new double[mez];
    }

    /// <summary>Gets the Effect bucket, written by eEffectType ordinal and read by eStatType ordinal.</summary>
    public double[] Effect { get; }

    /// <summary>Gets the debuff half of the speed scalars.</summary>
    public double[] EffectAux { get; }

    /// <summary>Gets the Mez bucket.</summary>
    public double[] Mez { get; }

    /// <summary>Gets the MezRes bucket.</summary>
    public double[] MezRes { get; }

    /// <summary>Gets the Damage bucket.</summary>
    public double[] Damage { get; }

    /// <summary>Gets the typed Defense bucket.</summary>
    public double[] Defense { get; }

    /// <summary>Gets the typed Resistance bucket.</summary>
    public double[] Resistance { get; }

    /// <summary>Gets the typed Elusivity bucket.</summary>
    public double[] Elusivity { get; }

    /// <summary>Gets the StatusProtection bucket.</summary>
    public double[] StatusProtection { get; }

    /// <summary>Gets the StatusResistance bucket.</summary>
    public double[] StatusResistance { get; }

    /// <summary>Gets the DebuffResistance bucket.</summary>
    public double[] DebuffResistance { get; }

    /// <summary>Gets the Boosts bucket.</summary>
    public double[] Boosts { get; }

    /// <summary>Gets the Boosts_Mez bucket.</summary>
    public double[] BoostsMez { get; }

    /// <summary>Gets or sets the MaxEnd accumulator.</summary>
    public double MaxEnd { get; set; }

    /// <summary>Adds a value to a bucket cell in float32.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="index">The cell.</param>
    /// <param name="value">The value.</param>
    public static void Add(double[] bucket, int index, double value)
    {
        bucket[index] = F32.Round(bucket[index] + value);
    }
}
