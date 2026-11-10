using CohEngine.Core.CombatLog;

namespace CohEngine.Core.Tests.CombatLog;

public sealed class CombatLogClassifierTests
{
    [Fact]
    public void An_outgoing_hit_keeps_the_chance_and_the_roll_at_two_decimals()
    {
        Expect(
            "HIT Big Mob Minion! Your Ball Lightning power had a 93.55% chance to hit, you rolled a 20.04.",
            line => new AttackRoll(line, CombatDirection.Outgoing, "Big Mob Minion", "Ball Lightning", true, 93.55m, 20.04m, false, false, false, null));
    }

    [Fact]
    public void An_outgoing_miss_is_a_miss()
    {
        Expect(
            "MISSED Big Mob Minion!! Your Blazing Aura power had a 95.00% chance to hit, you rolled a 97.64.",
            line => new AttackRoll(line, CombatDirection.Outgoing, "Big Mob Minion", "Blazing Aura", false, 95.00m, 97.64m, false, false, false, null));
    }

    [Fact]
    public void An_incoming_hit_names_the_attacker()
    {
        Expect(
            "Big Mob Minion HITS you! Fire Blast power had a 25.80% chance to hit and rolled a 25.50.",
            line => new AttackRoll(line, CombatDirection.Incoming, "Big Mob Minion", "Fire Blast", true, 25.80m, 25.50m, false, false, false, null));
    }

    [Fact]
    public void An_incoming_miss_names_the_attacker()
    {
        Expect(
            "Big Mob Minion MISSES! Fire Sword power had a 89.99% chance to hit, but rolled a 91.15.",
            line => new AttackRoll(line, CombatDirection.Incoming, "Big Mob Minion", "Fire Sword", false, 89.99m, 91.15m, false, false, false, null));
    }

    [Fact]
    public void An_autohit_carries_no_chance_and_no_roll_and_is_flagged()
    {
        Expect(
            "HIT Big Mob Boss! Your Consume power is autohit.",
            line => new AttackRoll(line, CombatDirection.Outgoing, "Big Mob Boss", "Consume", true, null, null, true, false, false, null));
        Expect(
            "Big Mob Boss HITS you! Fire Blast power was autohit.",
            line => new AttackRoll(line, CombatDirection.Incoming, "Big Mob Boss", "Fire Blast", true, null, null, true, false, false, null));
    }

    [Fact]
    public void A_streakbreaker_hit_is_flagged_so_it_is_not_taken_for_a_roll()
    {
        Expect(
            "HIT Big Mob Boss! Your Atom Smasher power was forced to hit by streakbreaker.",
            line => new AttackRoll(line, CombatDirection.Outgoing, "Big Mob Boss", "Atom Smasher", true, null, null, false, true, false, null));
    }

    [Fact]
    public void A_pet_or_effect_prefix_is_kept_as_the_origin()
    {
        Expect(
            "Irradiated Ground:  MISSED Big Mob Minion!! Your Irradiated Ground power had a 86.07% chance to hit, you rolled a 91.16.",
            line => new AttackRoll(line, CombatDirection.Outgoing, "Big Mob Minion", "Irradiated Ground", false, 86.07m, 91.16m, false, false, false, "Irradiated Ground"));
    }

    [Fact]
    public void A_self_targeted_autohit_is_flagged_only_when_the_character_name_is_known()
    {
        const string Outgoing = "HIT Player! Your Hasten power is autohit.";
        const string Incoming = "Player HITS you! Hasten power was autohit.";

        Expect(
            Outgoing,
            line => new AttackRoll(line, CombatDirection.Outgoing, "Player", "Hasten", true, null, null, true, false, true, null),
            "Player");
        Expect(
            Incoming,
            line => new AttackRoll(line, CombatDirection.Incoming, "Player", "Hasten", true, null, null, true, false, true, null),
            "Player");
        Assert.False(Assert.IsType<AttackRoll>(Classify(Outgoing)).IsSelf);
        Assert.False(Assert.IsType<AttackRoll>(Classify(Outgoing, "Someone Else")).IsSelf);
    }

    [Fact]
    public void Outgoing_damage_keeps_two_decimals_and_the_type()
    {
        Expect(
            "You hit Big Mob Minion with your Blazing Aura for 322.87 points of Fire damage.",
            line => new DamageHit(line, CombatDirection.Outgoing, "Big Mob Minion", "Blazing Aura", 322.87m, "Fire", false, false, null, null));
    }

    [Fact]
    public void Over_time_damage_is_flagged_as_a_dot()
    {
        Expect(
            "You hit Big Mob Minion with your Degenerative Interface for 5.13 points of Toxic damage over time.",
            line => new DamageHit(line, CombatDirection.Outgoing, "Big Mob Minion", "Degenerative Interface", 5.13m, "Toxic", true, false, null, null));
    }

    [Fact]
    public void A_set_piece_proc_is_flagged_from_its_set_and_piece_name()
    {
        Expect(
            "You hit Big Mob Minion with your Scirocco's Dervish: Chance for Lethal Damage for 322.87 points of Lethal damage.",
            line => new DamageHit(
                line, CombatDirection.Outgoing, "Big Mob Minion", "Scirocco's Dervish: Chance for Lethal Damage", 322.87m, "Lethal", false, true, null, null));
    }

    [Fact]
    public void A_critical_keeps_its_modifier_word()
    {
        Expect(
            "Ion Core Final Judgement:  You hit Big Mob Boss with your Ion Core Final Judgement for 111.79 points of Energy damage (JUDGEMENT CRITICAL).",
            line => new DamageHit(
                line, CombatDirection.Outgoing, "Big Mob Boss", "Ion Core Final Judgement", 111.79m, "Energy", false, false, "JUDGEMENT CRITICAL", "Ion Core Final Judgement"));
    }

    [Fact]
    public void Incoming_damage_names_the_source_and_may_be_a_dot()
    {
        Expect(
            "Big Mob Minion hits you with their Fire Blast for 12.48 points of Negative Energy damage over time.",
            line => new DamageHit(line, CombatDirection.Incoming, "Big Mob Minion", "Fire Blast", 12.48m, "Negative Energy", true, false, null, null));
    }

    [Fact]
    public void Endurance_the_log_owner_gains_from_a_power_is_gained()
    {
        Expect(
            "Your Consume grants you 15 points of endurance.",
            line => new EnduranceChange(line, EnduranceKind.Gained, null, "Consume", 15m, false));
    }

    [Fact]
    public void Endurance_another_player_gives_the_log_owner_is_gained_with_the_giver_named()
    {
        Expect(
            "Ally hits you with their Performance Shifter: Chance for +Endurance granting you 10.14 points of endurance.",
            line => new EnduranceChange(line, EnduranceKind.Gained, "Ally", "Performance Shifter: Chance for +Endurance", 10.14m, false));
        Expect(
            "Ally hits you with their Empowering Burst granting you 2.5 points of endurance over time.",
            line => new EnduranceChange(line, EnduranceKind.Gained, "Ally", "Empowering Burst", 2.5m, true));
    }

    [Fact]
    public void A_power_that_costs_the_log_owner_endurance_is_spent()
    {
        Expect(
            "Your Hasten drains 14.99 points of your endurance.",
            line => new EnduranceChange(line, EnduranceKind.Spent, null, "Hasten", 14.99m, false));
    }

    [Fact]
    public void Endurance_drained_from_a_foe_names_the_foe_and_may_be_over_time()
    {
        Expect(
            "You hit Big Mob Boss with your Ball Lightning for 20.14 points of their endurance.",
            line => new EnduranceChange(line, EnduranceKind.DrainedFromTarget, "Big Mob Boss", "Ball Lightning", 20.14m, false));
        Expect(
            "You hit Big Mob Boss with your Electrifying Fences for 3.83 points of their endurance over time.",
            line => new EnduranceChange(line, EnduranceKind.DrainedFromTarget, "Big Mob Boss", "Electrifying Fences", 3.83m, true));
    }

    [Fact]
    public void Endurance_the_log_owner_gives_to_someone_else_is_granted_to_the_target()
    {
        Expect(
            "You hit Ally with your Panacea: Chance for +Hit Points/Endurance granting them 5.4 points of endurance.",
            line => new EnduranceChange(line, EnduranceKind.GrantedToTarget, "Ally", "Panacea: Chance for +Hit Points/Endurance", 5.4m, false));
    }

    [Fact]
    public void A_self_heal_has_no_healer_and_no_recipient()
    {
        Expect(
            "You are healed by your Power Transfer: Chance to Heal Self for 80.32 health points.",
            line => new Heal(line, null, null, "Power Transfer: Chance to Heal Self", 80.32m, false));
    }

    [Fact]
    public void A_heal_the_log_owner_casts_names_the_recipient()
    {
        Expect(
            "You heal Ally with Panacea: Chance for +Hit Points/Endurance for 64.58 health points.",
            line => new Heal(line, null, "Ally", "Panacea: Chance for +Hit Points/Endurance", 64.58m, false));
    }

    [Fact]
    public void A_heal_from_another_player_names_the_healer_and_may_be_over_time()
    {
        Expect(
            "Ally heals you with their Regrowth for 25.19 health points over time.",
            line => new Heal(line, "Ally", null, "Regrowth", 25.19m, true));
    }

    [Fact]
    public void A_buff_keeps_the_power_and_the_effect_text()
    {
        Expect(
            "Your Hasten has increased your rate of attack.",
            line => new BuffApplied(line, "Hasten", "increased your rate of attack"));
    }

    [Theory]
    [InlineData("Taunt")]
    [InlineData("Immobilize")]
    [InlineData("Stun")]
    [InlineData("Hold")]
    [InlineData("Sleep")]
    [InlineData("Confuse")]
    [InlineData("Terrorize")]
    [InlineData("Placate")]
    public void A_status_the_log_owner_puts_on_a_foe_is_a_debuff(string verb)
    {
        Expect(
            $"You {verb} Big Mob Boss with your Superior Frozen Blast: Recharge/Chance for Immobilize.",
            line => new DebuffApplied(line, verb, "Big Mob Boss", "Superior Frozen Blast: Recharge/Chance for Immobilize"));
    }

    [Fact]
    public void A_status_ending_on_the_log_owner_is_an_expiry()
    {
        Expect("You are no longer held.", line => new EffectExpired(line, "held"));
    }

    [Fact]
    public void Seeking_a_team_ending_is_not_a_combat_expiry()
    {
        Assert.IsType<IgnoredEvent>(Classify("You are no longer seeking a team."));
    }

    [Fact]
    public void A_defeat_names_the_defeater_or_is_the_log_owners_own()
    {
        Expect("You have defeated Big Mob Minion", line => new Defeat(line, null, "Big Mob Minion"));
        Expect("Ally Two has defeated Big Mob Boss", line => new Defeat(line, "Ally Two", "Big Mob Boss"));
    }

    [Fact]
    public void Experience_alone_has_no_debt_and_no_currency()
    {
        Expect("You gain 1,905 experience.", line => new ExperienceGained(line, 1905, 0, 0, null));
    }

    [Fact]
    public void Experience_with_influence_carries_both_with_thousands_separators_read()
    {
        Expect("You gain 8,572 experience and 16,002 influence.", line => new ExperienceGained(line, 8572, 0, 16002, "influence"));
        Expect("You gain 665 experience and 465 infamy.", line => new ExperienceGained(line, 665, 0, 465, "infamy"));
    }

    [Fact]
    public void Experience_that_pays_off_debt_carries_the_debt_and_the_currency()
    {
        Expect(
            "You gain 1,074 experience, work off 1,075 debt, and gain 5,157 influence.",
            line => new ExperienceGained(line, 1074, 1075, 5157, "influence"));
        Expect("You gain 304 experience, work off 304 debt.", line => new ExperienceGained(line, 304, 304, 0, null));
    }

    [Fact]
    public void A_level_up_carries_the_new_level()
    {
        Expect(
            "Your combat improves to level 50! Seek a trainer to further your abilities.",
            line => new LevelUp(line, 50));
    }

    [Fact]
    public void A_power_activation_and_its_recharge_carry_the_power()
    {
        Expect("You activated the Burn power.", line => new PowerActivated(line, "Burn"));
        Expect("Hasten is recharged.", line => new PowerRecharged(line, "Hasten"));
    }

    [Theory]
    [InlineData("Your Tactics increases your team's chance to hit and Perception.")]
    [InlineData("Ally increases your team's chance to hit and Perception with good Tactics.")]
    public void The_tactics_notice_is_ignored_and_never_read_as_a_to_hit_roll(string text)
    {
        IgnoredEvent ignored = Assert.IsType<IgnoredEvent>(Classify(text));

        Assert.Equal(CombatOutcome.Ignored, ignored.Outcome);
        Assert.Contains("no roll", ignored.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Readying Temp Invulnerability.")]
    [InlineData("Shutting off Tactics.")]
    [InlineData("Not enough endurance to use Tactics.")]
    [InlineData("Soul Drain missed!")]
    [InlineData("You received Insight.")]
    [InlineData("You paid 5,000 to the Consignment House.")]
    [InlineData("You reduce your targets damage resistance temporarily")]
    [InlineData("Irradiated Ground:  You reduce your targets damage resistance temporarily")]
    [InlineData("You knock Big Mob Minion off their feet with your Superior Avalanche: Recharge/Chance for Knockdown!")]
    [InlineData("Big Mob Minion knocks you off your feet with their Cremate!")]
    [InlineData("You contaminate Big Mob Minion!")]
    [InlineData("You gain 12 Fury from Brute's Fury: Recharge/Fury Bonus!")]
    [InlineData("Ally grants you 9.5 points of absorption over time with their Lightfield.")]
    [InlineData("You're too far away to use Pulverize.")]
    [InlineData("Shatter is still recharging.")]
    [InlineData("Joined channel 'Example'")]
    [InlineData("Using global chat handle @Example")]
    [InlineData("Your friend Ally is now online.")]
    public void A_known_echo_is_ignored_with_a_reason(string text)
    {
        IgnoredEvent ignored = Assert.IsType<IgnoredEvent>(Classify(text));

        Assert.False(string.IsNullOrWhiteSpace(ignored.Reason));
    }

    [Fact]
    public void A_chat_line_is_ignored_even_when_its_text_looks_like_combat()
    {
        IgnoredEvent ignored = Assert.IsType<IgnoredEvent>(Classify("[General] Ally: HIT Big Mob! Your Burn power is autohit."));

        Assert.Contains("chat", ignored.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_with_no_timestamp_is_ignored_as_a_continuation()
    {
        using StringReader reader = new("second half of a message");
        CombatLogLine continuation = Assert.Single(CombatLogReader.Read(reader));

        IgnoredEvent ignored = Assert.IsType<IgnoredEvent>(new CombatLogClassifier().Classify(continuation));

        Assert.Contains("continuation", ignored.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_nobody_recognises_is_unknown_with_its_text_and_position_intact()
    {
        using StringReader reader = new("2024-04-24 00:55:01 Your Energy Drain increases your defense!");
        CombatLogLine line = Assert.Single(CombatLogReader.Read(reader));

        UnknownEvent unknown = Assert.IsType<UnknownEvent>(new CombatLogClassifier().Classify(line));

        Assert.Equal(CombatOutcome.Unknown, unknown.Outcome);
        Assert.Same(line, unknown.Line);
        Assert.Equal(1, unknown.Line.Number);
        Assert.Equal("Your Energy Drain increases your defense!", unknown.Line.Text);
        Assert.Equal("2024-04-24 00:55:01 Your Energy Drain increases your defense!", unknown.Line.Raw);
    }

    [Theory]
    [InlineData("HIT Big Mob! Your Burn power")]
    [InlineData("HIT Big Mob! Your Burn power does things.")]
    [InlineData("HIT Big Mob! Your Burn power had a lot of luck.")]
    [InlineData("HIT Big Mob! Your Burn power had a x% chance to hit, you rolled a 5.")]
    [InlineData("HIT Big Mob! Your Burn power had a 50% chance to hit, whatever 5.")]
    [InlineData("HIT Big Mob! Your Burn power had a 50% chance to hit, you rolled a x.")]
    [InlineData("You hit Big Mob with your Burn.")]
    [InlineData("You hit Big Mob with your Burn for lots.")]
    [InlineData("You hit Big Mob with your Burn for x points of Fire damage.")]
    [InlineData("You hit Big Mob with your Burn for 5 points of Fire damage")]
    [InlineData("You hit Big Mob with your Burn for 5 points of Fire.")]
    [InlineData("You hit Big Mob with your Burn for 5 points of Fire damage).")]
    [InlineData("Big Mob hits you with their Burn for 5 points of their endurance.")]
    [InlineData("Big Mob hits you with their Burn granting you x points of endurance.")]
    [InlineData("Big Mob hits you with their Burn granting you 5 points of endurance")]
    [InlineData("Big Mob hits you with their Burn granting you 5 points of luck.")]
    [InlineData("Your Burn grants you x points of endurance.")]
    [InlineData("Your Burn drains x points of your endurance.")]
    [InlineData("Your Burn rests.")]
    [InlineData("You are healed by your Burn.")]
    [InlineData("You are healed by your Burn for 5 health.")]
    [InlineData("You are healed by your Burn for 5 hearts.")]
    [InlineData("You are healed by your Burn for x health points.")]
    [InlineData("You heal Ally.")]
    [InlineData("Your Burn glows")]
    [InlineData("You Wave at Big Mob with your Hand.")]
    [InlineData("You Taunt Big Mob.")]
    [InlineData("You are no longer .")]
    [InlineData("You gain lots.")]
    [InlineData("You gain x experience.")]
    [InlineData("You gain 5 experience, work off x debt.")]
    [InlineData("You gain 5 experience, work off 5.")]
    [InlineData("You gain 5 experience and lots.")]
    [InlineData("You gain 5 experience and x influence.")]
    [InlineData("You gain 5 experience for free.")]
    [InlineData("Your combat improves to level x! Seek a trainer to further your abilities.")]
    [InlineData("Your combat improves to level 5! Go away.")]
    [InlineData("Your combat improves to level 5")]
    [InlineData("You activated the  power.")]
    [InlineData(" is recharged.")]
    public void A_near_miss_of_a_known_shape_is_unknown_rather_than_half_read(string text)
    {
        Assert.IsType<UnknownEvent>(Classify(text));
    }

    [Fact]
    public void Classifying_a_whole_file_yields_one_event_per_line_in_order()
    {
        using StringReader reader = new("2024-04-24 00:00:01 Hasten is recharged.\n2024-04-24 00:00:02 ???\n");
        CombatLogLine[] lines = [.. CombatLogReader.Read(reader)];

        CombatEvent[] events = [.. new CombatLogClassifier().ClassifyAll(lines)];

        Assert.Equal([CombatOutcome.Recognised, CombatOutcome.Unknown], events.Select(combatEvent => combatEvent.Outcome));
        Assert.Equal(lines, events.Select(combatEvent => combatEvent.Line));
    }

    [Fact]
    public void A_null_argument_is_refused()
    {
        CombatLogClassifier classifier = new();

        Assert.Throws<ArgumentNullException>(() => classifier.Classify(null!));
        Assert.Throws<ArgumentNullException>(() => classifier.ClassifyAll(null!));
    }

    private static CombatLogLine Line(string text)
    {
        using StringReader reader = new("2024-04-24 00:55:01 " + text);
        return Assert.Single(CombatLogReader.Read(reader));
    }

    private static CombatEvent Classify(string text, string? characterName = null)
    {
        return new CombatLogClassifier(characterName).Classify(Line(text));
    }

    private static void Expect(string text, Func<CombatLogLine, CombatEvent> expected, string? characterName = null)
    {
        CombatLogLine line = Line(text);

        CombatEvent actual = new CombatLogClassifier(characterName).Classify(line);

        Assert.Equal(expected(line), actual);
        Assert.Equal(CombatOutcome.Recognised, actual.Outcome);
    }
}
