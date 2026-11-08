namespace CohEngine.Core.Diagnostics;

/// <summary>The engine's stable error codes; each keeps its number and the message its Python module gives it.</summary>
public static class ErrorCodes
{
    /// <summary>A magnitude cannot be computed.</summary>
    public const string E08 = "E08";

    /// <summary>An unknown archetype class name.</summary>
    public const string E09 = "E09";

    /// <summary>A diagnostic severity outside error, warning and info.</summary>
    public const string E10 = "E10";

    /// <summary>The slot, enhancement and table inputs were not supplied together.</summary>
    public const string E11 = "E11";

    /// <summary>A set database was supplied without its companion inputs.</summary>
    public const string E14 = "E14";

    /// <summary>A damage-per-second input error.</summary>
    public const string E15 = "E15";

    /// <summary>A profile target references a breakpoint path that is absent or not numeric.</summary>
    public const string E16 = "E16";

    /// <summary>A profile target names an unknown scoring metric.</summary>
    public const string E17 = "E17";

    /// <summary>A profile target uses an unknown comparison operator.</summary>
    public const string E18 = "E18";

    /// <summary>An archetype name matches no canonical key.</summary>
    public const string E19 = "E19";

    /// <summary>An incarnate sub-power error.</summary>
    public const string E20 = "E20";

    /// <summary>Incarnates were supplied without the enhancement inputs.</summary>
    public const string E21 = "E21";

    /// <summary>A power name does not resolve.</summary>
    public const string E22 = "E22";

    /// <summary>A damage-source error.</summary>
    public const string E30 = "E30";

    /// <summary>A damage-model input error.</summary>
    public const string E31 = "E31";

    /// <summary>No canonical archetype file.</summary>
    public const string E32 = "E32";

    /// <summary>A canonical data error.</summary>
    public const string E33 = "E33";

    /// <summary>No canonical power file.</summary>
    public const string E35 = "E35";

    /// <summary>A damage-reference error.</summary>
    public const string E36 = "E36";

    /// <summary>A damage-reference error.</summary>
    public const string E37 = "E37";

    /// <summary>A damage-reference error.</summary>
    public const string E38 = "E38";

    /// <summary>An attack-profile error.</summary>
    public const string E40 = "E40";

    /// <summary>The build index differs between dump files.</summary>
    public const string E41 = "E41";

    /// <summary>An attack-profile error.</summary>
    public const string E42 = "E42";

    /// <summary>An attack-profile error.</summary>
    public const string E43 = "E43";

    /// <summary>A timeline availability error.</summary>
    public const string E45 = "E45";

    /// <summary>A build-spec pick error.</summary>
    public const string E46 = "E46";

    /// <summary>A build-spec error.</summary>
    public const string E47 = "E47";

    /// <summary>A build-spec error.</summary>
    public const string E48 = "E48";

    /// <summary>A build-spec slot-budget error.</summary>
    public const string E49 = "E49";

    /// <summary>A damage-model assumption is out of range.</summary>
    public const string E50 = "E50";

    /// <summary>An aura assumption error.</summary>
    public const string E55 = "E55";

    /// <summary>A hybrid melee-share assumption error.</summary>
    public const string E56 = "E56";

    /// <summary>A target resistance assumption error.</summary>
    public const string E57 = "E57";

    /// <summary>A pet entity error.</summary>
    public const string E60 = "E60";

    /// <summary>A pet entity or table error.</summary>
    public const string E61 = "E61";

    /// <summary>A pet damage error.</summary>
    public const string E62 = "E62";

    /// <summary>A pet damage error.</summary>
    public const string E63 = "E63";

    /// <summary>A chain report error.</summary>
    public const string E64 = "E64";

    /// <summary>A chain report form or directory error.</summary>
    public const string E65 = "E65";

    /// <summary>A chain report error.</summary>
    public const string E66 = "E66";

    /// <summary>A chain assumption is malformed.</summary>
    public const string E67 = "E67";

    /// <summary>A chain target is malformed.</summary>
    public const string E68 = "E68";

    /// <summary>A movement mode is unknown.</summary>
    public const string E69 = "E69";

    /// <summary>A standoff distance is invalid.</summary>
    public const string E70 = "E70";

    /// <summary>No power in a powerset.</summary>
    public const string E71 = "E71";

    /// <summary>An input file cannot be read, or lacks a key.</summary>
    public const string E72 = "E72";

    /// <summary>A build file has the wrong number of power sets.</summary>
    public const string E73 = "E73";

    /// <summary>A build file error.</summary>
    public const string E74 = "E74";

    /// <summary>A build file header level error.</summary>
    public const string E75 = "E75";

    /// <summary>A build file class or inherent-power error.</summary>
    public const string E76 = "E76";

    /// <summary>A build file entry does not resolve.</summary>
    public const string E77 = "E77";

    /// <summary>A build file enhancement error.</summary>
    public const string E78 = "E78";

    /// <summary>An assembly input error.</summary>
    public const string E85 = "E85";

    /// <summary>An assembly input error.</summary>
    public const string E86 = "E86";

    /// <summary>An assembly input error.</summary>
    public const string E88 = "E88";

    /// <summary>An assembly input error.</summary>
    public const string E89 = "E89";

    /// <summary>An assembly input error.</summary>
    public const string E90 = "E90";

    /// <summary>A GrantPower effect error.</summary>
    public const string E91 = "E91";

    /// <summary>An assembly effect error.</summary>
    public const string E92 = "E92";

    /// <summary>An assembly effect error.</summary>
    public const string E93 = "E93";

    /// <summary>An assembly effect error.</summary>
    public const string E94 = "E94";
}
