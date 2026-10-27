---
title: "Enhancement Diversification and You"
url: "https://forums.homecomingservers.com/topic/9837-enhancement-diversification-and-you/"
source: "forums.homecomingservers.com"
authors: ["daHob"]
date_posted: "2019-09-06"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "defense-resist", "recharge-endurance", "mechanics-formulas"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "f4f17d6e84e2faf1"
---

# Enhancement Diversification and You

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — daHob, 2019-09-06

**What is Enhancement Diversification?**

When CoH originally released it was common for people to 6-slot powers with the same enhancement (most often Damage for attack powers and Defense or Resist Damage for defense powers). For better or worse, the CoH devs didn't like that and relatively quickly implemented Enhancement Diversification which causes diminishing returns on slotting multiples of the same enhancement in a power.

The rule of thumb is: **no more than three of the same enhancement in a power**

**The Numbers**

I wanted to know the actual numbers involved (and am not really interested in installing Pines to plot out builds) but none of the wikis had a simple chart showing the effective numbers. Or at least none that I could find. So, for those that want it, here is how ED affect slotting. I calculated the ED values for level 50 enhancements.

Enhancements are divided into 4 sets, but 2 of them are for Knockback and Interrupt respectively and since I've never had any call to enhance those, I didn't bother to do the math.

**Schedule A**

Schedule A contains: Accuracy, Confusion, Damage, Defense Debuff, Endurance Modification, Endurance Reduction, Fear, Fly, Healing, Hold Duration, Immobilization Duration, Intangibility, Jumping, Recharge Time, Run Speed, Sleep, Slow, Stun, Taunt

A level 50 schedule A enhancement gives a 42.4% bonus

| Number Slotted | 1 | 2 | 3 | 4 | 5 | 6 |
| Total without ED | 42.4 | 84.8 | 127.2 | 169.6 | 212.0 | 254.4 |
| Total with ED | 42.4 | 83.3 | 99.1 | 105.4 | 111.8 | 118.2 |
| Incremental Bonus | 42.4 | 40.9 | 15.8 | 6.4 | 6.4 | 6.4 |

ED Formula: =IF(X<70, X, IF(X<90, 70 + 0.9*(X-70), IF(X<100, 88+0.7*(X-90), 95+0.15*(X-100))))

**Schedule B**

Schedule B contains: Defense Buff, Range Increase, Resist Damage, To Hit Buff, To Hit Debuff

A level 50 schedule B enhancement gives a 25.5% bonus

| Number Slotted | 1 | 2 | 3 | 4 | 5 | 6 |
| Total without ED | 25.5 | 51.0 | 76.5 | 102.0 | 127.5 | 153.0 |
| Total with ED | 25.5 | 50 | 58.5 | 62.3 | 66.1 | 70.0 |
| Incremental Bonus | 25.5 | 24.2 | 8.8 | 3.8 | 3.8 | 3.8 |

ED Formula: =IF(X<40, X, IF(X<50, 40 + 0.9*(X-40), IF(X<60, 49+0.7*(X-50), 56+0.15*(X-60))))

**Conclusion**

I mostly did this for myself, but I thought it might help a few other folks. The formulas are pulled straight from my Excel workbook, so you can copy them  (replacing X with a cell id) for your own purposes.

So, the rule of thumb is pretty accurate for schedule A enhancements, but for something with a large base number you will get a 20% boost going from 3 to 5 slots, which might matter to someone.

Schedule B enhancements get cut very rapidly. For Defense boosts especially, since they often start at such low numbers, 2 slotting them might be sufficient since you are getting such low return on your third slot.

I hope someone finds this useful!
