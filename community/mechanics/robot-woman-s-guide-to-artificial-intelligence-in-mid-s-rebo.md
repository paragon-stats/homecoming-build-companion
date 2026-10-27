---
title: "Robot Woman's Guide To Artificial Intelligence In Mid's Reborn"
url: "https://forums.homecomingservers.com/topic/20467-robot-womans-guide-to-artificial-intelligence-in-mids-reborn/"
source: "forums.homecomingservers.com"
authors: ["magicjtv"]
date_posted: "2020-06-26"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "set-bonuses-io", "defense-resist", "recharge-endurance", "mechanics-formulas", "tools"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "cc5d339c2acc1bd2"
---

# Robot Woman's Guide To Artificial Intelligence In Mid's Reborn

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — magicjtv, 2020-06-26

**Introduction**

This guide covers three new related features in Mid's Reborn Hero Designer: **The Bonus Wizard,The Perma Wizard**, and **The IO Bonus Spreadsheet**. These features are designed to help players, new and veteran, get the most from their toons.

**Current release**

Mids AI MVP Installer version 0.1.0 - Code-named Maria after the robot in Metropolis .

Recommended Practice

Download the app and do the walkthrough. Then read the post on Templates. After that, you'll be able to get good results with the Bonus Wizard. You can pick up everything else at your own pace.

**Bonus Wizard Walkthrough**

In this walkthrough, we'll take a Spines/Fiery Aura Brute from Zero to Hero in just a few clicks. **You can find the original build used for this walkthrough attached to the end of this post.**

The Bonus Wizard is an artificial intelligence package that is by far the most extensive of the three new features. It's purpose is to maximize the IO bonuses your toon gets. Here we do a quick walkthrough of the Wizard and show what kind of results it's capable of producing. The Wizard has five main screens. We'll cover each of the quickly here, and in more detail later in the guide.

**Accessing The Bonus Wizard**

The new Artificial Intelligence menu has an option for invoking the Bonus Wizard.

**Screen 1 - Welcome**

The first screen is an introduction screen. In the bottom right you see the Wizard navigation buttons. Click Next.

**Screen 2 - Select Bonus Types**

The second screen is where we tell the Wizard what types of bonuses we want and, if we select more than one type of bonus, what their relative importance is.

Importance

In this walkthrough we're going to select an importance of Top to Bottom Capped from the Importance dropdown. This option tells the Wizard to always try and improve the first bonus we enter before trying to improve the second, to always try improving the second before trying to improve the third, and so on. Selecting Top to Bottom Capped also reveals Capped fields that let us work with capped values. When a bonus reaches the value of the cap, the Wizard no longer tries to improve it.

Bonus To Add

The Bonus To Add dropdown is where you select a bonus and add it to your list of desired bonuses. You can add up to five bonuses.

To Add A Bonus

-
Select the type of bonus you wnat from the Bonus To Add Dropdown.

-
If you want to include PvP bonuses check the PvP box.

-
If you selected Top to Botoom Capped as the importance, enter a cap value in the Cap field.

-
Click the Add button.

For this walkthrough we're going to add two bonuses:

-
Defense (Fire) with a cap of 0.45 (That's the defense soft cap).

-
Resistance (Fire) with a cap of 0.9 (That's 90% resistance. It's above the Brute hard cap, but we'll go with that number anyway.)

When you've entered these bonuses, your screen should look like the one shown below.

**Screen 3 - Select Generations And Iterations, Set Options**

The third screen is where we where we set various performance options. We'll talk about these later in the guide, but for now just click Next.

**Screen 4 - Evolve Toon**

The fourth screen evolves your toon, attempting to maximize all the bonuses you requested. This process starts automatically. There are various charts and indicators that show how far along the evolution is.

Once the evolution completes click next.

**Screen 5 - Save Toon**

The Wizard does not overwrite your toon. In order to keep the changes made by the Wizard you must save it to disk using the Save button in the lower left of the screen.

**Results**

The results of this sample run are shown below. The toon is over the Fire Resistance cap and almost at the Fire Defense cap. In fact, adding a single defense enhancement to Weave puts the toon over the soft cap for Fire Defense. There are also quite a few empty slots in the build that can be moved around to increase damage.

**Villain Plan by Mids' Reborn : Hero Designer 2.7.2.10**
**https://github.com/Crytilis/mids-reborn-hero-designer**

**Click this DataLink to open the build!**

**toon: Level 50 Magic Brute**
**Primary Power Set: Spines**
**Secondary Power Set: Fiery Aura**
**Power Pool: Speed**
**Power Pool: Leadership**
**Power Pool: Force of Will**
**Power Pool: Fighting**

**Villain Profile:**
**Level 1: Lunge** -- SprBlsCol-Acc/Dmg(A), SprBlsCol-Dmg/EndRdx(3), SprBlsCol-Acc/Dmg/EndRdx(3), SprBlsCol-Acc/Dmg/Rchg(5), SprBlsCol-Dmg/EndRdx/Acc/Rchg(5), SprBlsCol-Rchg/HoldProc(7)
**Level 1: Fire Shield** -- Ags-ResDam/EndRdx(A), Ags-ResDam/Rchg(7), Ags-EndRdx/Rchg(9), Ags-ResDam/EndRdx/Rchg(9)
**Level 2: Blazing Aura** -- ScrDrv-Acc/Dmg(A), ScrDrv-Dmg/EndRdx(11), ScrDrv-Dmg/Rchg(11), ScrDrv-Acc/Rchg(13), ScrDrv-Acc/Dmg/EndRdx(13), ScrDrv-Dam%(15)
**Level 4: Healing Flames** -- StdPrt-ResDam/Def+(A), GldArm-3defTpProc(15), Empty(17), Empty(17), Empty(19), Empty(19)
**Level 6: Build Up** -- Empty(A), Empty(21), Empty(21)
**Level 8: Spine Burst** -- SprAvl-Acc/Dmg(A), SprAvl-Dmg/EndRdx(23), SprAvl-Acc/Dmg/EndRdx(23), Arm-Dmg(25), Arm-Dmg/Rchg(25), Arm-Acc/Dmg/Rchg(27)
**Level 10: Temperature Protection** -- Ags-ResDam/EndRdx(A), Ags-ResDam/Rchg(27), Ags-EndRdx/Rchg(29), Ags-ResDam/EndRdx/Rchg(29), Ags-ResDam(31)
**Level 12: Impale** -- Empty(A), Empty(31)
**Level 14: Hasten** -- Empty(A), Empty(31), Empty(33), Empty(33), Empty(33), Empty(34)
**Level 16: Plasma Shield** -- Ags-ResDam/EndRdx(A), Ags-ResDam/Rchg(34), Ags-EndRdx/Rchg(34), Ags-ResDam/EndRdx/Rchg(36), Ags-ResDam(36), Ags-Psi/Status(36)
**Level 18: Quills** -- Empty(A), Empty(37), Empty(37), Empty(37)
**Level 20: Consume** -- Empty(A), Empty(39), Empty(39), Empty(39)
**Level 22: Taunt** -- MckBrt-Taunt(A), MckBrt-Taunt/Rchg(40), MckBrt-Taunt/Rchg/Rng(40), MckBrt-Acc/Rchg(40), MckBrt-Taunt/Rng(42), MckBrt-Rchg(42)
**Level 24: Assault** -- Empty(A)
**Level 26: Ripper** -- Empty(A)
**Level 28: Burn** -- Empty(A)
**Level 30: Tactics** -- Empty(A)
**Level 32: Throw Spines** -- SprFrzBls-Acc/Dmg(A), SprFrzBls-Dmg/EndRdx(42), SprFrzBls-Acc/Dmg/EndRdx(43), SprFrzBls-Acc/Dmg/Rchg(43), SprFrzBls-Dmg/EndRdx/Acc/Rchg(43), SprFrzBls-Rchg/ImmobProc(45)
**Level 35: Fiery Embrace** -- Empty(A)
**Level 38: Weaken Resolve** -- SphIns-ToHitDeb(A), SphIns-Acc/ToHitDeb(45), SphIns-Acc/Rchg(45), SphIns-ToHitDeb/EndRdx/Rchg(46), SphIns-Acc/EndRdx/Rchg(46), SphIns-%ToHit(46)
**Level 41: Boxing** -- Empty(A), Empty(48)
**Level 44: Tough** -- Ags-ResDam/EndRdx(A), Ags-ResDam/Rchg(48), Ags-EndRdx/Rchg(48)
**Level 47: Weave** -- Empty(A), Empty(50), Empty(50), Empty(50)
**Level 49: Victory Rush** -- Empty(A)
**Level 2: Swift** -- Empty(A)
**Level 2: Health** -- Empty(A)
**Level 2: Hurdle** -- Empty(A)
**Level 2: Stamina** -- Empty(A)
**Level 1: Brawl** -- Empty(A)
**Level 1: Prestige Power Dash** -- Empty(A)
**Level 1: Prestige Power Slide** -- Empty(A)
**Level 1: Prestige Power Quick** -- Empty(A)
**Level 1: Prestige Power Rush** -- Empty(A)
**Level 1: Prestige Power Surge** -- Empty(A)
**Level 1: Fury**
**Level 1: Sprint** -- Empty(A)
**Level 2: Rest** -- Empty(A)
**Level 4: Ninja Run**
------------

**Original Spines/Fiery Aura Brute Build Used For This Walkthrough**

mids_build.mxd

**Edited July 22, 2020 by magicjtv**
