---
title: "Interface Damage over Time (DoT) Procs: How to quantify their effects"
url: "https://forums.homecomingservers.com/topic/15370-interface-damage-over-time-dot-procs-how-to-quantify-their-effects/"
source: "forums.homecomingservers.com"
authors: ["Bopper"]
date_posted: "2020-02-14"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "procs-ppm", "set-bonuses-io", "incarnate", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "b25bc8777d771853"
---

# Interface Damage over Time (DoT) Procs: How to quantify their effects

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Bopper, 2020-02-14

**Interface Damage over Time (DoT) Procs**

**How to Quantify Their Effects**

by Bopper

Written: 14 February 2020

Last updated: N/A

Some quick news. The bug causing Interface DoT procs to under-perform has been found and a fix should be easy enough to implement and hopefully will go live soon. With that being said, I wanted to put together a quick guide (or crib sheet, really) that details how you should quantify the effects of the Interface DoTs (once the bug is fixed, that is). This should help you decide if/which Interface DoT is right for your build.

**How do the Interface DoT procs work?**

The description of the Interfaces will read as “Adds… a Minor/Moderate DoT (X% chance) to most damaging attacks”. This phrasing causes some confusion as most assume this to mean there is an X% chance that each of your attacks will add 5 ticks of Damage over Time. In actuality, the Interface procs are mechanized such that each of the DoT ticks will have an X% chance to proc; however, once a tick misses all remaining DoT ticks are cancelled. This is what’s referred to as “Cancel-On-Miss”.

**How much damage do Interface DoT procs do?**

There are 5 Interfaces that offer a DoT proc: Cognitive (Minor Psionic), Degenerative (Minor Toxic), Reactive (Moderate Fire), Preemptive (Moderate Energy), and Spectral (Moderate Negative Energy). The Minor damage DoTs do up to 5 ticks of 10.71 damage each, and the Moderate damage DoTs do up to 5 ticks of 13.39 damage each (25% stronger than minor damage). Those numbers assume same level enemies (refer to Purple Patch mechanics if you’re considering fighting weaker/stronger enemies).

**What can I expect from a 25% DoT proc?**

This is the lowest performer of the bunch as it is very likely to miss on an early damage tick, resulting in all subsequent ticks to be cancelled. That being said, what is the expected performance?

| X | Prob(X) | E[X] =X*Prob(X) |
| 0 | 75.00% | 0 |
| 1 | 18.75% | 0.1875 |
| 2 | 4.69% | 0.09375 |
| 3 | 1.17% | 0.035156 |
| 4 | 0.29% | 0.011719 |
| 5 | 0.10% | 0.004883 |
| Total | 100% | 0.333008 |

These numbers tell us that on average, each attack will do 0.333 ticks of damage. Given that a Minor DoT does 10.71 damage (Major DoT does 13.39 damage), this equates to averaging only 3.57 more damage per hit (or +4.46 damage, for Major DoT).

**What can I expect from a 50% DoT proc?**

This is the middle performer of the bunch and is only available as a Tier 3 Interface Proc. Below is its expected performance.

| X | Prob(X) | E[X] =X*Prob(X) |
| 0 | 50.00% | 0 |
| 1 | 25.00% | 0.2500 |
| 2 | 12.50% | 0.2500 |
| 3 | 6.25% | 0.1875 |
| 4 | 3.125% | 0.1250 |
| 5 | 3.125% | 0.15625 |
| Total | 100% | 0.96875 |

These numbers tell us that on average, each attack will do 0.96875 ticks of damage, which equates to averaging +10.375 and +12.969 more damage for the Minor and Major DoTs, respectively. In this case, doubling our Proc probability nearly tripled out added damage performance.

**What can I expect from a 75% DoT proc?**

This is the top performer of the bunch and is only available as a Tier 3+ Interface Proc. Below is its expected performance.

| X | Prob(X) | E[X] =X*Prob(X) |
| 0 | 25.00% | 0 |
| 1 | 18.75% | 0.1875 |
| 2 | 14.06% | 0.28125 |
| 3 | 10.55% | 0.316406 |
| 4 | 7.91% | 0.316406 |
| 5 | 23.73% | 1.186523 |
| Total | 100% | 2.288086 |

These numbers tell us that on average, each attack will do 2.288 ticks of damage, which equates to averaging +24.505 and +30.632 more damage for the Minor and Major DoTs, respectively. In this case, using a 75% DoT proc instead of a 25% DoT proc has increased the average DoT output by 587% (so nearly 7x damage). Using a 75% DoT proc instead of a 50% DoT proc increased the average DoT output by 136% (more than 2x damage).

**Can you show your work?**

Spoiler

Gotcha! I will show my math later if folks really don't want to believe the numbers I just gave. Until then, be patient and I'll add numbers and formulas if you really need it.

**Edited February 14, 2020 by Bopper**
