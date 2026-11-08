using System.Globalization;

namespace CohEngine.Core.CombatLog;

/// <summary>
/// Sorts every combat-log line into one of three outcomes: a taxonomy event, a deliberately ignored line, or unknown.
/// The rules are literal matches on the game's English line shapes. They run in a fixed order and the first match wins:
/// <list type="number">
/// <item>Structure: a line with no timestamp, or with a chat channel tag, is ignored.</item>
/// <item>The ignore list: lines that look like events (the Tactics notice reads like a to-hit roll) or carry nothing the engine can use.</item>
/// <item>Attack rolls, then damage and endurance drains, then heals, then the remaining event shapes.</item>
/// <item>Anything left is <see cref="UnknownEvent"/>, with its text and position intact.</item>
/// </list>
/// </summary>
public sealed class CombatLogClassifier
{
    private static readonly string[] RollJoins = [", you rolled a ", " and rolled a ", ", but rolled a "];

    private static readonly string[] StatusVerbs = ["Taunt", "Immobilize", "Stun", "Hold", "Sleep", "Confuse", "Terrorize", "Placate"];

    private static readonly (Placement Where, string Text, string Reason)[] IgnoreList =
    [
        (Placement.Contains, " increases your team's chance to hit", "team to-hit notice; it names a chance to hit but carries no roll"),
        (Placement.Prefix, "You are no longer seeking a team", "team-search status, not a combat effect"),
        (Placement.Prefix, "Readying ", "power-queue echo"),
        (Placement.Prefix, "Shutting off ", "toggle echo"),
        (Placement.Prefix, "Not enough endurance to use ", "refusal echo; no power fired"),
        (Placement.Suffix, " missed!", "repeats the MISSED roll line that follows it"),
        (Placement.Prefix, "You received ", "loot and rewards; no consumer needs them yet"),
        (Placement.Prefix, "You paid ", "market fee; no consumer needs it yet"),
        (Placement.Prefix, "You reduce your targets damage resistance", "resistance-debuff echo; carries no numbers"),
        (Placement.Prefix, "You knock ", "knockdown echo; the roll line before it carries the data"),
        (Placement.Contains, " knocks you off your feet", "knockdown echo; the roll line before it carries the data"),
        (Placement.Prefix, "You contaminate ", "status echo; carries no numbers"),
        (Placement.Contains, " Fury from ", "Fury gauge change; not a combat result"),
        (Placement.Contains, " points of absorption", "absorption shield; not in the taxonomy until a consumer needs it"),
        (Placement.Prefix, "You're too far away to use ", "refusal echo; no power fired"),
        (Placement.Suffix, " is still recharging.", "refusal echo; no power fired"),
        (Placement.Prefix, "Joined channel ", "chat-system notice"),
        (Placement.Prefix, "Using global chat handle ", "chat-system notice"),
        (Placement.Prefix, "Your friend ", "friend-list notice"),
    ];

    private readonly string? _characterName;

    /// <summary>Initializes a new instance of the <see cref="CombatLogClassifier"/> class.</summary>
    /// <param name="characterName">
    /// The log owner's character name, so self-targeted rolls can be flagged. Null flags none: the log never says whose it is.
    /// </param>
    public CombatLogClassifier(string? characterName = null)
    {
        _characterName = characterName;
    }

    private enum Placement
    {
        Prefix,
        Suffix,
        Contains,
    }

    /// <summary>Classifies every line, in order.</summary>
    /// <param name="lines">The lines from <see cref="CombatLogReader"/>.</param>
    /// <returns>One event per line.</returns>
    public IEnumerable<CombatEvent> ClassifyAll(IEnumerable<CombatLogLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Select(Classify);
    }

    /// <summary>Classifies one line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The event for the line; never null.</returns>
    public CombatEvent Classify(CombatLogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Timestamp is null)
        {
            return new IgnoredEvent(line, "continuation of a multi-line message; it has no timestamp");
        }

        if (line.Channel is not null)
        {
            return new IgnoredEvent(line, "chat message; combat lines carry no channel tag");
        }

        ReadOnlySpan<char> body = line.Text;
        string? origin = SplitOrigin(ref body);
        if (IgnoreReason(body) is { } reason)
        {
            return new IgnoredEvent(line, reason);
        }

        return MatchRoll(line, origin, body, _characterName)
            ?? MatchHit(line, origin, body)
            ?? MatchSelfEndurance(line, body)
            ?? MatchHeal(line, body)
            ?? MatchBuff(line, body)
            ?? MatchDebuff(line, body)
            ?? MatchExpiry(line, body)
            ?? MatchDefeat(line, body)
            ?? MatchExperience(line, body)
            ?? MatchLevelUp(line, body)
            ?? MatchActivation(line, body)
            ?? (CombatEvent)new UnknownEvent(line);
    }

    /// <summary>Pet and effect lines read "Origin:  rest" with two spaces after the colon.</summary>
    private static string? SplitOrigin(ref ReadOnlySpan<char> body)
    {
        if (!Scan.Split(body, ":  ", out ReadOnlySpan<char> origin, out ReadOnlySpan<char> rest))
        {
            return null;
        }

        body = rest;
        return origin.ToString();
    }

    private static string? IgnoreReason(ReadOnlySpan<char> body)
    {
        foreach ((Placement where, string text, string reason) in IgnoreList)
        {
            bool found = where switch
            {
                Placement.Prefix => body.StartsWith(text, StringComparison.Ordinal),
                Placement.Suffix => body.EndsWith(text, StringComparison.Ordinal),
                _ => body.Contains(text, StringComparison.Ordinal),
            };
            if (found)
            {
                return reason;
            }
        }

        return null;
    }

    private static AttackRoll? MatchRoll(CombatLogLine line, string? origin, ReadOnlySpan<char> body, string? characterName)
    {
        ReadOnlySpan<char> counterpart;
        ReadOnlySpan<char> tail;
        CombatDirection direction = CombatDirection.Outgoing;
        bool hit = true;
        if (Scan.After(body, "HIT ", out ReadOnlySpan<char> afterHit) && Scan.Split(afterHit, "! Your ", out counterpart, out tail))
        {
            // outgoing hit
        }
        else if (Scan.After(body, "MISSED ", out ReadOnlySpan<char> afterMissed) && Scan.Split(afterMissed, "!! Your ", out counterpart, out tail))
        {
            hit = false;
        }
        else if (Scan.Split(body, " HITS you! ", out counterpart, out tail))
        {
            direction = CombatDirection.Incoming;
        }
        else if (Scan.Split(body, " MISSES! ", out counterpart, out tail))
        {
            direction = CombatDirection.Incoming;
            hit = false;
        }
        else
        {
            return null;
        }

        if (!Scan.Split(tail, " power ", out ReadOnlySpan<char> power, out ReadOnlySpan<char> outcome))
        {
            return null;
        }

        decimal? chance = null;
        decimal? roll = null;
        bool autohit = outcome is "is autohit." or "was autohit.";
        bool forced = outcome is "was forced to hit by streakbreaker.";
        if (!autohit && !forced && !TryChanceAndRoll(outcome, out chance, out roll))
        {
            return null;
        }

        bool isSelf = characterName is not null && counterpart.Equals(characterName, StringComparison.Ordinal);
        return new AttackRoll(line, direction, counterpart.ToString(), power.ToString(), hit, chance, roll, autohit, forced, isSelf, origin);
    }

    private static bool TryChanceAndRoll(ReadOnlySpan<char> outcome, out decimal? chance, out decimal? roll)
    {
        chance = null;
        roll = null;
        if (!Scan.TrimStart(ref outcome, "had a ")
            || !Scan.Split(outcome, "% chance to hit", out ReadOnlySpan<char> chanceText, out ReadOnlySpan<char> rest)
            || !Scan.Number(chanceText, out decimal chanceValue)
            || !Scan.AfterAny(rest, RollJoins, out ReadOnlySpan<char> rollText)
            || !Scan.TrimEnd(ref rollText, ".")
            || !Scan.Number(rollText, out decimal rollValue))
        {
            return false;
        }

        chance = chanceValue;
        roll = rollValue;
        return true;
    }

    /// <summary>Handles "You hit T with your P for ..." and "S hits you with their P ...": damage and endurance in both directions.</summary>
    private static RecognisedEvent? MatchHit(CombatLogLine line, string? origin, ReadOnlySpan<char> body)
    {
        ReadOnlySpan<char> counterpart;
        ReadOnlySpan<char> rest;
        CombatDirection direction = CombatDirection.Outgoing;
        if (Scan.After(body, "You hit ", out ReadOnlySpan<char> afterHit) && Scan.Split(afterHit, " with your ", out counterpart, out rest))
        {
            // outgoing
        }
        else if (Scan.Split(body, " hits you with their ", out counterpart, out rest))
        {
            direction = CombatDirection.Incoming;
        }
        else
        {
            return null;
        }

        bool incoming = direction == CombatDirection.Incoming;
        if (Scan.Split(rest, incoming ? " granting you " : " granting them ", out ReadOnlySpan<char> giftPower, out ReadOnlySpan<char> gift))
        {
            return Granted(line, incoming ? EnduranceKind.Gained : EnduranceKind.GrantedToTarget, counterpart, giftPower, gift);
        }

        if (!Scan.SplitLast(rest, " for ", out ReadOnlySpan<char> power, out ReadOnlySpan<char> amountAndKind)
            || !Scan.Split(amountAndKind, " points of ", out ReadOnlySpan<char> amountText, out ReadOnlySpan<char> kind)
            || !Scan.Number(amountText, out decimal amount)
            || !Scan.TrimEnd(ref kind, "."))
        {
            return null;
        }

        string? modifier = null;
        if (kind.EndsWith(')') && Scan.SplitLast(kind, " (", out ReadOnlySpan<char> beforeModifier, out ReadOnlySpan<char> inParens))
        {
            modifier = inParens[..^1].ToString();
            kind = beforeModifier;
        }

        bool isDot = Scan.TrimEnd(ref kind, " over time");
        if (direction == CombatDirection.Outgoing && kind is "their endurance")
        {
            return new EnduranceChange(line, EnduranceKind.DrainedFromTarget, counterpart.ToString(), power.ToString(), amount, isDot);
        }

        if (!Scan.TrimEnd(ref kind, " damage"))
        {
            return null;
        }

        string powerName = power.ToString();
        return new DamageHit(
            line, direction, counterpart.ToString(), powerName, amount, kind.ToString(), isDot, powerName.Contains(": ", StringComparison.Ordinal), modifier, origin);
    }

    /// <summary>"S hits you with their P granting you N points of endurance[ over time]." and "You hit T with your P granting them N points ...".</summary>
    private static EnduranceChange? Granted(
        CombatLogLine line, EnduranceKind kind, ReadOnlySpan<char> counterpart, ReadOnlySpan<char> power, ReadOnlySpan<char> gift)
    {
        if (!Scan.TrimEnd(ref gift, "."))
        {
            return null;
        }

        bool isDot = Scan.TrimEnd(ref gift, " over time");
        if (!Scan.TrimEnd(ref gift, " points of endurance") || !Scan.Number(gift, out decimal amount))
        {
            return null;
        }

        return new EnduranceChange(line, kind, counterpart.ToString(), power.ToString(), amount, isDot);
    }

    /// <summary>"Your P grants you N points of endurance." and "Your P drains N points of your endurance."</summary>
    private static EnduranceChange? MatchSelfEndurance(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (!Scan.TrimStart(ref body, "Your "))
        {
            return null;
        }

        if (Scan.Split(body, " grants you ", out ReadOnlySpan<char> gainPower, out ReadOnlySpan<char> gain)
            && Scan.TrimEnd(ref gain, " points of endurance.")
            && Scan.Number(gain, out decimal gained))
        {
            return new EnduranceChange(line, EnduranceKind.Gained, null, gainPower.ToString(), gained, false);
        }

        if (Scan.Split(body, " drains ", out ReadOnlySpan<char> costPower, out ReadOnlySpan<char> cost)
            && Scan.TrimEnd(ref cost, " points of your endurance.")
            && Scan.Number(cost, out decimal spent))
        {
            return new EnduranceChange(line, EnduranceKind.Spent, null, costPower.ToString(), spent, false);
        }

        return null;
    }

    private static Heal? MatchHeal(CombatLogLine line, ReadOnlySpan<char> body)
    {
        ReadOnlySpan<char> rest;
        string? healer = null;
        string? recipient = null;
        if (Scan.After(body, "You are healed by your ", out rest))
        {
            // the log owner healed themself
        }
        else if (Scan.After(body, "You heal ", out ReadOnlySpan<char> afterHeal) && Scan.Split(afterHeal, " with ", out ReadOnlySpan<char> target, out rest))
        {
            recipient = target.ToString();
        }
        else if (Scan.Split(body, " heals you with their ", out ReadOnlySpan<char> source, out rest))
        {
            healer = source.ToString();
        }
        else
        {
            return null;
        }

        if (!Scan.SplitLast(rest, " for ", out ReadOnlySpan<char> power, out ReadOnlySpan<char> amountText)
            || !Scan.TrimEnd(ref amountText, "."))
        {
            return null;
        }

        bool isDot = Scan.TrimEnd(ref amountText, " over time");
        if (!Scan.TrimEnd(ref amountText, " health points") || !Scan.Number(amountText, out decimal amount))
        {
            return null;
        }

        return new Heal(line, healer, recipient, power.ToString(), amount, isDot);
    }

    private static BuffApplied? MatchBuff(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (Scan.TrimStart(ref body, "Your ")
            && Scan.TrimEnd(ref body, ".")
            && Scan.Split(body, " has ", out ReadOnlySpan<char> power, out ReadOnlySpan<char> effect))
        {
            return new BuffApplied(line, power.ToString(), effect.ToString());
        }

        return null;
    }

    private static DebuffApplied? MatchDebuff(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (Scan.TrimStart(ref body, "You ")
            && Scan.TrimEnd(ref body, ".")
            && Scan.Split(body, " ", out ReadOnlySpan<char> verb, out ReadOnlySpan<char> rest)
            && StatusVerbs.Contains(verb.ToString())
            && Scan.Split(rest, " with your ", out ReadOnlySpan<char> target, out ReadOnlySpan<char> power))
        {
            return new DebuffApplied(line, verb.ToString(), target.ToString(), power.ToString());
        }

        return null;
    }

    private static EffectExpired? MatchExpiry(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (Scan.TrimStart(ref body, "You are no longer ") && Scan.TrimEnd(ref body, ".") && !body.IsEmpty)
        {
            return new EffectExpired(line, body.ToString());
        }

        return null;
    }

    private static Defeat? MatchDefeat(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (Scan.TrimStart(ref body, "You have defeated "))
        {
            return new Defeat(line, null, body.ToString());
        }

        if (Scan.Split(body, " has defeated ", out ReadOnlySpan<char> defeater, out ReadOnlySpan<char> victim))
        {
            return new Defeat(line, defeater.ToString(), victim.ToString());
        }

        return null;
    }

    private static ExperienceGained? MatchExperience(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (!Scan.TrimStart(ref body, "You gain ")
            || !Scan.TrimEnd(ref body, ".")
            || !Scan.Split(body, " experience", out ReadOnlySpan<char> experienceText, out ReadOnlySpan<char> rest)
            || !Scan.Whole(experienceText, out long experience))
        {
            return null;
        }

        long debt = 0;
        if (Scan.TrimStart(ref rest, ", work off "))
        {
            if (!Scan.Split(rest, " debt", out ReadOnlySpan<char> debtText, out rest) || !Scan.Whole(debtText, out debt))
            {
                return null;
            }
        }

        if (rest.IsEmpty)
        {
            return new ExperienceGained(line, experience, debt, 0, null);
        }

        if (!(Scan.TrimStart(ref rest, ", and gain ") || Scan.TrimStart(ref rest, " and "))
            || !Scan.Split(rest, " ", out ReadOnlySpan<char> currencyText, out ReadOnlySpan<char> currencyName)
            || !Scan.Whole(currencyText, out long currency))
        {
            return null;
        }

        return new ExperienceGained(line, experience, debt, currency, currencyName.ToString());
    }

    private static LevelUp? MatchLevelUp(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (Scan.TrimStart(ref body, "Your combat improves to level ")
            && Scan.Split(body, "!", out ReadOnlySpan<char> levelText, out ReadOnlySpan<char> rest)
            && rest is " Seek a trainer to further your abilities."
            && int.TryParse(levelText, NumberStyles.None, CultureInfo.InvariantCulture, out int level))
        {
            return new LevelUp(line, level);
        }

        return null;
    }

    private static RecognisedEvent? MatchActivation(CombatLogLine line, ReadOnlySpan<char> body)
    {
        if (Scan.TrimStart(ref body, "You activated the ") && Scan.TrimEnd(ref body, " power.") && !body.IsEmpty)
        {
            return new PowerActivated(line, body.ToString());
        }

        if (Scan.TrimEnd(ref body, " is recharged.") && !body.IsEmpty)
        {
            return new PowerRecharged(line, body.ToString());
        }

        return null;
    }
}
