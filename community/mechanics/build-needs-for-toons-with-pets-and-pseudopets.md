---
title: "Build Needs for Toons with Pets and Pseudopets"
url: "https://forums.homecomingservers.com/topic/60549-build-needs-for-toons-with-pets-and-pseudopets/"
source: "forums.homecomingservers.com"
authors: ["Jacke"]
date_posted: "2025-09-04"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "procs-ppm", "set-bonuses-io", "incarnate", "mechanics-formulas", "tools"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "48e0f58efd141555"
---

# Build Needs for Toons with Pets and Pseudopets

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Jacke, 2025-09-04

EDIT: The situation for Mastermind Henchmen is more complex.  @Maelwys has done a good analysis here with slotting recommendations.

https://forums.homecomingservers.com/topic/59720-question-on-pet-power-slotting-procs-vs-io-bonus/#findComment-743792

The first thing any Toon that depended on Pets or Pseudopets (example all Masterminds, Traps Toons (8 out of 9 Powers, all but Web Grenade), etc.) needs to do is get their Pets final ToHit close to 95%.

The vast majority of Pets do not benefit from the Global Buffs that the Caster has.  No Level Shifts (except from the MM Inherent Supremacy), no Global ToHit, no Global Accuracy.  Some Pets (like Trip Mine for all ATs) don't even get benefit from the Leadership Auras (there are flags that exclude those buffs).  Only Pets who are considered an extension of the Toon benefit from the Global Buffs.

The example I use to point this out, especially in CoD2, are 2 Pets on a Electrical Affinity/Electrical Blast Defender:

-
Electrical Blast's T8 Voltaic Sentinel is an extension of the Toon and does benefit from the Caster's Global Buffs.

https://cod.uberguy.net/html/power.html?power=defender_ranged.electrical_blast.voltaic_sentinel&at=defender

-
Note the Double Green Box on the right side of the Create Entity Effect, "Boosts are copied to entity".

-
That means the slotted Enhancements are copied (with some restrictions shown elsewhere) to the entity, the Pet.

-
Elsewhere this is called "Use Combat Mods".

-
Note the Double Red Box on the right side of the Create Entity Effect, "Entity has creator's modifiers".

-
That means the Caster's Global Buffs are copied (with some restrictions shown elsewhere) to the entity, the Pet.

-
Elsewhere this is called "Use Caster Mods".

-
Note that the Effects of the Pets are all listed immediately here.

-
Note that in game, the Pet cannot be targeted.

-
Any aggro the Pet gets is immediately passed to the Caster.

-
Electrical Affinity's T3 Galvanic Sentinel is like the vast majority of Pets and Pseudopets and doesn't benefit from Caster's Global Buffs.

https://cod.uberguy.net/html/power.html?power=defender_buff.shock_therapy.discharge&at=defender

-
Note there's only a Double Green Box on the right side of the Create Entity Effects, "Boosts are copied to entity".

-
That means the slotted Enhancements are copied (with some restrictions shown elsewhere) to the entity, the Pet.

-
Note that there's a link to a separate Entity page for the created Pet.

-
Where you see a lot of information and links to the Pet's Powers.

-
Note that in game, the Pet can be targets usually (not all Pseudopets of this type can be targeted).

-
This type of Pet includes all Mastermind Henchmen.

As well, there is a large and effectively unfixable bug in Mids Reborn: It doesn't recognise this difference and always gives Pets the benefit of the Caster's Global Buffs.  You cannot depend on MRB's calculations of the final ToHit chance of the vast majority of Pets and Pseudopets.

To avoid calculating every case of the vast majority of Pets, I have 2 rules I use to get the Pet's final ToHit close to 95% in content the Casting Toon may see as +3 (+4 with the Toon +1 due to an Incarnate T3 or T4 Alpha Boost slotted--which the Pet sees as +4 all the time):

-
Slot the Pet Casting Power with ED-capped Accuracy (~93%+).

-
If the Pets has important Attack Powers that aren't autohit and have a Base Accuracy of under 1.2, do one or both of the following:

-
Have the Caster take Leadership's Tactics, even just the base Slot on a Mastermind.

-
Have a -Def Power applied to the potential targets.

Two examples of Pets who's Attack Powers have 1.2 Base Accuracy:

-
Devices' Gun Drone

-
Fire Control's Imps

Most Pet IO sets do not have enough Accuracy, requiring mixed slotting of different IO sets to get there.  This has to be done for ALL Pet and Pseudopet Summons Powers of the second type from above.  Some Pet Toons won't have a -Def Power, so in that case they'll often need Tactics.

Mastermind Henchmen have other Level reductions from that of the Mastermind that complicates this.  Mastermind Supremacy Inherent Power can offset this, but Henchmen need to remain close enough to the MM.  I have heard that this won't be changed until other changes go in.

**Edited October 13, 2025 by Jacke**
