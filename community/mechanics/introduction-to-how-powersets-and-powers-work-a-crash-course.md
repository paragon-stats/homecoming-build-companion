---
title: "Introduction to How Powersets and Powers Work - A Crash Course for Suggestion Writers"
url: "https://forums.homecomingservers.com/topic/30087-introduction-to-how-powersets-and-powers-work-a-crash-course-for-suggestion-writers/"
source: "forums.homecomingservers.com"
authors: ["oedipus_tex"]
date_posted: "2021-07-05"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "discussion"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "f01b0acedf3e2c2c"
---

# Introduction to How Powersets and Powers Work - A Crash Course for Suggestion Writers

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — oedipus_tex, 2021-07-05

Hi all,

One of my favorite things to do is read powers and powerset suggestions in the Suggestions forum. We have some very creative players who've come up with some fantastic stuff. This post is intended to provide some technical parameters to help folks who are writing suggestions.

Keep in mind I am writing this not as a member of the Homecoming development team but rather as an independent player who has explored the CoX code in some detail. The intent of this write up is merely to help folks understand at a very high level how powersets are coded under the hood, which techniques can be used to modify them, and allow them to grasp some high level developer lingo so that they can communicate their visions in more detail.

Throughout this post, I will refer to the designer as a "Power Storyboarder" to hopefully clarify the role.

**Part 1: Anatomy of a Category and Powerset**

Everything starts with two basic concepts: a **Category** and a **Powerset.**

-
**Category: **

-
The Category file lists all of the powersets available to the archetype that has access to that category. For example "Controller Control" would contain Mind Control, Electric Control, Plant Control, Ice Control, etc.

-
As a Suggestion Storyboarder, there isn't a lot for you to worry about with the Category. However, it may simply be helpful to be aware it exists, because this topic will come up again later when we look at pets and pseudo pets.

-
**Powerset:**

-
The Powerset mainly defines the **order of powers** in the set.

-
It is technically possible to have powers unlock at arbitrary levels. However, most unlocks follow a pattern depending on the "tier" of the power.

-
There is no limit on the number of powers a Powerset must contain. The standard is nine, but there can be more or fewer powers.

Here is a screenshot of an unmodified Defender Buff Category file, and the Defender Buff Powerset file that the developer, should they decide to implement your suggestion, would be editing:

With some frequency, especially in newer powersets, there are also powers that are associated with a powerset that aren't technically part of that powerset. Usually these fall into one of two categories:

-
**Temporary Powers Powerset:** This is a master list of all the Temporary Powers in the game. If your Suggestion contains a combo mechanic similar to Water Blast, for example, probably there's a Temporary Power involved.

-
**Inherent Powers Powerset: **It's theoretically possible to provide a power to a player inherently, without them picking anything. Usually this is done based on archetype.

**The more than you need to know portion (skip if not relevant to you): **EVERY power in the game must be in a category and a powerset. This includes not just player powers, but temp powers, enemy powers, inspirations (which are actually powers that delete themselves when you click them), and so on. Matter of fact, pets and pseudo pets often get a whole powerset all to themselves. For example, Ice Slick the Ice Control power has a powerset called Pets_IceSlick that contains one power, the pulse that knocks enemies down. The more you know.

**Key takeaway: **As a Powers Storyboarder, you probably don't need to know too much about this part of the implementation. Just know that creating new powersets is possible, and it is theoretically possible to have powers unlock in an arbitrary order

**Part 2: Power Files, PFX, and FX**

-
**The Powers file itself**

-
The Powers file defines each of the powers in the powerset. This part probably requires the least explanation, because its where you are defining your suggestion. However, if you would like to read about the technical side of this in detail, there's an excellent write up for i24 and previous here: https://wiki.ourodev.com/view/Power

-
Homecoming uses a slightly different engine than some other servers or than the write up above, which I believe mainly involves the inclusion of "Effect Groups" that make it possible to chain two or more effects together so that they roll their Chance at the same time rather than independently.  The above guide should still mainly apply though,

I have added a copy of a full .powers file (issue 24 publicly available version of Defender Fire Blast) to this post.

We do need to talk about some characteristics of the individual power though, discussed below.

-
**PFX: **

-
Within each power definition, a power can point to a **PFX file** that further defines the look and feel of the power.

-
At a high level, PFX files define a power's **animation** and its **particles. **I've included a visual reference to a portion of a PFX file for the Fireball power below to illustrate the concept.

-
**Animation**

-
Power animations are handled by setting variables called "bits." Essentially, when the power is activated, bits are applied to the caster and (potentially) any targets. The creature model is then responsible for performing any animation associated with those bits, taking into account all the various bits that are currently active.

-
In other words, as a Powers designer you are suggesting to the creature model what animation to play. However, based on the sum total of bits that currently exist on the model, it may or may not actually do the animation. For example, it won't animate if the creature is currently being knocked back or dead.

-
It's common for powers to set multiple bits. Powers that use weapons for example usually at a minimum set both a bit for the actual animation, and another bit that tells the model its holding a Sword or whatever.

-
As a Storyboarder, the main thing you should know is that if you identify a power with a cool animation, you can **usually** reuse it for other powers, as long as the cast time is the same.

-
**FX**

-
The FX portions of the PFX file define the **particle effects** to play when various aspects of the power are triggered. Triggered particle effects can be **custom** to the power, or they can be **generic**. For example, most Confusion powers don't define their own ConditionalFX, they just re-use the generic Confusion particles.

-
Some useful categories to know about are:

-
**ActivationFX: **FX associated with activating the power. This is often used with toggle powers.

-
**AttackFX: **The FX file to play when the attack is performed. Used by most clickables (actual attacks and also buff powers).

-
**HitFX: ** The FX file to play if the target is hit. For powers that are not autohit, these FX will not play if the Accuracy roll fails.

-
**ConditionalFX: **The FX file to play conditionally if the target's Mezz Protection is exceeded.

-
For some mezzes, this is also associated with a bit being set.

-
**ContinuingFX: **The FX file to play continually on targets hit by the power for the duration of effect. Think of the dark haze that hangs on enemies hit by Dark powers, for example.

**FAQs for PFX and FX Files:**

**Can a dev use an animation from one power with a completely different particle effect from another?**

Yes this is very possible. There is no direct linkage between animation bits and particles.

**The FX I want to use aren't currently on a player power, but they are on an enemy who exists in game. Is it possible to reuse those effects?**

Generally yes, with some time investment. Most enemy powers aren't coded to accept particle reshading, but with some work it is possible to convert the particles so they work properly.

**Can a dev make a totally new, completely custom animation?**

I think the answer to this is yes, but this is an extreme level of difficulty. I am not sure if any modder has succesfully accomplished it. It would involve creating the animation, correctly setting up the sequencers and bits, and is generally a lot of work. In general I would recommend that Storyboarders stick to existing animations.

**Can a dev create totally new particle FX?**

Yes and no. The tools available make it possible to create effects, but they can be hard to visualize while you are designing. Probably the best bet is for the dev team to copy aspects of existing FX and modify them. With a lot of patience a dedicated dev team could make the effects happen, but your idea probably has a higher chance of being implemented if it reuses portions of other powers.

**Can a dev combine FX from two existing powers into one FX file? For example, my idea would use both aspects of Fire and Ice.**

Yes, this is possible from a technical standpoint.

**I have a question you didn't list here.**

Please feel free to ask in the comments below.

**Part Three: Power Text and Combat Messages**

How far you want to take the messaging in your Suggestion is up to you, but for the sake of understanding I've included a writeup of all

All the text in a powerset/power (and the game generally) is stored in what are called **Message Store (ms)** files. I've pasted an example below.

Messaging is IMO an area where even a non-coder has a lot of room for adding value. Speaking personally, I find creating these files time consuming, mainly because I'm just not that creative when it comes to writing combat text.

The code at the start of each message is called a "pstring." These are generated using "CRC32" encoding. If you decide to go the extra mile and create PString messaging for your suggestion, you can generate CRC32 encoded pstrings using tools like this one: https://www.meridianoutpost.com/resources/etools/php-functions/php-crc32.php

Every pstring number has to be unique. As a protip, it might be helpful to include a calling card in the text to ensure there are no message store collisions from powers with identical descriptions. E.g. if your text is "Stuns the target," which is super generic, you could pstring it as "OEDIPUS_TEXStuns the target" to ensure it will be unique. At the end of the day I don't think it's critical that the final CRC generated number match up to the text, just that every pstring is unique.

I've included a MS file attachment to this post.

**Part Four: Power Icons**

Creation of new power icons is possible and encouraged. If you want to design your Suggestion with icons, I suggest using the power-icon-template.psd document (attached to this post). I wish could credit the person who created this document, but I am not sure who the author is.

This write up will not include a full explanation of how to use Photoshop or GIMP. However, it may be helpful to know that the final icon is exported as a Targa (TGA file) and then undergoes some internal conversion by the developers.

You create the icon using a combo of alpha and color/adjustment layers.

**Part Five: Advanced Topics: Pets and Pseudo Pets**

I'm leaving this spot open for future write up.

Player_Defender_Buff.ms power-icon-template.psd

Defender_Ranged_Fire_Blast.powers

**Edited July 9, 2021 by oedipus_tex**
