---
title: "What The Hell Is \"Doublehit\"? - A Hybrid Assault Guide"
url: "https://forums.homecomingservers.com/topic/7404-what-the-hell-is-doublehit-a-hybrid-assault-guide/"
source: "forums.homecomingservers.com"
authors: ["Hopeling"]
date_posted: "2019-08-03"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["assault", "procs-ppm", "incarnate", "recharge-endurance", "damage-dps", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "9697089ccb2cf8dd"
---

# What The Hell Is "Doublehit"? - A Hybrid Assault Guide

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Hopeling, 2019-08-03

**What The Hell Is "Doublehit"? - A Hybrid Assault Guide**

This guide sets out to answer two questions:

-
What does Doublehit actually do?

-
Should I take Assault Core or Assault Radial?

This is a guide that I was planning to write back when i24 and the Hybrid slot were still on the test server in 2012. I shelved the project after the shutdown announcement, but recently dug out my old notes and decided to finish it.

There is a lot of incorrect information out there about Hybrid Assault Radial and its Doublehit mechanic. I've seen people say that it deals a percentage of the power's damage, that it deals the power's base damage, that it's affected by slotting, and general confusion over what it does. None of these are correct. It's a strange power which works in a strange way. But based on my old notes, and some testing both then and now, I have a fairly complete picture of what it actually does.

1. What Does Doublehit Actually Do?

The t4 version of Doublehit, from Radial Assault Embodiment, is a 6 PPM proc on all your attacks. Its damage depends on the power being used. Specifically it depends on two things about the power:

-
its base recharge, that is, the recharge before any slotting or buffs. For toggles, I believe the tick frequency should instead be used, which is 2 seconds for most damage auras, but I have not tested this directly.

-
its areafactor, which is given by a fancy formula: ((1+(Radius*0.15))-(((Radius/6)*0.011)/5)*(360-Arc))﻿. For single-target powers, this simplifies to 1. For spherical AoEs, it simplifies to (1+(Radius*0.15)).

Once you know these two things, here's how you calculate how much damage a doublehit proc will do:

Doublehit Damage = 0.4×0.2×[0.8×(Recharge)+1.8]×107.09/Areafactor

If the power's recharge time is less than 1 second or more than 30 seconds, it gets "clamped" to those values.

Where Did That Come From? Are You Making This Up?

Nope, that's the real formula. But it's not pulled from thin air: if you hop over to the Design Formulas thread, you'll recognize this as the damage formula, with a base damage of 107.09 damage plugged in, and then a coefficient of 0.4 applied.

Since the usual base damage in that formula is (55.61×AT damage scale) instead of 107.09, what Doublehit does when it procs is it deals 77% of what the power's base damage would be, if that power followed the damage formula, and if your archetype had a damage scale of 1.00.

What are these "ifs" about? Well, there's two things going on:

-
Every AT has a damage scale. You can view them raw here, but most people are more used to the user-friendly interpretation on ParagonWiki. Your AT damage scale determines how much damage your powers do: a Blaster's ranged scale of 1.125 is 50% higher than a Corruptor's ranged scale of 0.75, so Blasters will do 50% more base damage with ranged powers. But Doublehit doesn't care what your AT is. We'll see later that this has implications for Core vs Radial comparisons.

-
The damage formula is a guideline for designing powers, but not all powers follow it. For example, Foot Stomp has a 20s base recharge and a 15-foot radius, so according to the damage formula, its base damage for a level 50 Tanker "should" be 0.2×(0.8×20+1.8)×44.488/(1+.15×15) = 48.73 damage. But a quick peek into Pine's will show you that it actually deals 63.17 damage base. Foot Stomp breaks the damage formula, so Doublehit will deal less than 77% of Foot Stomp's base damage, because Foot Stomp's damage is higher than it "should" be.

What About Other Tiers of Assault?

Man, I have no idea.

Ostensibly, lower tiers should have a lower PPM and a reduced coefficient (instead of the 0.4 used above). But I could not tell you what they are. The numbers in my old notes came from a dev post in the old forums. I have no idea which thread it was, and looking for things on the old forums is a pain. If someone wants to do it, or do some in-game testing, I'll be happy to update the guide.

2. Should I Take Assault Core or Assault Radial?

Now, to answer the question most people are actually interested in: which is better? Unfortunately, the answer is "it's complicated".

To compare the two, you have to also know what Core does. The t4 Core Assault Embodiment power gives each of your hits a 65% chance to give you a +15% damage buff, lasting 10 seconds, and stacking up to 5 times. A 10s duration means that to stay at 5 stacks, you'd have to get a stack every 2 seconds. Attacking every 2 seconds is feasible, but there's only a 65% chance on hit to get a stack. This means that, unless you have very fast attacks, you won't be hitting 5 stacks very often on a single target. But AoE powers get a chance to proc on each separate hit, so getting 5 stacks is very feasible when there's more than one enemy around - ie, most of the game.

If you have 5 stacks of Core, that's +75% damage. That is, Core adds 75% of the power's base damage, and it does that to every hit. Radial adds 77% of what "should" be the power's base damage, ignoring AT modifiers. But it's a proc; it doesn't go off on every hit. The math for comparing these is tedious, because you have to use the above formula AND do PPM calculations, so I made a spreadsheet. Feel free to download a copy and enter the info for the powers you use. By default I've put the "Stacks of Core Buff" setting at 4 stacks; play with that to see how it performs in ideal or not-ideal situations.

Some Cliffs notes that may help you decide, if you don't want to crunch a ton of numbers:

-
Doublehit ignores AT modifiers, but Core does not. If your AT has a damage scale below 1.000, lean towards Radial. If it's above 1.000, lean towards Core. If you have a lot of pets (whose attacks usually have low base damage, relative to player attacks), again lean towards Radial.

-
Core is generally a better choice for AoE damage. Core is at its strongest in AoE situations since it can build stacks easily. Meanwhile, Doublehit double-dips on recharge and areafactor: a power with longer recharge has a higher proc chance AND deals more damage when it does proc; a power with a larger area has a lower proc chance AND deals less damage when it does proc.

-
Core is generally a better choice if most of your attacks have short recharge times, by the same reasoning.

-
Doublehit is not subject to the damage cap, because it's not a damage buff, it's a proc. If you're often at the damage cap, Core does nothing for you, so go Radial.

-
Radial works at full strength whenever it's toggled on, while Core needs to build stacks. If you spend a lot of time throwing support powers, buffing your pets, etc, then you won't be building stacks very effectively, so go Radial instead.

Hopefully this guide left you less confused than you were before reading it, but if not, please feel free to ask questions. Either power is quite strong overall, so if you're getting anxiety over this much math, don't stress a great deal about picking the "right" one.

**Changelog**

-
08/04/19: Fiddled with some phrasing. Fixed proc chance calculations. Update info about Core stacks from AoE powers.

-
06/30/22: New link due to a Google security update; the spreadsheet should be accessible again.

**Edited June 30, 2022 by Hopeling**
