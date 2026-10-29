"""A full build spec for the writer tests: a Willpower/Super Strength Tanker with Leadership.

Its written ``.mbd`` is ``fixtures/builds/wpss_tanker_leadership.mbd`` and MidsReborn's dump
of that file is ``fixtures/mids/builds/wpss_tanker_leadership/``.
"""

from coh_engine.buildfile.mbd import BuildSpec, PickSpec

C = "Crafted_"
SMT = "Superior_Attuned_Superior_Might_of_the_Tanker_"
SBC = "Superior_Attuned_Blistering_Cold_"
SWB = "Superior_Attuned_Winters_Bite_"


def _set(prefix: str, letters: str) -> tuple[str, ...]:
    return tuple(prefix + letter for letter in letters)


PICKS = (
    PickSpec(
        "Tanker_Defense.Willpower.High_Pain_Tolerance",
        1,
        (*_set(C + "Unbreakable_Guard_", "ABDEF"), C + "Steadfast_Protection_B"),
        True,
    ),
    PickSpec("Tanker_Melee.Super_Strength.Punch", 1, _set(SMT, "ACBDEF"), essential=True),
    PickSpec(
        "Tanker_Defense.Willpower.Mind_Over_Body", 2, _set(C + "Unbreakable_Guard_", "ABDE"), True, essential=True
    ),
    PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, _set(C + "Makos_Bite_", "AEBCDF"), essential=True),
    PickSpec("Tanker_Defense.Willpower.Indomitable_Will", 6, _set(C + "Red_Fortune_", "ABDECF"), True, essential=True),
    PickSpec(
        "Tanker_Defense.Willpower.Rise_to_the_Challenge",
        8,
        _set(C + "Preventive_Medicine_", "ABCDE"),
        True,
        essential=True,
    ),
    PickSpec(
        "Pool.Leaping.Combat_Jumping",
        10,
        (C + "Luck_of_the_Gambler_F", C + "Shield_Wall_F", C + "Luck_of_the_Gambler_A"),
        True,
        essential=True,
    ),
    PickSpec("Pool.Leaping.Long_Jump", 12, (C + "Blessing_of_the_Zephyr_C",)),
    PickSpec("Pool.Fighting.Boxing", 14, (C + "Accuracy",)),
    PickSpec("Pool.Fighting.Tough", 16, _set(C + "Gladiators_Armor_", "ABDECF"), True),
    PickSpec("Tanker_Defense.Willpower.Heightened_Senses", 18, _set(C + "Luck_of_the_Gambler_", "BDEF"), True),
    PickSpec("Tanker_Melee.Super_Strength.Knockout_Blow", 20, _set(SBC, "ACBDEF")),
    PickSpec("Tanker_Defense.Willpower.Fast_Healing", 22, (C + "Numinas_Convalesence_A",), True),
    PickSpec("Tanker_Melee.Super_Strength.Rage", 24, (C + "Recharge",) * 2, True),
    PickSpec(
        "Pool.Leadership.Defense", 26, (C + "Luck_of_the_Gambler_F", C + "Red_Fortune_A", C + "Red_Fortune_E"), True
    ),
    PickSpec("Tanker_Defense.Willpower.Quick_Recovery", 28, (C + "Recovery",) * 2, True),
    PickSpec("Tanker_Melee.Super_Strength.Foot_Stomp", 30, _set(C + "Eradication_", "DEACBF")),
    PickSpec(
        "Tanker_Defense.Willpower.Strength_of_Will", 32, (C + "Unbreakable_Guard_A", C + "Unbreakable_Guard_E"), True
    ),
    PickSpec("Pool.Leadership.Tactics", 35, (C + "Endurance_Discount",), True),
    PickSpec("Epic.Fire_Mastery.Fire_Blast", 38, _set(SWB, "ACBDEF")),
    PickSpec("Epic.Fire_Mastery.Fire_Shield", 41, _set(C + "Aegis_", "ABED"), True),
    PickSpec("Pool.Leadership.Vengeance", 44, (C + "Luck_of_the_Gambler_F",)),
    PickSpec("Tanker_Melee.Super_Strength.Taunt", 47, _set(C + "Mocking_Beratement_", "ABCD")),
    PickSpec("Pool.Speed.Hasten", 49, (C + "Recharge",) * 2, True),
)
# Only the slotted inherents; the writer adds the rest (Brawl, Gauntlet, Rest) as Mids does.
INHERENTS = (
    PickSpec("Inherent.Inherent.Sprint", 1, (C + "Celerity_C",)),
    PickSpec("Inherent.Fitness.Swift", 1, (C + "Run",), True),
    PickSpec("Inherent.Fitness.Health", 1, (C + "Numinas_Convalesence_F", C + "Miracle_F", C + "Panacea_F"), True),
    PickSpec("Inherent.Fitness.Hurdle", 1, (C + "Jump",), True),
    PickSpec("Inherent.Fitness.Stamina", 1, (C + "Performance_Shifter_F", C + "Performance_Shifter_A"), True),
)
SPEC = BuildSpec(
    class_name="Class_Tanker",
    origin="Science",
    alignment="Hero",
    name="WP-SS Tanker - Hard Mode (Leadership)",
    comment="Willpower / Super Strength / Fire Mastery.",
    power_sets=(
        "Tanker_Defense.Willpower",
        "Tanker_Melee.Super_Strength",
        "",
        "Pool.Leaping",
        "Pool.Fighting",
        "Pool.Leadership",
        "Pool.Speed",
        "Epic.Fire_Mastery",
    ),
    picks=PICKS,
    inherents=INHERENTS,
    incarnates=("Incarnate.Alpha.Cardiac_Radial_Paragon",),
)
