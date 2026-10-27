---
title: "Hybrid Slot, Assault Radial vs. Core"
url: "https://forums.homecomingservers.com/topic/4489-hybrid-slot-assault-radial-vs-core/"
source: "forums.homecomingservers.com"
authors: ["Cheli"]
date_posted: "2019-06-17"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["assault", "procs-ppm", "incarnate", "damage-dps", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "f71112181fa39c53"
---

# Hybrid Slot, Assault Radial vs. Core

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Cheli, 2019-06-17

This question is brought up constantly in help chat, and because a lot of people really don't seem to know how it works, I'm going to break it down. This is all stuff I read/absorbed from various places about the hybrid slot; it's not my research, and I'm not an expert, but I know how it works and I figured I would help people who are always asking this (and people who don't seem to understand it).

**Core**

Core is a stacking buff that gives you 15% damage, up to a maximum of 75% damage at 5 stacks. This is considered +dmg% (which you can track on your combat stats), so it's subject to the +dmg% cap. What this means is that when you're at 5 stacks you're doing your base damage times 1.75.

**Radial**

Radial adds 'a chance for your power to hit twice'. What it actually does is, when it procs, it does the power that activated it's damage again. However, it only does the base damage the power deals, because it uses your AT's 'temporary power damage' stat (which afaik is 1.00 for every AT).

Every AT has invisible stats that control how much damage their attacks do. This is why a blaster does more damage with blast powers than a defender, or a scrapper does more than a tanker. If you took a generic attack that dealt 100 damage, for example, you would take that base damage (100) and multiply it by your AT's damage scale. As an example, a scrapper's melee attack scale is 1.125, and they have a 10% chance to crit; brute melee attack damage scale is .750, significantly lower (to allow for the Fury mechanic to do its thing).

That means that if those two ATs use that 100 damage attack, the Scapper version will do ~112 damage and the Brute version will do 75 damage.

The thing is that the radial doublehit effect does not scale with your AT's damage scale. That means that when the doublehit procs, the attack will do -

Scrapper - 112 damage + 100 damage (doublehit)

Brute - 75 damage + 100 damage (doublehit)

Doublehit also does not scale with critical strikes. If you crit with a scrapper and that crit triggered doublehit, it would deal 224 (double damage) + 100 (doublehit), **NOT** 224 + 224.

**Comparison**

Radial is better for ATs with lower base damage because the 'doublehit' actually does MORE damage, to scale, than the base power. Core is better for ATs that have higher base damage because the 'doublehit' does LESS damage, and DOESN'T scale with crits.

If you look at this mathematically, it's pretty glaring. Doublehit has, iirc, 6 procs per minute. Taking, again, a generic power that deals 100 base damage, we'll say you can use that power 10 times over the course of 1 minute. In that same 1 minute, you'll get 6 doublehit procs, and a scrapper will, over 10 hits, deal 1 critical strike for double damage.

Radial

Scrapper:

10 hits for 112 damage = 1120 damage

1 critical hit for = 224 damage

6 doublehit procs for = 600 damage

1120+224+600 = 1944 damage over a minute.

Brute:

10 hits for 75 damage = 750 damage

6 doublehit procs for = 600 damage

750+600 = 1350 damage over a minute.

Core

Scrapper:

10 hits for 112 damage = 1120 damage

1 critical hit for = 224 damage

multiply ALL damage by 1.75

= (1120+224)x1.75

=2352 damage over 1 minute

2352-1944= **408 extra damage** you're dealing each minute with Core over Radial as a scrapper.

Brute:

10 hits for 75 damage = 750 damage

multiply ALL damage by 1.75

= 750x1.75

=~1313 damage

1313-1350 = **37 less damage** you're doing each minute with Core over Radial as a brute.

So what does that mean?

It means if you're playing an AT like a scrapper that has a base damage scale HIGHER than 1.00, you're losing out on damage if you're using doublehit, and if you're playing an AT with a base damage scale LOWER than 1.00, you're better off with doublehit.

Which ATs have higher than 1.00 damage scales?

Scrappers, blasters with their ranged powers, and (I think?) dominators with their ranged powers. Stalker base damage is 1.00; however, they have both random and guaranteed critical hits, which again don't scale with doublehit, so are also better off with Core.

Everyone else has either 1.00 or lower base damage with most of their attacks, and will want to consider taking Radial instead. This is especially true for ATs that just do low damage in general like Defenders and Controllers. The exception to this is damage sets that use lots and lots of big AOEs; because of how procs interact with AOE range and power cooldowns, proc-based effects with big AOEs are generally less valuable, and so a brute or tank who uses lots of those will maybe be better with Core. (I'm not as clear on this.)

I don't know how incarnate powers interface with Mastermind pets, or Kheldian shapeshifts, so take this with a grain of salt if you play one of those.

The only other times Core is **worse** for a scrapper, blaster or stalker are:

- You have to constantly interrupt your attack chain enough that it interferes with your ability to maintain your stacks of Core. I generally never have this problem, personally, on these ATs, though, and even then, it would need to be often enough that you're losing most or all of your stacks, since even a +60% damage bonus is still better than the doublehit damage.

- You spend most of your time sitting at capped +dmg%, by using trays full of reds. Or you run with a kin who dumps Fulcrum Shift on you all the time. I don't think this is terribly practical, though, and if you're not +dmg% capped like more than half of your playtime, you're probably losing damage over if you used Core.

Basically, the lower your AT's base damage is from 1.00, the better doublehit is; the higher it is (and, if you crit more often like a stalker does), the better Core is.

There's a list of AT's base damage scales here.
