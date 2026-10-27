---
title: "How to Calculate Debuffs"
url: "https://forums.homecomingservers.com/topic/21251-how-to-calculate-debuffs/"
source: "forums.homecomingservers.com"
authors: ["Bopper"]
date_posted: "2020-07-25"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "set-bonuses-io", "defense-resist", "recharge-endurance", "damage-dps", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "53be20053cc5b073"
---

# How to Calculate Debuffs

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Bopper, 2020-07-25

How to Calculate Debuffs

When factoring in Strength, Resistance Debuffs, and Enemy Debuff Resistance

By Bopper

25 July 2020

**Author’s Note**

The motivation for this guide comes from a tool I developed from knowledge of the mechanics presented by Trickshooter and Frosticus. Their work helped me better understand how resistance debuffs interact with debuffs (Damage debuffs interact with Damage Resistance debuffs, To-Hit debuffs interact with To-Hit Resistance debuffs, and so on) and I wanted to pass along that information with examples and highlight how you can use the tool to do your own calculations.

Some quick things with the format of this guide. First, I try to use wording that we see in the game, so I use the term "unresistable" whenever referring to an effect that ignores resistances. Also, there is a lot of verbiage on debuff resistances and resistance debuffs. That will come off as confusing, but I try to use consistent language when using the terms.

-
A **Resistance Debuff** refers to when you debuff an enemy's resistance to debuffs. This effect is usually denoted in game as -Res(TypeDebuff) and it essentially acts as a way to increase the effects of that type of debuff applied on that target (similar to how debuffing damage resistance increases everyone's damage, debuffing a debuff resistance will increases everyone's debuff of that type).

-
A **Debuff Resistance** refers to the amount of resistance an enemy has to debuffs. The equivalent to this is your character's debuff resistances such as defense debuff resistance (DDR) or recovery resistance.

Hopefully that clears it up, but if it is still too technical, just remember Resistance Debuff is good, Debuff Resistance is bad.

**How Debuffs are impacted by Resistance Debuffs:**

Trickshooter goes into excellent detail on the steps of these mechanics, but I’ll summarize the best I can. When a Debuff is applied, it has a base amount (e.g. Defender’s Radiation Infection does -31.25% To-Hit debuff and -31.25% Defense debuff). That debuff can be increased in strength from enhancements and/or strength buffing effects (like Power Boost). This serves as an effect multiplier, for instance let’s say you slot 20% enhancement to the To-Hit Debuff and you get 98.34% strength to To-Hit Debuff from Power Boost, the new To-Hit debuff would become -31.25% x (100% + 20% + 98.34%) = 68.23%.

However, Strength is not the only way you can improve your debuffs. You can also apply a resistance debuff to the target. This may show in the form of something like -40% Res(ToHit). This would apply a 40% resistance debuff to the target’s to-hit debuff resistance. Is that a mouthful? That’s because this is the same “resistance resists resistible resistance debuffs” situation you typically see discussed when talking about damage resistance debuffs (which I’ll get to shortly).

Let's now assume you face a Level 50 AV, which typically will have 85% resistance to all debuffs. So applying a 40% resistance debuff to their To-Hit will cause their new resistance to become:

New Debuff Resistance = 85% - 40% x (100% - 85%) = 79%.

Now that we have the final strength of our debuff and the final resistance of the target’s resistance to that debuff, we can calculate the final debuff applied to the target. Using the numbers in our example, we have 68.23% To-Hit debuff which the target resists 79% of, leaving the final debuff to be:

Final Debuff = 68.23% x (100% - 79%) = 14.33% To-Hit debuff.

There is a simple formula we can use to calculate these effects:

Final Debuff = (Base Debuff) x (1 + AddedStrength) x (1 + Res[Debuff]) x (1 – DebuffResistance)

In our example, that becomes:

Final Debuff = 31.25% x (1 + 0.20 + 0.9834) x (1 + 0.40) x (1 – 0.85) = 14.33%

There are other factors to consider, such as purple patch. This will impact the base debuff and it will impact the Resistance Debuff (Res[Debuff]) that is applied. So if you were facing a level 50 AV and you were only level 47, the +3 con of the enemy would make your effects have only 65% strength. This changes the results of the debuffs as so:

Final Debuff = (Base Debuff) x (Purple Patch Modifier) x (1 + AddedStrength) x (1 + (Purple Patch Modifier) x Res[Debuff]) x (1 – DebuffResistance).

In our example that would result in:

Final Debuff = 31.25% x 0.65 x (1 + 0.2 + 0.9834) x (1 + 0.65 x 0.4) x (1 – 0.85) = 8.38%

**What about Damage Debuffs?**

Damage Debuffs work in conjunction with the Damage Resistance of the target. Frosticus went into extensive detail on how this interaction works in their Poison Guide, but the big take away is Damage Resistance Debuffs will improve your Damage Debuffs, and because damage resistance varies by damage type, those damage debuff effects will also vary by damage type. This doesn’t change our formula from before, just need to factor in the damage type. The main difference is damage debuffs always ignore strength so you won’t have strength improvements from enhancements nor outside buffs:

Final Damage Type Debuff = (Base Damage Type Debuff) x (Purple Patch Mod) x (1 + (Purple Patch Mod) x ResistanceDebuff) x (1 -  DamageTypeResistance).

For an example, let’s use Defender’s Enervating Field which provides a 25% damage debuff and a 30% resistance debuff and we will assume the target is a Level 50 AV while we are Level 47. Let’s assume the AV has 40% resistance to Smashing Damage and 0% resistance to Energy Damage.

Final Smashing Damage Debuff = 25% x 0.65 x (1 + 0.65 x 0.30) x (1 – 0.40) = 11.65%

Final Energy Damage Debuff = 25% x 0.65 x (1 + 0.65 x 0.30) x (1 – 0) = 19.42%

This means we reduced the damage of Smashing attacks by 11.65% and we reduced the damage of Energy attacks by 19.42%. Ideally, you debuff an enemy that has low resistance to the type of damage they attack with. Next, let's compare these results to a 25% Damage Debuff with no Resistance Debuff and we can see the improvement made by the Resistance Debuffs:

Final Smashing Damage Debuff = 25% x 0.65 x (1 + 0) x (1 – 0.40) = 9.75%

Final Energy Damage Debuff = 25% x 0.65 x (1 + 0) x (1 – 0) = 15%

**How to Apply this Formula for Calculating Damage**

Damage can be treated as its own debuff, as it debuffs their health. Ok, I might be stretching with that comparison, but the nice thing is the formula doesn’t have to change:

Final Type Damage = (Base Type Damage) x (1 + AddedStrength) x (Purple Patch Mod) x (1 + (Purple Patch Mod) x ResistanceDebuff) x (1 – DamageTypeResistance)

Let’s assume we’re doing an attack that does 200 Smashing Damage and we have +95% enhanced damage and +30% global damage buff. We apply a 20% resistance debuff to an enemy that is +1 (0.90 purple patch) and they have 25% resistance to smashing damage.

Final Smashing Damage = 200 x (1 + 0.95 + 0.30) x 0.90 x (1 + 0.90 x 0.20) x (1 – 0.25) = 358.425 smashing damage.

**How to incorporate Unresistable Debuffs and Unresistable Resistance Debuffs**

So far, all discussion has only looked at resistible debuffs and resistible resistance debuffs. There are effects that can be unresistable which can include debuffs and resistance debuffs. This does not change the math at all, just expands the formula to breakout the resistible and unresistable portions.

Final Resistance Debuff = (Purple Patch Mod) x ( UnRes[Debuff] + Res[Debuff]x (1 - DebuffResistance) )

FinalDebuffResistance = DebuffResistance - (Final Resistance Debuff)

Resistible Debuff = (Base Resistible Debuff) x (1 + AddedStrength1)     // I am showing AddedStrength1 and AddedStrength2 to highlight the debuffs could be from different powers and thus have different enhancements.

Unresistable Debuff = (Base Unresistable Debuff) x (1 + AddedStrength2)

Final Debuff = ( (Unresistable Debuff) + (Resistible Debuff) x ( 1 – (FinalDebuffResistance) ) ) x (Purple Patch Mod)

This certainly looks messier, but trying to fit it all into one formula might be a bit much to take in. So I hope breaking it out like this helps make sense of the steps used to calculate the Final Debuff amount. Luckily, unresistable debuffs and unresistable resistance debuffs are rare and hopefully you won't need to calculate them too often.

To help bring this home, let's assume a hypothetical power. Let's say we have a power that does 20% base to-hit debuff, however this power is unique in the fact half of that is unresistable. This would mean 10% can be resisted and the other 10% is not resisted. Let's assume the power is enhanced by 50%. The target we are debuffing has 85% resistance to to-hit debuffs and is +2 level con (80% strength purple patch). Finally, let's assume another power is used that provides a 40% resistance debuff to to-hit on the target (not enhanced, resistible). What is the final to-hit debuff on the target?

Final Resistance Debuff = 0.80 x (0 + 0.40 x (1 - 0.85) ) = 4.8%

FinalDebuffResistance = 85% - 4.8% = 80.2%

Resistible Debuff = 10% x (1 + 0.50) = 15%

Unresistable Debuff = 10% x (1 + 0.50) = 15%

Final Debuff = (15% + 15% x (1 - 0.802) x 0.80 = 14.376%

**How to use the Resist/Debuff Calculator**

To calculate all of the final Damage Debuffs use the first calculator in the Resist/Debuff Calculator sheet of Bopper's CoH Tools and Formulas. You can input the total amount of resistance debuffs, unresistable resistance debuffs (rare), damage debuffs, and unresistable damage debuffs. Next, select the Enemy Con level from the drop-down list and you input each of the different Damage Type Resistances the target has. Using our example of the +3 Level 50 AV and our Defender with Enervating Field we can see we get the same results by providing those parameters:

To calculate non-damage debuffs, you might find the bottom two calculators more useful. There is no functional difference between these two calculators, there are multiples simply for Quality of Life (QoL) purposes. You might want to look at two different debuffs side by side, so having two calculators can do that for you. Also for QoL purposes, you can compare up to 4 different targets in each calculator. You can change the purple patch effect and the debuff resistance for each of the 4 targets. Below, I plugged in numbers from a couple of the examples used in this guide:

In the top calculator I plugged in the numbers from the numbers of the Radiation Infection example. The 68.23125% To-Hit Debuff (after enhancement and power boost) is entered into the Debuff field. A hypothetical 40% Res(To-Hit) debuff was entered into the Resistance field. There were no Unresistable debuffs in this example. The final debuff was then calculated for 4 targets: Even Con enemy with no debuff resistance, an even con Level 50 AV with 85% to-hit debuff resistance, a +3 Level 50 AV with 85% to-hit debuff resistance, and a +4 Level 50 AV with 87% to-hit debuff resistance. The PP column represents the purple patch modifier, and its values are manually entered along with the resistances of the targets. The output breaks down the Final Resistance Debuff applied to each target, what the new Debuff Resistance becomes, and what the Final Debuff will be. We can see for the Even Con AV, the debuff was 14.33% and the +3 AV was 8.38%, just like in the examples above.

The bottom calculator performed the hypothetical unresistable debuff example. A 40% Res(To-Hit) was applied, as well at a 15% resistible To-Hit Debuff (after enhancement) and 15% unresistable To-Hit Debuff (after enhancement) were plugged into their respective fields. Compared to the +2 Level 50 AV with 85% to-hit debuff resistance, we see the final resistance debuff, new debuff resistance, and final debuff all match the results provided in the above example.

**Edited July 26, 2020 by Bopper**
