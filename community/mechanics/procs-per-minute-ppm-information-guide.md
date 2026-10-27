---
title: "Procs Per Minute (PPM) Information Guide"
url: "https://forums.homecomingservers.com/topic/5290-procs-per-minute-ppm-information-guide/"
source: "forums.homecomingservers.com"
authors: ["Bopper"]
date_posted: "2019-06-27"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "procs-ppm", "set-bonuses-io", "recharge-endurance", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "044106dcd680b77e"
---

# Procs Per Minute (PPM) Information Guide

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Bopper, 2019-06-27

**Procs Per Minute (PPM) Information Guide**

by Bopper

Written: 9 August 2019

Last updated: 16 August 2019
(Information that might help new players in understanding the PPM game mechanics)

**Update (9 August 2019)**

This thread has adapted quite a bit since I first put the call out for Testers to determine how the PPM mechanics actually work and to clear up the confusion from outdated resources (love you Paragon Wiki, but you're i23, and we've moved on). Since then a lot of good information was discovered and plenty of mechanics have been confirmed. Now that I have a good understanding of most of the important pieces, I wanted to change the format of this post. I will still keep the Formulas that have been vetted at the top, that way people can see the important stuff right away. As for the rest of the post, I will slowly build it up to look more like a Guide (since the Mods moved my post to the Guide Section, might as well try to do them proud).

As for building up a guide, that will take time, but I will clean it up and make it nice. For now, I'll try to keep adding sections periodically.

**PPM Formulas**

**Breakdown of Formulas**

Quote

**For Click Type Powers:**

Probability to Proc = PPM * (MRT+ CastTime) / (60 * AreaMod)

**For all other Powers (Toggles, Auto, Pseudopets?):**

Probability to Proc = PPM * ActivatePeriod / (60 * AreaMod)

***Note* **This is not the ActivatePeriod of the power, it is the ActivatePeriod of the Proc Enhancement which seems to always be 10 seconds.

**Modified Recharge Time:**

MRT = BaseRecharge / ( 1 + RechargeBoost_from_Enhancements_and_Alpha / 100 )

***Note*** The MRT is not affected by outside global recharge boosts, such as Hasten, Set IO bonuses, team buffs, etc

**Area Modifier:**

AreaMod = 0.25 + 0.75 * AF

***Note*** This is a dampening effect Phil "Synapse" incorporated for i24. He felt the game's Area Factor punished procs too much

**Area Factor:**

Single Target:     AF = 1

Sphere (PBAoE or Target AoE):     AF = 1 + 0.15 * Radius

Cone:     AF = 1 + 0.15 * Radius - 0.011 * Radius x (360 - Arc) / 30

***Note*** The original Range of a Cone Attack is considered the Radius. Enhancing the Range will not change the Radius, thus will not affect the proc probability.

Chain:     AF = 1 + 0.75 * MaxTargetsHit

***Note* **I have yet to find a power in the game that is tagged as a kEffectArea_Chain that would use this Area Factor. FYI, Refractor Beam is tagged as kEffectArea_Sphere

One Formula to Rule them All (Except Chain):     AF = 1 + Radius x (11 * Arc + 540) / 30,000

***Note* **For Single Target use Radius = 0.

***Note*** For Sphere use Arc = 360.

**Maximum Probability to Proc:**

MaxProb = 0.90

***Note*** Max probability is 90%

**Minimum Probability to Proc:**

MinProb = 0.05 + 0.015 * PPM

***Note*** Min probability is 5% + PPM x 1.5%

**List of PPM values for Procs:**

The PPM for each Proc can be found **HERE** although this information has not yet been vetted for Homecoming servers as of yet. (Credit: @macskull)

**Single Formulas (for Copy/Paste)**

Spoiler

Here are single formulas you can Copy/Paste to perform the entire Proc Probability calculations (including some minor simplification). In the preceding quoted section, I broke down these formulas piece by piece.

**Notes:**

**1.  **I do not include a Chain formula here. I will when I can confirm which powers truly use it. What's listed below is all you need...for now.

**2.  **For Single Target use Radius = 0

**3.  **For Sphere (i.e. non-cone AoE) use Arc = 360

**4.  **For Cone use the original Range (non-boosted) as the Radius

**5.  **The ActivatePeriod is the activation period of the enhancement, which is always 10 seconds. Feel free to replace the ActivatePeriod with 10 in the Toggles formula.

**Click Powers:**

Probability to Proc = PPM * ( CastTime + BaseRecharge / (1 + RechargeBoost_from_Enhancements_and_Alpha / 100) ) / ( 60 * (1 + Radius * (11*Arc + 540) / 40000) )

**Toggle, Auto (and Pseudopets?):**

Probability to Proc = PPM * ActivatePeriod / ( 60 * (1 + Radius * (11*Arc + 540) / 40000) )

**Scope**

The purpose of this guide is to clear up the PPM mechanics that are used in i25+, which incorporated changes made in i24 Beta but never made it to Live. Most of the information in this guide comes from reviewing the original forum posts discussing this topic (Phil “Synapse, circa April of 2012), extensive testing used to confirm the proposals made in the forum discussion, and reviewing source code by the SCoRE team which further confirmed these formulas. The formulas are provided at the top of this guide for easy viewing, and I will update the formulas and this guide when new information is revealed.

**Background**

A “Proc” is a procedure that has a chance of happening. Every time you hit a target (self, ally or enemy) with a power slotted with a Proc enhancement, the effect of the Proc has a chance to trigger. For more information on Procs, check out the ParagonWiki article.

**History (possibly a Drunken History)**

Honestly, I don’t know the whole history, so I apologize if some of this is wrong. Basically, prior to i21 almost all Proc enhancements worked as a fixed percentage. This allowed for many exploits such as slotting AoE powers with a high tick rate to “buzzsaw” enemies. Then sometime in the i21 timeframe, the game introduced Archetype Origin enhancements (ATOs) which introduced the new Procs Per Minute (PPM) mechanic, which used base recharge, area factor, and cast time to calculate the probability of a proc triggering its effect. Finally, in i23, the original Proc enhancements had their fixed percentage mechanics replaced with PPM.

Due to exploits that could be achieved with the PPM mechanics (100% Proc probabilities, increased Proc rates using extremely high recharge builds, etc.), the devs decided to retune the PPM mechanic such that it used global recharge instead of base recharge and cap the probability to Proc at 90% (which was an attempt to preserve the spirit of a Proc always being a random occurrence). There was an obvious outcry as this unfairly punished players who built high-recharge characters and there was no way to predictably control your proc performance when teaming with players who could buff your recharge…which effectively debuffs your Proc performance. After much discussion with players, the devs came to a compromise and decided to only use the enhanced recharge (recharge buffs from enhancements or the Agility/Spiritual Alpha incarnates). The devs also compromised by incorporating a minimum Proc probability of at least 5% and dampened the negative effects of Area Factor so that AoE powers are not too severely punished. This was all incorporated into the i24 Beta when the game abruptly shut down.

**PPM Mechanics**

**Procs Per Minute (PPM):     **

PPM is roughly the average number of times per minute an effect from a Proc should fire. Each Proc enhancement will list this number in its description info and is used directly in calculating the Proc’s probability to trigger.

**Modified Recharge Time (MRT):     **

Modified Recharge Time (MRT) is the power’s actual recharge time when no global recharge boosts are present. If you’re not sure what your MRT is, you can check this in the game. Just right-click on the power, select “info”, and see what is listed as the recharge time. This recharge time will incorporate the recharge enhancements and alpha enhancements installed in the power, after enhancement diversification is factored in (again, global recharge boosts will not affect this).

Sometimes an example is worth a thousand words, so let’s do a few. Let’s assume we look at a power that has no recharge boosts from enhancements nor alpha incarnates, thus its enhanced recharge is 0%. If we look at the same power slotted with a T4 Agility Alpha and 2 Level 50 Invention-Origin Recharge Enhancements, the enhanced recharge is 114% after enhancement diversification. Let’s look at what the actual recharge time would be and the what the MRT would be if we also incorporate a 100% global recharge (achievable with Hasten and 4 LotGs).

| Base Recharge | Enhanced Recharge | Global Recharge Bonus | Actual Recharge Time | Modified Recharge Time |
| 10 sec | 0% | 0% | 10 sec | 10 sec |
| 10 sec | 0% | 100% | 5 sec | 10 sec |
| 10 sec | 114% | 0% | 4.67 sec | 4.67 sec |
| 10 sec | 114% | 100% | 3.18 sec | 4.67 sec |

As we can see, the global recharge bonus has no effect on the modified recharge time. This also highlights how added recharge has diminishing returns. Notice how going from 0% enhanced recharge to 114% enhanced recharge while already having 100% global recharge only decreased the actual recharge time by 1.82 seconds, yet the MRT decreased by 5.33 seconds. If your build already has managed high global recharge, you might be better off not slotting any recharge enhancements into your Proc powers as your actual recharge time will not decrease by much but your Proc performance can greatly decrease due to the significant decrease in MRT.

**Activation Period (ActivatePeriod):     **

Activation Period is the time between when the effects of an ENHANCEMENT trigger. Notice the not-so-subtle all-caps there? That's to emphasize the fact that the Activation Period has nothing to do with the power itself (ok, not exactly, but I'll get to that), but rather how the enhancement functions. When an enhancement is slotted, its effects trigger every 10 seconds (this is the ActivatePeriod) and those effects will last a duration of 10.25 seconds. There is some history to this that I don't recall, but I believe Procs used to work off of the Activation Period of the power, which resulted in silly Proc rates when slotted into powers with a high tick rate (Caltrops was cutely referred to as Proc-trops, if I recall correctly). Anyways, a change was made that resulted in powers such as toggles, autos, etc would only have the Procs pulse once every 10 seconds. Apparently this is how they incorporated it, by using the enhancement's activation period. I apologize for my shoddy description there, but my memory is fuzzy and I just recently discovered this functionality. I bring this up with emphasis because I've seen everywhere else folks talking about using the Activation Period of the power for calculating Proc probabilities (I was also guilty of that). Hopefully this clears that up.

There is one more thing worth mentioning, which does not impact how we calculate the Proc probability, but does detail something about the PPM mechanics. The Proc can only trigger when the power ticks, but will still be able to trigger once in every 10 second interval. That is a mouthful and not necessarily clear, so let me describe an example that I had tested **repeatedly **to confirm this mechanic. World of Confusion has an Activate Period of 4 seconds, so when you cast it (assuming a mob is present), World of Confusion will tick at 0 seconds, 4 sec, 8 sec, 12 sec, 16 sec, 20 sec, and so on. Where I highlighted in yellow is when the Procs will be eligible to trigger. Even though the time between the 2nd and 3rd proc is only 8 seconds, they fall into different 10 second intervals (which cover from 0-9.9 secs, 10-19.9 secs, and 20-29.9 secs). I don't know if this is intended behavior, but you can rest assure that when you attempt to calculate your Proc rates with toggles (or the like) you can expect **on average** one Proc opportunity per 10 seconds.

**Area Modifier (AreaMod):     **

Area Modifier (AreaMod) is an unofficial term used to help distinguish itself from Area Factor, which is used in the game for things outside of PPM mechanics (such as Design Formulas). Basically, AreaMod is the new Area Factor for PPM calculations, which uses the 0.75 weight to dampen the effects on Proc probabilities (per Synapse's design). This is entirely an i24+ concept. It's worth noting the AreaMod (and the Area Factor that feeds into it) is entirely dependent on the area of effect of the power, and has no dependence on the maximum number of targets the power can hit.

**Area Factor (AF):    **

Area Factor (AF) is used in the game to discount the impact of Area of Effect (AoE) powers in relation to Single Target (ST) powers. For example, when it comes to power design formulas, the damage for an AoE attack should be equal to an equivalent ST attack's damage divided by the area factor (in this case, equivalent means having the same recharge time). This same concept was incorporated into the original PPM mechanics, however the impact proved to be more detrimental than anticipated; which is why Synapse proposed dampening its effect in i24 (thus creating the new PPM Area Factor...AreaMod).

**Single Target - AF:    **

The Area Factor of a Single Target attack is always 1. Obviously. It's the thing area attacks are being compared to.

**Sphere - AF:    **

The Area Factor of a Sphere attack applies to any non-cone AoE. These attacks are centered on a location (self, target, or patch) and can hit any target within its radius.

**Cone - AF:    **

The Area Factor of a Cone attack is always less than its Sphere counterpart (same radius). This is simply due to the fact a sphere has 360 degree coverage and a cone has less than that, so it gets a discount. For ranged cone powers, the radius is always equal to the base range of the power (enhancing the range of the power will not change the radius of the power, so no impact on PPM calculations).

**Chain - AF:    **

The Area Factor of a Chain attack depends entirely on the maximum number of targets it can hit. From what I know, there were no Chain attacks prior to i25 (powers described as Chains actually summon numerous pet attacks, resulting in a chain visual effect, but those summoned pets use the Sphere - AF for PPM calculations). Now that we are in i25+, it seems this feature is starting to be implemented. As of this date (14 Aug 2019), only Refractor Beam (Sentinel, Beam Rifle) and Rehabilitating Circuit (Sentinel, Electric Mastery) might (**MIGHT!**) be tagged with the kEffectArea_Chain that would use the Chain - AF. Also, on Beta, there is mention that among the Dominator changes, Trick Shot (Martial Assault) "...is now a proper chain attack, hits up to 10 targets". This suggest its mechanics may be tweaked to a kEffectArea_Chain. Now, if you were getting excited by the prospects of utilizing Procs in Chain attacks - don't. It is a huge nerf. If Trick Shot does in fact become a Chain Attack with a maximum of 10 targets, its AreaMod would be 6.625. That's equivalent to a Sphere attack with a 50 (**FIFTY!**) foot radius. The dev team will seriously need to tweak the Chain - AF formula for it to become viable for Proc usage.

**Frequently Asked Questions:**

**1) How much enhanced recharge can I put into a click-power and still achieve the maximum 90% Proc probability?**

If the Proc has a greater than 90% probability to Proc without enhancement, you can calculate the maximum allowed enhanced recharge as follows:

For those interested, here is the derivation:

Spoiler

Other versions of the same formula include:

**2) What is the impact of adding just a little bit of recharge enhancement into my click-power?**

A lot, actually. The formula shows this already, but MRT will decay rapidly with just a little added recharge enhancement, then will slow its decay rate as it approaches its CastTime limit. Since there are an infinite number of combinations for BaseRecharge and CastTime, I will only do a few examples to demonstrate this point. In the below examples I will assume a 3.5 PPM proc (since it’s the most common), and I will look at Base/Cast pairings of: 10s/1s, 10s/2s, 4s/1s, 20s/2s. You'll notice the general trend is the same throughout.

**Figures:**

Spoiler

In the first 3 figures I wanted to highlight the first 20% drop off then the next 20% drop off. In each case you hit the 80% mark very quickly (~30% enhancement), but the 60% mark is not hit until much later (approximately triple the 80% mark’s enhancement value). So basically, the lesson here is to either go all in on not adding enhanced recharge, or put as much recharge as you want knowing that the drop off in Proc probability will have diminishing returns (the same could be said of the power’s cool down, so perhaps it’s a wash). In Figure 4, I wanted to show that if you are over the maximum probability (90%) you should go ahead and add enhanced recharge up to that limit (in this case, 48% enhanced recharge still kept the 90% Proc probability).

Lastly, please understand what these graphs are speaking to. It is merely suggesting the impact of your Proc probability whenever that power is clicked. It does not speak to your actual in game performance. Ultimately*, the goal is to maximize Proc rate (Procs per minute) which is a function of Proc probability (Procs per activation, which these figures show and requires recharge to be minimized) and Proc opportunity rate (activations per minute, which requires recharge to be maximize). There is a balance between Proc probability and opportunity rate that can be calculated using simple calculus, the parameters of the powers and your attack chain; however that is a unique calculation for every scenario and is outside the scope of this guide.

*Ok, so maximizing Proc rate is not really THE ultimate goal…but you get the point.

**Update History:**

Spoiler

*Update 1: Improving Range for Cones (via Enhancement or Boost) has NO IMPACT on proc performance*

*Update 2: Updated Title of Subject. Originally was "Calling all Testers: Can we confirm how Procs Per Minute (PPM) really works?"*

*Update 3: Max probability is confirmed at 90%. Minimum probability is confirmed at 5% + PPM x 1.5%*

*Update 4: Updated Title of Subject. Previously was "Testing on How Procs Per Minute (PPM) really works" (9 Aug 2019)*

*Update 5: Added the Scope, Background and History. Started to incorporate the PPM Mechanics section (12 Aug 2019)*

*Update 6: Finished PPM Mechanics section. Added FAQ Section (14 Aug 2019)*

*Update 7: Corrected misinformation in the ActivatePeriod section. Testing showed Procs in toggles can trigger once per 10 second intervals, but are not required to wait 10 seconds between Procs (16 Aug 2019)*

**Original Post (27 June 2019, kept for Historical Purposes)**

Spoiler

Hello to all, especially the ever so important testers.

I have been diving as deep as I could into the message boards (new and archived) as well as outdated paragonwiki pages, trying to get an understanding of how the Procs Per Minute (PPM) mechanic works. It seems i24 was supposed to have some new algorithms used for calculating PPM, but I am unsure as to whether or not anything was finalized prior to server shutdown. Below I will provide resources to the different algorithms I have seen:

This is the standard search  for PPM, which is a hodgepodge of i23 mechanics with projected i24 mechanics sprinkled in. There's not much to say about this, other than folks seem to not pick up on what is from i23 and what is actually in the game today. Basically, we need to get away from thinking the calculations are using Base Recharge, but rather Enhanced Recharge (recharge that gets slotted into the power, including the agility alpha incarnate, but does not factor in the recharge boosts provided by buffs and Set IOs).

This source shows much of the same formulas as those provided in the previous link, however it tweaks it with a 1.25 and a 0.75 factor. This was cited around April of 2012.

i24 Proc Chance = (Enhanced Recharge + Activation Time)*(Current PPM * 1.25)/(60*Area Factor)

Area Factor = 1 + 0.75*(0.15*Radius - 0.011*Radius*(360-Arc)/30)

Doing some algebra simplification we can show Area Factor to be:

Area Factor = 1 + 0.75*(R*(11A + 540)/30000)

This thread, which seemed to go up until the day NCsoft terminated the Paragon Studios development team, used a version of the formula above; however, Hopeling's formula did not include the 0.75 muliplier. Also it seems the 1.25 multiplier to Current PPM was expected to occur with i24, so for example, the Force Feedback proc had a 1.5 PPM in i23 and it was expected all PPMs would increase by 25%. In i25, the Force Feedback proc is 2 PPM, so I suspect the PPMs shown in game today (i25) are correct for use in the following formula:

i25 Proc Chance = PPM * (Enhanced Recharge + Activation Time) / (60*Area Factor)

Area Factor = 1 + (Radius*(11*Arc + 540)/30000)

As you can see, I am getting a lot of information from old sources and there is no confirmation on anything that is currently implemented. This does not include the other nuances that come with calculating Proc Probability, such as ticks from powers that are toggled, a "pet" such as bonfire that does multiple ticks of damage, powers that have DoT ticks, summoned pets with procs slotted, etc.

Are there any good and curious testers who have the experience to take on this type of project? To be able to verify how PPM is calculated, how it works for different types of powers, and to get the story straight once and for all on how PPM works?

I thank you all for your time and consideration. I greatly hope good answers can come from this.

**Still Not a Guide Writer!**

**Edited August 3, 2020 by Bopper**
