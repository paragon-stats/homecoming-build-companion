---
title: "The Survivability Tool"
url: "https://forums.homecomingservers.com/topic/12212-the-survivability-tool/"
source: "forums.homecomingservers.com"
authors: ["Bopper"]
date_posted: "2019-11-03"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "set-bonuses-io", "recharge-endurance", "damage-dps", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "cbc66046cccaf160"
---

# The Survivability Tool

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Bopper, 2019-11-03

**The Survivability Tool**

by Bopper

Written: 2 November 2019

Last Updated: 17 June 2020

(How long will it take until you're dead?)

Author's Note:

Spoiler

This tool is always a work in progress. I am always updating the tool when new information comes in (like how I learned about Blasters having a higher max HP than what's listed on paragon wiki). There is also a giant exclusion from this tool that needs to be incorporated: the Absorb mechanic. I don't entirely understand how it works and I don't entirely understand how I can factor it into the survivability measurements. The temporary duration of an absorb shield is an issue, but once I better grasp it I will incorporate it.

Have you ever wondered how strong am I, really? Well, I have a simple test for that. I have written a program that allows the user to enter characteristics of their hero/villain; such as Archetype, Defenses, Resistances, Max Health Bonuses, and Regeneration. You can then input an amount of damage per second you are attacked with (you select the type of damage, specifically) and the tool will calculate how long it would take for you to go from 100% health to death. I call this the "Time until Death" calculation: which is one of four Survivability Calculations I showcase in this tool. The others: Max DPS for Survival Duation, Survivability Score, and Immortality Score will all be described in detail below.

Now, you might be asking, why would I want to know this? Answer, you might not; but if you have ever asked the question, “should I add more defense or resistance (or more HP, or more regeneration)?” then this is the tool for you. In real time, you can tweak any parameter and see how it will impact your survivability.

Without further ado, here is a link to the** Survivability Tool** (It is a Google Doc that you will make a local copy of).

**How do I use the tool?**

First off, you can only edit the fields marked with a Green background. Also, there are two types of editable fields: ones you enter a value (e.g. changing Max HP Boost from 50% to 15%), and ones you select from a drop-down menu (e.g. changing Archetype from Brute to Defender).

The fields marked with an Orange background are calculated fields, which takes the user-input information to determine their values. You can’t edit these fields, otherwise you would break the formulas in the field.

The fields marked with a Black background are fixed tables, which are used for look-up purposes. Again, you can’t edit these fields.

Now that we know the rules, let’s step through each field:

**User Inputs**

**Select Your Archetype**

Archetype:  The user can select their archetype using the drop-down menu. Based on the selection, the appropriate archetype information is loaded into the “Max Values for your AT” field (HP base, HP max, Regeneration max, and Resistance max). Those values are used as limits checks for calculations later on.

**Enter Your Health Bonuses**

Max HP Boost (%):  The user enters the amount of Max HP boosts in the build. This could include boosts from Set IO Bonuses, or it could be a power that increases the Max HP. Ultimately, add up all the bonuses and **do not include** the 100% from base. Max HP Boosts do 2 things for the character: more Max HP means more damage needs to be done to kill you, also more Max HP means more HP is recovered every time a regeneration tick is triggered (5% of max HP is recovered on each tick).

Regeneration Total (%):  The user enters the total amount of Regeneration in the build (100% from base, regeneration from set IO bonuses and powers). Add up all of them (or use Mid’s Reborn and look at the totals…**do include** the 100% base). Regeneration determines how frequently the 5% HP recovery triggers.

Additional Heal over Time (HPS):  The user enters the average amount of additional Healing the character receives in units of Hit Points per Second. For example, if every 90 seconds the character Heals them self for 450 HP, then the user will input 5 (450 HP/90s = 5 HPS).

**Enter Your Debuffs**

Damage Debuff:  The user enters the amount of damage debuff their character applies to the enemy. In the game the enemy can resist this debuff, so it is important to know that the user must enter the post-resisted debuff value into this field. For example, if the character applies a 50% damage debuff and the enemy resists 25% of the debuff, the user must input 37.5% into this field:

To Hit Debuff:  The user enters the amount of to-hit debuff their character applies to the enemy. In the game the enemy can resist this debuff, so the user must enter the post-resisted debuff value into this field. For example, if the character applies a 30% to-hit debuff and the enemy resists 25% of the debuff, the user must input 22.5% into this field:

**Enter Your Enemy’s Attack Info**

Attack Damage Per Second:  The user enters the amount of DPS is coming from an enemy’s attack (remember, this is a value that is prior to looking at defense, resistance, etc. Just the raw damage).

Survival Duration:  The user enters the duration (in seconds) that they want to be capable of surviving. This value is used later for the "Max DPS for Survival Duration" and "Survivability Score" calculations.

Attack Type: The user selects the type of attack being used for the Survivability Calculation.

Damage Type: The user selects the type of damage being used for the Survivability Calculation.

Enemy To Hit:  The user enters the To-Hit Modifier. By default, this value is 50% (63.75% if you want to use Incarnate Trials’ To-Hit mods). The user can make this any value they want though, so if they want to buff it (or debuff it), tweak this value appropriately.

Enemy Accuracy Modifier:  The user enters the Accuracy Modifier. By default, this value is 1.0x, but the user has the freedom to apply an accuracy bonus if desired.

Enemy Type:  The user can select the rank of the enemy, which will introduce additional accuracy modifiers for the calculations.

Enemy Relative Level: The user can select the relative level of the enemy which could introduce additional To-Hit modifiers and accuracy modifiers.

**Enter Your Defense/Resistance Values**

Defense: The user enters the defense for each position and type. Note that there is no such thing as toxic defense (at least according to Mids) so keep that set to 0%.

Resistance: The user enters the resistance for each damage type. Note that there is no such thing as positional resistances so keep those set to 0%.

**Outputs**

**Max Values for your AT**

HP (base): This is the base Hit Points for the user-selected Archetype.

HP (max): This is the maximum Hit Points for the user-selected Archetype.

Regeneration (max):  This is the maximum regeneration for the user-selected Archetype.

Resistance (max):  This is the maximum resistance for the user-selected Archetype.

**Your Health Calculations**

Resistance:  This is the resistance value to the user-selected damage type. This value is used in calculating the amount of damage taken for each successful attack.

Defense:  This is the defense value to the user-selected attack and damage type. This value is always the highest between the user-selected options.

Max Health:  This is the maximum HP for the character after factoring in the Archetype, Max HP Boost, and HP (max).

Regen Period:  This is how frequently 5% of max HP is returned to the character. When there is 0% Regeneration Bonus, this value is 12 seconds. As regeneration boosts are increased, the interval period reduces:

HP per Regen Tick:  This is simply 5% of the Max Health you recover on each regeneration tick.

HP/sec:  This is the average amount of HP recovery each second. Simply, this is:

Using some algebra, we can simplify this formula to be:

**Your Damage Taken Calculations**

Damage Taken (after Resistance):  This is the damage you take when an attack hits you. It is reduced by the amount of your resistance and the amount of damage debuffs.

Total Accuracy Mods (product):  This is the combined Accuracy Modifier used by the enemy. It is the product of the enemy’s general accuracy mod, rank accuracy mod, and relative level accuracy mod.

Total ToHit Mods (sum):  This is the combined To-Hit Modifier used by the enemy. It is the summation of the enemy’s general to-hit mod, relative level to-hit mod, and to-hit debuffs.

Probability to Hit:  This is the final probability of an attack hitting you, combining the effects of all accuracy mods, to-hit mods, and defense.

Note, this is an oversimplification of the formula. The to-hit mod portion and final result are actually clamped to where the result can go no less than 5% and no more than 95%. This tweaks the formula to be:

Average Damage Taken Per Second:  This is the expected amount of damage per second your character will take, which is the product of the Damage Taken (after Resistance) and the Probability to Hit.

**Your Survivability Calculation**

Each of the Survivability Calculations will display their scores for every permutation of Positional and Type attacks. This was a quality-of-life feature added in v2.0 to quickly allow users to view all of their scores instead of having to manually change the Attack and Damage type for every permutation. The table will look like this:

For the table we can see for this build, when facing a Smashing-Melee attack, it has an Immortality Score of 193.33 (same score for Ranged, AoE, and No Position which implies the Smashing Defense is higher than all of the positional Defenses). When facing Psionic-Range attacks, it has an Immortality Score of 54.39. The No Position (or "positionless") attack is rare, but there are a few enemies who do Psionic Attacks that are not tagged with a position, in which case the positional defenses are ignored and only the type defense is used in the analysis.

Time until Death:  This is the amount of time it takes to go from 100% health to death. This calculation uses the inputs from the user and incorporates them into the following formula:

Max DPS for Survival Duration:  This is the maximum amount of Attack Damage per Second you are able to take in order to survive for the user-defined Survival Duration.

Survivability Score:  This is the survivability factor above your Archetype's base values. This simply calculates the ratio of your "Max DPS for Survival Duration" rating versus what that rating would be if all of your attributes were set to their base values (e.g. no defense, no resistance, no HP boost, no additional regeneration). This score is not intended to compare across other Archetypes as other ATs will have different Base HP

Immortality Score:  This is perhaps the definitive metric one can use to measure how well they can survive. It simply calculates how much the DPS of the enemy needs to be for it to overtake your build's HP/sec. Anything more, and your health bar will trend downward over time; thus this is the maximum amount of DPS your character can withstand while still surviving forever.

**Edited June 18, 2020 by Bopper**

Long overdue update to the Guide. It now properly reflects the features of the Tool
