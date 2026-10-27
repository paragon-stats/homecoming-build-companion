---
title: "Build Swap Macro Tutorial"
url: "https://forums.homecomingservers.com/topic/6450-build-swap-macro-tutorial/"
source: "forums.homecomingservers.com"
authors: ["Gulbasaur"]
date_posted: "2019-07-16"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "set-bonuses-io", "recharge-endurance", "leveling-content"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "f4bda2a9a7c59e5c"
---

# Build Swap Macro Tutorial

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Gulbasaur, 2019-07-16

This tutorial will show you how to create a macro button for your power bar that change your build.

For those who don't know, you can speak to a trainer to set up a new build from level 10. It will reset your powers back to level 1 and you can level up again like a respec but it saves your old build so you can go back to it later.

It's a bit like having two characters in one. I recommend giving your builds a name to help you remember them. The command uses then number, but a name helps you remember which is which. Your builds are numbered from 1 to 3. You default build is build 1.

You might want to do this because:

- VEATs literally have branching builds that play very differently. You can swap from a Night Widow to a Fortunata mid-mission.

- Peacebringers might want to have an all-human build and a tri-form build.

- One build for enhancement sets and one for Flashback missions using basic IOs that scales down better without losing bonuses.

- One build for farming and one for general play.

- A travel build that you switch out of when you arrive at a mission that swaps out your travel power for another attack or defensive skill. (See note about recharge times)

- Your build has a lot of situational powers - this allows you to change to suit the mission.

- Solo vs teamed builds - Support archetypes can drop ally-only powers in exchange for more personal damage and then switch back at the press of a button if they need to.

- You want to roleplay a "Jekyll and Hyde" Empath who switches from passive do-no-harm healy buffbot to rifle-wielding murdertron with a flashy animation in between.

**The most basic way** of setting this up is:

To create an icon to switch to Build 1, type this in the chat box:

/macro B1 select_build 1

To create an icon to switch to Build 2, type this in the chat box:

/macro B2 select_build 2

This will create icons called B1 and B2 that allow you to click them to change builds. Simple, but not very elegant.

**If you want to add icons and a tooltip** (thanks to u/mudflapgirls and u/Antaniserse on Reddit for posting how to do this) you need to choose an icon from this list and use the template it gives you to create something like this:

/macro_image "Experimentation_Jaunt" "Fortunata" "select_build 2"

Note the speech marks around each part. This creates an icon in my power bar with the Experimentation Jaunt icon (the teleport icons work well for this, I think), named "Fortunata" and when I click on it I switch to Build 2 (my Fortunata build).

**If you want to get really fancy**, you could create a costume for each build and trigger a costume change emote from this list. Costume slots start counting at 0, so your default costume slot is 0 and the first one after that is 1.

Combining all of this creates something like this:

/macro_image "Experimentation_Jaunt" "Fortunata" "select_build 2$$cce 2 CCDimensionShift"

Note the speech marks around each part. The $$ allows you to join two commands together in one macro so it basically says "Select Build 2 and put on Costume 2 using the Dimension Shift emote".

This allows me to go into a mission as my Night Widow and fall back if I need more range or control and to shift to my Fortunata build without leaving the mission and it's bloody brilliant. Visually, my Night Widow wears goggles and my Fortunata build has them off and has an eye aura in their place, showing that their psychic powers have been unleashed - it's a silly touch but it's a silly game so I like it.

A few caveats:

- The cooldown is 60 seconds on build swaps, so if you click it prematurely your costume and build will get out of sync.

-
**Changing builds detoggles every power and puts everything on cooldown**, so be aware of that if you use any long-cooldown buffs etc. This isn't something you should do in combat or if you use the prestige and T9 powers a lot. Plan ahead.

- Maintaining multiple builds can be expensive, so it's not something you should do with your first character at level 10. Wait until you can afford the enhancements.

- You have to speak to the trainer twice when you level up - once on each build. This is actually pretty cool as it goes from having to pick between two powers to "this goes on my solo build and that goes on my teamed build".

- Macros are unique to each build, so you need to set your Build 1 swap macro on Build 2 and vice versa.

Any questions or help, please ask!
