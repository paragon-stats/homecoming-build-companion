---
title: "A Comprehensive Guide to the Incarnate System"
url: "https://forums.homecomingservers.com/topic/8727-a-comprehensive-guide-to-the-incarnate-system/"
source: "forums.homecomingservers.com"
authors: ["Robotech_Master"]
date_posted: "2019-08-22"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "incarnate", "leveling-content"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "079833aafb75fc21"
---

# A Comprehensive Guide to the Incarnate System

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Robotech_Master, 2019-08-22

**A COMPREHENSIVE GUIDE TO THE INCARNATE SYSTEM**

**or**

**“WHY ARE INCARNATES SUCH SNAPPY DRESSERS?”**

The Incarnate endgame power system is one of the more confusing systems that the Homecoming version of City of Heroes has to offer—and while there are a number of great guides for it out there (most notably "Incarnates Made Easy" by @jimjimjimmeh) it seems to me that none of them do much more than scratch the surface. A number of times in-game, I’ve had to explain the system to new Incarnates in depth, and my experiences there showed me that the newly Incarnated often have a lot of questions where the answers aren’t usually all in one place. So I’m going to try to change that.

This guide will go over how to use the Incarnate powers window, and offer suggestions for how to proceed once you hit level 50. If any other experienced players disagree with my advice (and I’m sure some will), I invite them to offer alternatives in follow-up comments.

And yes, I recognize this guide is very, very long. (It came to 48 pages when I typed the draft in Google Docs, albeit at 18 point font size.) But the Incarnate system is very, very complicated, and I figured it was best to put down everything I knew about it because I can't tell what bit of information might be crucial to someone else. If you want a shorter, basic overview, that guide I linked a couple paragraphs back is a very good one—but this guide is meant to be comprehensive.

(Oh, and in case you’re wondering, the answer to the question posed in the alternate title is, “Because they have so many awesome Threads!”)

**WHAT ARE INCARNATES AND WHY DO YOU WANT THEM?**

In deference to @TheSwamper, who asked those questions in another Incarnate guide thread, here’s a brief explanation of what Incarnates are all about.

The Incarnate system, explained in-game as a way to become a god-like being yourself, is the post-level-50 endgame power and content system that NCSoft implemented in the final days of the live version of City of Heroes. In the lore of the game, you’re assumed to go right on leveling up to outright genuine godhood, in the years or decades after the time period covered by the game ends (you see a “flash-forward” memory of yourself as a god, who the game’s various archvillains are completely unable to damage, as part of the Incarnate introductory arc in Ouroboros), but the game just follows you a smidgen of the way there—but as far as these powers are concerned, a smidgen is quite enough.

As for why you “want” them, well, you’re going to be given them whether you want them or not. You can always ignore these new powers, of course, but I find that they add a little extra zest to playing my level 50s through 45+ content, and are a great way to make an old character seem suddenly fresh and new again in some ways.

And if nothing else, being able to be effectively level 50+1 in regular content is a great reward for the effort you put into getting all the way to 50. So, why not go for it?

The answer to that rhetorical question seems be largely, “Because I don’t understand what the hell it even is.” And that’s what this guide is intended to explain, in detail—as well as the answer to TheSwamper’s third question, “How do I get started earning them?”

**WHY IS THE INCARNATE SYSTEM SO CONFUSING?**

To understand why the Incarnate system is such a god-awful (Incarnate-awful?) mess, you have to know a little of the system’s history.

I think that there are two main reasons the Incarnate system is the way it is. The first is that the system was introduced very late in the life cycle of the live version of the game, just a few issues before it was closed down for good.

City of Heroes has had a history of introducing clunky and overly complex systems early on, then gradually streamlining and simplifying them over time. (Case in point: sidekicking. Ask anyone who was around in the early days about the amount of fiddling around it took to organize the right quantities of people at the right levels to sidekick or exemplar people when sidekicks and exemplars needed one mentor per sidekick. But now, it's all entirely automatic.) The Incarnate system simply didn’t get enough time for the developers to be able to do those sorts of revisions.

The other reason has to do with how the Incarnate system was engineered in the first place. If you look at the write-up for the Incarnate system in live on ParagonWiki, you’ll see that there were some key differences from how the system now works. In particular, back then Incarnate XP and threads could only be ground out via Trials, and there were a number of more grindy ways to unlock slots. The Incarnate system was largely locked behind NCSoft’s "VIP" paywall, as yet another way to try to pry money out of its free-to-play playerbase.

When SCORE made the game completely free to play and made free-by-default most of the things that had required grinding to unlock, it left some holes and rough edges in the Incarnate system—for example, the Empyrean and Astral Merit Vendors who had once resided on Ouroboros had pretty much their entire inventory of unlockable costume parts and other oddments incorporated into the costume designer’s free selection or otherwise made free. With nothing left to sell, those vendors disappeared, replaced by a new one whose main function is to convert between Empyreans and Astrals—and there's nearly nothing else you can actually do with Empyreans and Astrals anymore.

SCORE is a small group of volunteers who have a limited amount of time to devote to working on the game, and don’t have the resources that a commercial game developer like NCSoft had. When it came to removing the paywall, my surmise is that that they saw that redesigning the system from the ground up would take many more man-hours than they had, so they settled for patching over things as best they could. This is why we still have two separate systems of largely incompatible Incarnate crafting components, and two sets of Incarnate merits that actually can’t be used for very much. (More on that later.)

Given the level of manpower SCORE had, and all the other things they had to fix and update, I think that going this route was absolutely the right decision at the time. It’s not exactly the only clunky thing about the revival, after all—just look at the entirely empty Kallisti Wharf, for example. Perhaps someday, after enough other issues are ironed out, the SCORE and Homecoming developers will be able to take another look at Incarnates.

But for now, we have what we have, and I’m going to try to help you make some sense of it.

**UNDERSTANDING THE INCARNATE POWERS WINDOW**

For this next section, you might want to have the game open and the Incarnate window available so you can follow along with me as I describe what you’re going to see.

I would suggest beginning by pasting this macro into your game:

/macro INC "windowtoggle Incarnate"

This will create a button that will open the Incarnate window with one click, instead of having to open the Powers window and click the “Incarnate” link on top of that. It’s a lot more convenient if you’re going in and out of that window all the time—as you will be, once you start working with it.

The Incarnate window has three main tabs: Equip, Create, and Convert.

**EQUIP**

This window shows all of your Incarnate power slots, which if any you’ve unlocked, and what percentage you’ve unlocked of any that haven’t been yet. Clicking on each power slot shows you a list on the right-hand side of any currently-available powers you might have in that category. After you create a new Incarnate power, you do have to come here and slot the new version of it before you have access to it.

There’s a 5-minute cooldown on slotting new powers. Display Name notes below that this cooldown applies to any member of your team being attacked or activating powers. In my experience, that timer may reset after you change zones; I have been able to craft and slot new powers immediately after exiting completed Incarnate trials.

**CREATE**

This tab is one of the least intuitive parts of the whole City of Heroes user interface. On the left side are a list of categories for the different Incarnate powers. If you click on one, it expands to a list of the different types of that power. If you click on one of those types, in the middle you’ll see what looks like a stylized tree diagram with various Christmas tree ornaments on it—one at the bottom, two on the second row up, four on the third row up, and two on the top row again. On the right, you’ll see one or more “recipes”—lists of components that you need to create that particular power.

Drag the bottom of this screen down to make it taller so you have room for more recipes to fit, and click on “Alpha”. Select one of the Alpha powers, and click on the single orb at the bottom of the tree. On the right-hand side, you should see two recipes—one using Shard-based components, the other using Thread-based components. I’ll go into more detail on that in the section on crafting Alphas, later.

Whether you’re talking about Shards or Threads, there are four different rarity levels of Incarnate components: Common, Uncommon, Rare, and Very Rare. Each recipe uses two Common components, then one more component that is a different rarity depending on how high up the tree it is. The one at the very bottom uses three Common components, the two on the next level use two Common and one Uncommon, and so on.

Also, each level above the first uses the power below it as a component. If you’re crafting a Tier 2 Incarnate ability, the Tier 1 ability you already have is one of the ingredients. The Tier 3 ability uses the Tier 2 below it as an ingredient. (So, if you’re planning on a specific Tier 3, you need to craft the Tier 2 on the same side of the tree as it is.) The Tier 4 ability uses any two Tier 3 abilities—so to craft a Tier 4, you actually have to craft your way up to possessing two separate Tier 3s. It doesn’t matter which two; there are six separate Tier 4 recipes representing each possible combination of two Tier 3s.

The recipes are also crafted from this window—when you have all the ingredients, the recipe lights up, and there's a button you can click to create the power. If there are multiple recipes, as for Tier 4 powers, you just scroll down until you see the one that's lit up.

**CONVERT**

This screen allows you to create Incarnate components out of Shards, Threads, or Empyrean Merits, as well as “sidegrade” components to other components. I’ll go over what each section lets you do.

**INCARNATE SHARD**

This is the section for conversion of Incarnate Shards into other items. Common components cost 4 Shards, and Uncommon components cost a Common component plus 8 Shards for a total of 12 Shards.

The only Rare component is a Notice of the Well, and it costs 4 Uncommon components, plus 40 Shards, for a total of 88 Shards—and 12.5 million Inf to boot. And the only Very Rare component, Favor of the Well, requires 2 Notices plus 32 Shards—a whopping 208 Incarnate Shards if you actually bothered to craft one from scratch—but you’ll probably almost never need to do that.

Notices of the Well are given out one per week as rewards for the Weekly Strike Target Task or Strike Force (see the section below about building your Alpha), so there’s no point in spending 88 shards and 12.5 million Inf on one. And while it’s possible that you might want to craft a Favor of the Well, you’ll probably do it with two Notices you got from previous weeks’ WSTs so only have to pay 32 Shards. And maybe by the time you have two Notices, you’d have that many Shards to spend on it. Or you could skip the question entirely by using Thread-based components to make your Tier 4 Alpha.

The next section, “Breakdown,” lets you turn a Shard-based component back into Shards, but you’ll only get a quarter or less of the Shards used to build it, so it doesn’t really seem like the wisest possible choice. Finally, “Upgrade” lets you convert 10 Shards to 10 Threads once every 20 hours, 10 Shards to 5 Threads at any time, a Notice of the Well to 40 Incarnate Threads, or a Favor of the Well into 100 Incarnate Threads. (I’m not really sure how that qualifies as an “Upgrade,” but whatever.)

**INCARNATE THREAD**

Just like the Shard section, the next section of the menu deals with converting Incarnate Threads into Thread-based components. I’ll go into more detail about these in the section discussing crafting powers from them. For now, it’s enough to know that these are the components you get from specifically Incarnate content—Incarnate Trials, the Apex and Tin Mage task forces, Dark Astoria missions, and so on. Also, from Vet Levels 1 to 11, you will get 8 installments of 120 Threads each time you level up (skipping the times you get Empyrean Merits for leveling; see below).

The Common, Uncommon, Rare, and Very Rare sections work about the same as in Shards. Common components cost 20 Threads, Uncommon cost 60, Rare cost 4 Uncommon, 100 Threads (so 340 Threads in all) plus 25 million Inf, and Very Rare cost 4 Rare components (1360 Threads) plus 100 million Inf.

But once again, you’re unlikely ever to craft a Rare or Very Rare component from other components. This is partly because you have a decent chance of getting some of these components as rewards for doing Incarnate content, but mainly because there’s a simpler way to make them—and I’ll be covering that in the next section.

The final option under Incarnate Thread is “Incarnate XP,” which will let you exchange a single Thread for 50,000 Physical or Psychic Incarnate XP, used for unlocking your slots. This might have been a good deal back in Live, where you couldn’t easily get Incarnate XP outside of Trials, but Homecoming lets you get it along with regular XP once you hit 50. And 50,000 Incarnate XP is about what you’d get from just three or four good-sized mob spawns. So, it really doesn’t seem worth it now. Save your Threads and grind out the levels; it really won’t take all that long.

**ASTRAL MERITS, EMPYREAN MERITS**

These two sections deal with the same thing: converting Astral and Empyrean Merits into other things. Astral Merits are the Merits awarded for completing individual segments of Incarnate Trials, and each one can be converted to 4 Threads. (Also, 5 Astral Merits can be converted to 1 Empyrean Merit, but that’s done through the vendor Luna on Ouroboros, not through this control panel.)

Empyrean Merits have more conversion options, though. You can convert one of them to 20 Threads—or you can convert 8 of them to a Rare component or 30 to a Very Rare component.

Note that the game will actually give you Empyrean Merits as you level up, every 3 Vet levels, at the same time as it awards you one of the old Veteran Rewards badges from back in the day. Every 3 levels, from Vet level 3 to 24, you’ll get 20 Empyrean Merits when you level up. From 27 to 48, it’s 15 Merits, 51 to 69 is 10 Merits, and 72 to 99 is 5 Merits every three levels.

So, by the time you hit Vet Level 9, you’d have enough Empyreans to craft two Very Rare components, or one Very Rare and three Rare with a little left over.

You can also get 4 Empyrean Merits for a successful Hamidon Raid, or obtain them from various other Incarnate content.

As I mentioned above, Astral and Empyrean Merits used to have merit vendors on Ouroboros, where you could trade Astrals and Empyreans for costume parts, auras, unlocks, Inspirations, and other rewards. (The complete list is here, if you’re curious. How many of those costume parts and auras do you recognize from the costume creator now?) But all the costume stuff was rolled into the freely unlocked stuff from the costume creator, and the Inspirations were changed to sell for Threads rather than Merits. (And the unlocks you could buy were largely unlocked for free with Homecoming!)

In the end, there’s now only one Empyrean/Astral vendor left on Ouroboros—Luna, just to the right of the big building with the shards in it. She’ll convert Astral and Empyrean Merits back and forth, and sell super Inspirations at 10 Threads each and up. You can also convert Astral and Empyrean Merits to Reward Merits at any Reward Merit Vendor, or convert 50 Empyreans to 1 Transcendent Merit and back to make it easier to email them to your other characters.

I’d strongly not recommend converting Empyreans to Reward Merits, by the way. Reward Merits are easy to come by, but Empyreans are much rarer, and you can’t convert Reward merits back into Empyreans. If you reach the point where you don’t need any more Empyreans on that character, you can always change them to Transcendents and give your other characters an Incarnate head start with them.

**COMMON, UNCOMMON, RARE, VERY RARE**

The final four sections are named for the rarity levels of the Incarnate components, and they include options to Sidegrade, Downgrade, or Breakdown Thread-based components of those rarities. Sidegrading costs a number of Threads to change a component into another component of the same rarity—useful if you have an extra of something but need something else to make your chosen recipe. Downgrading turns a component in one of the next rarity level down—but you don’t get any Threads back for it. As with the Shard option, Breakdown dismantles a component into a number of Threads—but you only get a fraction of the cost to create that component back.

**THE INCARNATE PROCESS**

The process of building Incarnate powers is pretty simple once you understand it, but a little hard to understand at first when the user interface is so confusing. You get Shards and Threads from mob drops, Threads and Empyreans from Vet level-ups, Astrals, Empyreans, and components from events, and you use all this stuff to build your way up each power tree, twice—you make your first Tier 3 to use, then your second Tier 3 to convert into a Tier 4. It just takes a while to grind out enough components to make everything—which process I’ll go over in a later section.

It’s worth noting that, unlike the powers you get when you level up, you aren’t locked into the first Incarnate power you build. In fact, by the time you reach Vet level 99 and stop getting any further Empyrean Merits from leveling, you’ll have gotten enough drops to have built several powers for each slot if you wanted.

For example, my Tanker and Brute have both Resilient and Spiritual Alphas at Tier 4, and can swap them out depending on whether they want to be tougher or recharge faster in the mission they’re doing. And all my characters have Incandescence Destiny because I find its fast-recharge Assemble-the-Team effect really useful, but some also have one of the other Destinies for times when they might want to buff people rather than teleport them.

So, don’t be afraid to experiment with multiple powers, especially if you might need to use more than one in different situations.

**THE INCARNATE POWERS**

I’m going to run down the various slots and powers now, with commentary and recommendation on each. I’m going to cheat a little, and instead of giving the statistics for each one, I’ll just point you at the Paragonwiki pages which have nicely organized charts. As far as I know, even though a lot of stuff about the Incarnate system has changed from live, the abilities of the Incarnate powers should still be mostly identical. There’s no need for me to waste time and effort reinventing the wheel.

The thing you should consider in picking a particular option for any of the Incarnate powers is not simply what the Tier 1 version of the power provides—look at both Tier 4 versions of it, too. It’s possible that even if the Tier 1 isn’t useful to you, the Tier 4 might just include the specific other things that you need.

In addition to reading the wiki pages, you can also see what any given power does by mousing over it in the Incarnate powers window.

**ALPHA SLOT ABILITIES**

The Alpha slot is the only Incarnate slot that you can build from either Shards or Threads, because it was intended to be the one slot freely available to people who didn't pay to unlock the full Thread-based Incarnate system. As I'll explain later, I recommend building it from Shards as much as possible and saving the Threads for other slots.

The Alpha slot is an always-on power that functions, essentially, as an extra Enhancement tacked onto to all of your powers that can use that particular category of Enhancement, and partly ignoring the effects of Enhancement Diversification that cap Enhancement of your powers at set limits.

Any and all of your powers that can slot that category of Enhancement benefit from this extra Enhancement, while powers that can’t do not. An Alpha that boosts Range doesn’t just make your blasts shoot farther, it also lets you teleport farther if you have Teleport, and recall friends from farther away with Recall Friend. (Maybe even from all the way across Independence Port!) But it doesn’t do anything for your melee attacks, unless they can also be slotted with Range for some reason. Also, **it will not affect powers that do not take Enhancements**, such as temp powers, prestige powers, other Incarnate powers, etc.

These powers each enhance one attribute at Tier 1—Damage, Endurance Modification, Healing, etc.—but at Tier 4, they can either enhance three attributes extremely well, or six attributes moderately well. Any given attribute only seems to show up in two powers—one as the Tier 1 ability, and another as a secondary ability from later Tiers.

For example, Agility boosts Endurance Modification at Tier 1, and Recharge Rate in later Tiers. Spiritual boosts Recharge Rate in Tier 1. Musculature boosts Endurance Modification in later tiers. And so on.

A Controller who is considering taking the Intuition Alpha because of its boost to Hold Duration might also want to look at Nerve, which primarily boosts Accuracy but also boosts Hold Duration and Confusion later on.

If you’re picking an Alpha, try to find the Alpha that enhances the most things you do use, and the fewest things you don’t. My Tanker took Resilient Radial Paragon, because it boosted Damage Resistance and Taunt. It also boosted a few other things, which I don't much use, but it was worth having for the two things that it did.

The Alpha Slot also offers a Level Shift with Tier 3 and 4 versions of the power, which makes you effectively one level higher than your “current” level. This Level Shift applies to any City of Heroes content when exemplared to level 45 or higher. I’ll have more to say about that in the section on advice for new Incarnates.

**JUDGEMENT SLOT ABILITIES**

Judgement is, not to put too fine a point on it, a nuke. It’s a high-damage, high-number-of-targets attack that can go off every couple of minutes. It can provide a nifty complement to the rest of your powers, particularly in regard to softening up huge crowds of adversaries.

But which one do you want? There are a number of elements to consider. For one thing, there’s thematic appropriateness. My Fire/Fire Tanker and Brute both took Pyronic, because shooting a fireball was perfectly in keeping with their other talents for fire. Ditto my Dark/Dark Controller with Void, and my Empathy/Electric Defender with Ion.

On the other hand, there are other things to look at as well. For example, there’s the type of damage. Do you want a most-often-resisted Smashing or Lethal damage, like Mighty or Vorpal have? Or one of the less commonly resisted elemental attacks, like all the rest?

And then there’s the pattern of the attacks. Mighty and Void are both PBAoE attacks, Cryonic and Vorpal are cones, Pyronic is a ranged AoE, and Ion is a chained lightning attack that jumps from target to nearby target until it runs out or hits its target limit. So, if your target is a melee class and often surrounded by mobs, you might prefer to consider one of the PBAoEs rather than a cone. My Fire/Kinetic Controller, who often has to run into the middle of enemies to Fulcrum Shift them, went with Mighty, which not only does some decent damage but can be taken with a knock-up effect that makes it harder for the enemies to damage him before he can get out of range.

Or, if you’re a min-maxer, Ion is probably the one you want regardless. It’s got high damage, hits an extremely high number of targets at the higher tiers, and you don’t particularly need to worry about where you aim it as long as you shoot it at any mob who’s near any other mobs—it chain-jumps from one to the next until it hits its max number of targets or runs out of nearby mobs. A lot of people complain that it’s overpowered; it’s certainly by far the most common Judgement you see in use. (If you see the words “Ion Judgement Jump” appearing over a lot of enemies’ heads at once, someone just used it.)

Judgement is a click power. When you equip it on the Incarnate Equip tab, it creates a new power in your Powers window, which you can then drag to your preferred spot on your control panel.

**INTERFACE SLOT ABILITIES**

The Interface power adds an inherent proc to all damaging attacks, for debuffs, extra damage, or other effects. Most Interface power trees have two potential effects you can choose to concentrate on, and the two Tier 4 power options each do a lot of one effect and a little of the other. The debuffs can stack on a single mob up to 4 times, whether from one or multiple characters with the same Interface.

My Fire/Fire Tanker and Brute went with Reactive, because a little extra fire damage and damage resistance debuffing was in keeping with their theme—they burn stuff. But see what works better for you.

Note that a lot of the debuff effects you get are fairly small—for example, Reactive only offers a 2.5% Damage Resistance debuff for ten seconds, which even stacked the maximum of 4 times only amounts to 10%—so going with Radial Flawless Interface that has the higher chance for the damage-over-time proc might be more effective at helping to kill things all in all.

I've heard reports that this slot may be bugged, and the 75% proc is the only one that actually goes off. I'm not sure of the truth of those, however.

**LORE SLOT ABILITIES**

Lore powers are Mastermind-style summonable pets that will stay around for 3 to 5 minutes, and have a 15-minute recharge time (meaning it’ll be at least 10 minutes after they go away that you can summon them again). There is a wide variety of pets available, encompassing all kinds of damage and all kinds of secondary effects. Unlike temp power pets, Lore pets will change zones with you.

Lore pets use the same control panel as Mastermind pets, meaning you can set them to Aggressive, Defensive, or Passive modes, and order them to attack your target. Unlike Mastermind pets, you can't drag Inspirations onto them, though.

In practical use, Lore pets are handy companions for adventuring, especially if you solo a lot. They can make clearing "defeat all" missions a little easier, as well as giving a boost against archvillains and monsters. They also figure into Hamidon raids; as soon as all the mitos are down, the raid leader will call for everyone to pop Lores and set them to Aggressive. And there is a specific time within the BAF Trial they should be saved for.

The left (Core) side of the Lore power tree focuses on damage, with Minion, Lieutenant, and Boss pets that have primarily damaging powers. The right (Radial) side of the tree features a support pet. The right-side tier 4 power’s support Lieutenant has no damaging attacks, but is also intangible so it can’t be attacked by enemies.

I will usually take the Core (damage) side on support characters like Controllers or Defenders, and those (like Tankers) who are tough enough not to need any extra support but could benefit from damaging things faster. But my characters who are chiefly damaging and a little squishier, like Brutes, Blasters, etc. will go with the Radial (support) side for a little extra healing and buffing power.

As for just which Lore pet to get, much like with Judgement it comes down to a question of what’s more thematically appropriate vs. what’s more powerful. If you’re a Demons Mastermind and want to take the Demons Lore set to match, then go right ahead. If you’re looking for the most useful abilities, you might want to check out this list that @CR Miss on the Guides forums has been working on.

Cimerorans have a reputation for having the highest single-target damage attacks, though they use the most-commonly-resisted Lethal damage type. The Immunes Surgeon from the support side of the tree has some nice healing abilities that can be helpful if you’re a Fire Brute doing an AE farm. Storms and Polar Lights have some decent AoE. Longbow has a giant robot with some good AoE and a -regen attack. (Also, Longbow has a giant frigging robot.) I’ve heard good things about Banished Pantheon, too.

As with any other Incarnate power, you need not limit yourself to only one. You’ll get enough components, Threads, and Merits over time that you can experiment with multiple different Lore sets, or you can pick different ones for different characters and see how you like them all.

At Tier 3 and 4, Lore applies an Incarnate Level Shift—a +1 to your level that only applies when doing Incarnate content, such as the Trials or Dark Astoria missions (though oddly enough, not Apex/Tin Mage for some reason). I’ll have more to say about in the section on advice for new Incarnates.

**DESTINY SLOT ABILITIES**

Destiny powers are click-to-cast AoE buff auras that mostly emanate from the caster to cover a wide radius. They have a variety of useful effects that are at their strongest in the first thirty seconds after being cast, then gradually fade over time. Higher levels of these buffs may increase radius or buff duration, or add additional buffs or other effects.

Most of these are pretty straightforward buffs. Ageless boosts Recharge Rate and Endurance Reduction, sort of like a wide-area Speed Boost but without the bump to running speeds. Barrier gives everyone bubbles that boost Defense and Damage Resistance, and a couple of the higher-tier abilities are noteworthy for rezzing either one or two fallen allies—pretty handy for a power that’s up every couple of minutes.

Clarion provides hugely powerful mez protection, and is often useful in cases where powerful mezzes are things to worry about—avoiding getting stunned by Romulus’s Nictus self-rez in the Imperious Task Force, for example, or in Rikti Mothership raids to protect against Rikti Mages, or in Hamidon raids to guard against mito-mezzes. Rebirth is a wide-area mega-heal.

But I think one of the most generally useful Destiny powers, both as an Incarnate power and in the game in general, is Incandescence. Not to be confused with the Incandescent Strike Kheldian ability, the Incandescence Destiny is a whole-league version of Assemble the Team that fires off instantaneously and is available every two minutes. In addition to teleporting everyone to the target location, it also knocks back and stuns nearby foes, improves the effectiveness of heal powers, and has various other buffs at higher levels.

This power is particularly useful in certain Incarnate Trials (such as in pulling the whole league out of the green disentigration beam in the Keyes Reactor Trial), but it’s extremely handy in general in pretty much any teaming or league situation. In Hamidon raids it’s useful for transporting everyone to the giant monster “walls” to take them down until Hamidon spawns, and gathering them all at the “safety rock” afterward to prepare to go in. In Rikti Mothership raids it can be useful in the event of the league taking significant casualties—instead of rezzing everyone one by one, have them all go to the hospital and fire off Incandescence a few seconds later. In the Justin Augustine Task Force, huge amounts of travel time can be saved by one team member with Incandescence going on ahead to where the next hunt will be while everyone else works on the current one, then porting them there when it’s done.

And, of course, it’s even better than Assemble the Team for stealthing to the end of missions and porting everyone else there—there’s no excessively long casting time during which enemies can interrupt you, and you don’t have to wait a half hour for it to recharge. And since it’s up so often, you can use it in any circumstance (for example, fixing team splits, or porting everyone else to the mission door if you get there first) rather than saving it for when you might really need it. For these reasons, I suggest that everyone should craft at least the 60-Thread Tier 1 version of Incandescence even if they mainly use some other Destiny—it’s just super-useful having that on-demand Assemble the Team effect.

Plus, thanks to its knockback and stuns, Incandescence can also be used offensively. You just pick up your entire team and throw it into a group of enemies, and the enemies get knocked back and stunned while your team gets buffed. You want to be careful how you do this, though—since Incandescence can’t be slotted, there’s no way to turn its Knockback into Knockdown. So you want to try to position the teleport target to knock enemies into a corner if possible, or against a wall—don’t scatter them every which way and make it harder for your Tanker to get aggro control.

If you’re going to do this on a team, you should definitely let people know at the start that you’re inclined to use Incandescence offensively, and if they don’t want to be yanked around (and particularly if they're squishy) they should make sure that “prompt team teleport” is turned on. It might also be useful to warn them that an Incandescence is incoming before you cast it. And, of course, if people react badly to the idea, just don’t do it.

At Tier 3 and 4, Destiny applies an Incarnate Level Shift—a +1 to your level that only applies when doing Incarnate content, such as the Trials or Dark Astoria missions (though oddly enough, not Apex/Tin Mage for some reason). I’ll have more to say about in the section on advice for new Incarnates.

**HYBRID SLOT ABILITIES **

Finally, Hybrid powers are two-in-one—they include an inherent ability from being slotted, as well as 0-Endurance timed toggles that don’t shut off if you get mezzed. The toggles will run for up to two minutes, then take two minutes to recharge. They can buff damage, regeneration, control, or the rest of the team, with various differentiated abilities as you climb the Tier tree.

The Hybrid abilities that offer extra damage to controlled enemies include some interesting pop-ups over affected enemies. If you’re wondering why you saw “WAYLAY!” over an enemy’s head, that’s why.

Again, take whichever one of these has good synergy with the other powers you already have. For my Tanker and Brute that was Melee, given its buffs to regeneration, Resist Damage, and Defense, and the taunt aura effect of the Radial branch of the tree. My controllers took the Control power, and my Defender took Support.

Now that I’ve gone over the Incarnate control panel and slots, the next section of this guide will cover my advice to new players just getting into Incarnate abilities. Again, if you experienced Incarnates disagree with any of this, please discuss it in the comments—having different points of view available will help those new to the system decide for themselves.

**FIRST STEPS FOR NEW INCARNATES**

Welcome to level 50! You’re about to take your first steps into a much larger world. Here’s my advice on the best way to go about building up your Incarnate powers. You may choose to go about it some other way, of course; there are plenty of good ways to do it—but understanding the way I recommend should help you to work out for yourself which other way you might prefer.

**STEP ONE: CHECK YOUR BUILD**

Your Incarnate powers, when you have them, will sit on top of your normal build and make an already good build even better—so to start, make sure you already have a good build. If you leveled to 50 without any goals in mind, this is a good time to look back at your character, decide what you want your build to do, and respec into it.

The best way to plan out a respec, in my opinion, is with the new Mids' Reborn: Hero Designer utility. The most recent version includes the new Sentinel class, new Enhancement sets, and all the new and newly-proliferated power sets for all the character classes. It will let you play with builds, and see what slotting particular Enhancement sets into them can do for you. I personally like to spec out my builds with it beforehand and build my character into them on the way up, but not everyone does it that way.

So, go ahead—make a good build and respec into it. (Remember to take a screenshot of your power trays before you do the respec, for reference when you’re rebuilding your trays afterward.) Slot ATOs or Winter Super Pack Enhancements and catalyze them; slot purples; slot Overwhelming Force into a damage aura or other AoE power…go wild! You’re level 50, you deserve it.

If you don’t quite have the money for that yet, you can still spec out your planned build and slot cheaper sets or common IOs until you can afford the more expensive ones. But you’re level 50 now, you make the most Inf per mob defeat of any level in the game, and you can no longer disable earning Influence for double XP—so you will have it sooner or later. (And that’s leaving aside methods of specifically grinding for loot, such as AE farming or Enhancement conversion.) And thanks to the way the Homecoming market works now, even the most expensive purples are still much more affordable than they ever were under Live. So, you’ve got options, if you care to use them.

**STEP TWO: BUILD YOUR ALPHA**

If you looked at that Paragonwiki page I recommended earlier, you will have noticed several ways of unlocking the Incarnate system, including doing Mender Ramiel’s arc, buying Incarnate XP with Threads, or outright buying the unlock from a vendor with Astral Merits. Which of those would I recommend?

Well…none of them, actually. One of the changes Homecoming implemented was automatically unlocking the Incarnate system once you train up to level 50—no Mender Ramiel arc completion required. Once you train, you’ll immediately start earning Incarnate XP—and not just from special Incarnate content, either, but from ordinary adventuring.

(Note that the Mender Ramiel arc will unlock your Alpha slot instantly upon completion, as well as awarding you an Incarnate Shard. If you want to unlock the slot right away, working through the arc would probably be faster than grinding out the XP. However, one of the missions involves a pair of elite bosses who can be tough to solo if your character isn't from a good damage-dealing class. You may want to stock up on Shivans and P2W store pet summons beforehand, or invite a friend or two to tag along—or if there's no other option, use your autocomplete to clear the mission.)

The first batch of Incarnate XP will go toward unlocking your Alpha slot, and it unlocks pretty quickly—do a Task Force of any reasonable length, or even just get in a Rikti Mothership raid, and you’ll almost certainly have it unlocked by the time you’re done. And the other slots will follow that—a little more slowly, but keep adventuring and they’ll be done before you know it.

The vendor who sold Alpha unlocks with Astral Merits no longer exists. You can still buy Incarnate XP with Threads (as I discussed in the description of the Incarnate power panel options), but really, I don’t recommend it. Unlocking all the slots doesn’t take long at all, so just grind a little more, and keep more Threads to spend on your power picks.

**SHARD-BASED COMPONENTS: HOW TO GET THEM**

While you’re unlocking your Alpha, you should also be thinking about building it. As a reminder, **Alpha Slot powers can be built with either Shards or Threads.** This is one of the more confusing aspects of the Incarnate power window, because the default window size is just small enough to show only one recipe and the scrollbar on the right indicating there are more recipes below can be very hard to notice. But both sets of recipes are there; you just have to scroll down to get to the Thread-based ones.

You can, if you choose, apply the methods of gaining Thread-based stuff that I’ll cover in a later section for the other Incarnate power slots, and build the Alpha out of them, too. But I would tend to recommend using Shards as much as possible for the Alpha.

You’re going to get Shard drops anyway, and they’re honestly not good for much other than the Alpha. You can convert 10 of them at a time into 10 Threads once a day, and 10 of them into 5 Threads at any time, but there’s not a lot else they’re really good for. So you might as well build as much of your Alpha with them as possible, and save your Threads for the other six slots. If you want to build out all your slots as fast as possible, that would be the best use of your resources.

As for how you get Shards, you’ll get one if you do Mender Ramiel’s arc—it’s still available even if you no longer need to do it to unlock Incarnate abilities—and you’ll get them as occasional random drops during your other adventuring—along with Incarnate Threads. So, the main thing you need to do is any content you want to, and the drops will come.

(Since you have a small chance of a drop with each enemy you defeat, things where you defeat large numbers of enemies quickly are particularly good for getting large quantities of Shards and Threads quickly: AE farms, Mothership Raids, etc. Also, monsters and AVs seem to have a higher than average chance of dropping Shards and Threads, so if you get in on the nightly triple Hamidon raids where everything you fight is classed as a monster, you'll probably end up with a handful of each in a very short time. Note that enemies must con at least white (that is, be the same level as you) to have a chance of dropping Incarnate stuff. So, doing missions at -1 or +0 (once you've gotten your first Level Shift) full of blues and greens won't be all that helpful for building out your Incarnate powers.)

You might specifically want to do Rikti Mothership Raids to buy the Gr’ai Matter component at the Vanguard crafting table, and also do the Level 50 Task/Strike Forces—Imperious, Lady Grey, Miss Liberty, Lord Recluse—because these Task Forces give you the choice of a Shard-based Incarnate component on completion. Every component you get means that you are spending 4 fewer dropped Shards on what you need to craft the power—and you’ll have the chance of getting Shards from drops while doing them. You can only choose the component reward once every 18 hours, but you could certainly do the Task Forces once a day, and a single MSR could buy you several days’ worth of Gr’ai Matter. Apart from that, you can grind for Shard (and Thread) drops the same way you’d grind for XP and Inf, if you do or have a friend who does AE fire farms.

**LEVEL SHIFTING AND YOU**

Your first big goal should be to get your Alpha to at least Tier 3, because Tier 3 Alpha is where you get your first Incarnate Level Shift. The Level Shift is an odd little benefit of the Incarnate system, and it effectively makes your character one level higher than the game treats them. When you’re level “50+1,” level 50 mobs will be blue to you—but if you’re at +0 notoriety, the game will still give you those blue level 50 mobs in your missions, not white level 51s.

You’ll keep this Level Shift as long as you aren’t exemplared below level 45, and this first one from the Alpha will apply to all content in the game that you can play at those levels. The Level Shifts from Lore and Destiny will apply to special Incarnate content only, such as the Incarnate Trials and Dark Astoria zone and missions (though oddly enough, not the Apex and Tin Mage Task Forces for some reason). I’ll touch on those again in the section on Thread-based powers.

**WEEKLY STRIKE TARGET**

And then there’s the Weekly Strike Target TF or SF, a rotating selection of Task and Strike forces which grants double Reward Merits but also awards a huge XP bonus if you’re not an Incarnate yet, or a Notice of the Well Shard component if you are level fifty with your Alpha slot unlocked. You have to have your Alpha unlocked to receive a Notice.

You can only get one Strike Target reward per week, across any of the targets, so if you know you’re just about to hit 50, you might want to hold off on doing it until you can get your Alpha slot unlocked before you start it.

The Notice of the Well that the WST awards you is a crucial component to crafting your Tier 3 Alpha—and it’s expensive enough to craft that you really want to make sure you get all the free ones you can. And another reason I suggest being sure to craft the Alpha with Shards is that you’re going to get that Notice of the Well anyway if you do the Weekly for the double Merit reward, so why not be able to use it?

(You can break the Notice of the Well down into 40 Threads, and you probably should do that for future Notices once you’re finished building all the Alphas you want. But it’ll be a lot more efficient to build your T3 Alphas with them first.)

**THE TIER 4 ALPHA: SHARDS VS THREADS**

When I recommended using Shards to build your Alpha, I did make an exception for Tier 4. That’s because for Tier 4 you need a Favor of the Well, which takes two Notices and 32 Shards to make. Now, if you’ve been playing at level 50 for a while before getting into this Incarnate thing, you might very well have multiple Notices of the Well and bunches of Shards sitting around, but if you haven’t, you’re only going to be able to get one Notice per week going forward—so do you really want to wait two more weeks to craft your Tier 4? I don’t think so.

Fortunately, crafting a Tier 4 Alpha with Thread-based components is just as easy for the Alpha as it is for the other powers I’ll discuss. So you can just do that and call it good.

(By the same token, if you’re ready to craft the second Tier 3 you’ll need for that Tier 4 before you’re eligible to get another Notice from a WST, you may want to do that with Threads, too.)

**STEP THREE: BUILD LORE, DESTINY, AND THE OTHER THREAD-BASED SLOTS**

You don’t actually have to wait until you’ve built your Alpha to start on your Thread-based slots; you can work on both at the same time if and as you get enough Shards and Threads for them. But using Shards to build the Alpha means you can devote more Threads to the other slots, and you should certainly do so if you can.

If you like, you can actually get started before you’ve even got the slots unlocked. You can craft Incarnate powers as soon as you have the components for them; you just can’t slot them until the slots are unlocked. So if you’re doing a lot of Incarnate Trials early on while you’re still unlocking the slots, you might want to make note of what components you’re going to need, so you can pick them from the reward rolls at the end of the Trials and work toward having the right ones to make the powers you want. And if you want to build them early so you're ready to drop them in as soon as they open up, that will be just fine.

I would recommend that, apart from the Alpha, the first two Incarnate slots you should work on are Lore and Destiny. The reason is, those slots provide the Incarnate Level Shifts at Tier 3, making you that much more effective in Incarnate Trials (which I’ll discuss in a moment). Once you have all three, you’ll be at level 53, which means you’ll be on a more even footing with the level 54 mobs in most Incarnate Trials and other Incarnate content. That means you’ll be a more effective fighter in those trials, and those trials will be more likely to be successful.

After you get those Level Shifts, you can build out the remaining powers in whatever order you like. You’ll probably want to get them all to Tier 3 before you start moving any up to Tier 4, though.

**THREAD-BASED COMPONENTS: HOW TO GET THEM**

There are three major sources of Thread-based components: Incarnate missions, such as those in Dark Astoria; Incarnate Task Forces, like Apex or Tin Mage; and Incarnate Trials, like Behavioral Adjustment Facility or Lambda Complex.

**INCARNATE MISSIONS**

Incarnate Missions are probably the simplest way for solo players to get their hands on Shard-based components. They are awarded at the end of Incarnate story arcs, including when the arcs are replayed via Ouroboros. As this guide by @ElPuerco indicates , the Dark Astoria introductory arc can be soloed at the lowest difficulty in about fifteen minutes, thus providing several components over the course of one hour of play. If you don’t enjoy having to depend on a large team of people not to screw things up for everybody else, and are of a class that solos easily, this might be the best course for you—especially after you’ve gotten your Incarnate level shifts and are at +3 and can do these missions at a Notoriety of -1, so you’ll be 3 or 4 levels higher than all the enemies.

If you haven’t done this arc yet, and can gather a team of other people who also haven’t done it, you’ll be able to simultaneously complete the first few missions in the arc, then do the final mission once for each team member so each of you on the team gets as many components as there are team members who have the arc available. In Ouroboros, it runs Task Force style so there’s only one chance to run the final mission for everyone.

**INCARNATE TASK FORCES**

The next step up from those missions is doing the Incarnate Task Forces, of which there are only two—Apex and Tin Mage. These run similarly to all the other Task Forces, being primarily missions where you go places, click glowies, and defeat things. At the end of each of them, you’re awarded a choice of Incarnate Thread-based component. Also, if you earn the optional objective badges and “Master Of” badge for each, those should come with extra components, too.

Unlike previous Task Forces, these do have specific Incarnate requirements—you have to have your Alpha unlocked and slotted in order to be able to participate meaningfully. If you don’t, you’re penalized 4 levels, meaning you’re going to be effectively level 46 while doing a 50+ Task Force, and will probably be dead weight at best. So if you don’t have your Alpha yet, you might want to focus on those Shard-based components I discuss above until you have at least the first Tier of it.

**INCARNATE TRIALS**

The main way of getting Incarnate components for most will be the series of Incarnate Trials that players get together and run. These trials usually call for leagues of 16 to 24 players, and take place over the course of a single mission in which players must complete several sets of challenges successfully in order to finish and win a Thread-based component. While going into great detail about these is beyond the scope of this guide, I can at least cover the basics of getting started in them.

Incarnate Trials disable most of your temp or Day Job powers, though they don’t disable Accolade Powers. Interestingly enough, they also don’t disable many of the temp powers you get from Super Packs. You might lose your Resuscitator or Revive Ally powers, but any charge of the Super Pack “Restore” power you get will be right there for you to use at need. (So you may want to save those along and not use them in regular content.)

Incarnate Trials usually form up in places where both Heroes and Villains can mingle, since both are eligible to participate in said trials. The most popular spot seems to be Pocket D, though you’ll also see some form up in Ouroboros. If you want to get in on such a trial, you might want to hang out in one of those places while you wait, and also watch the LFG channel just in case a trial starts forming up in another instance of the zone you’re in.

Most pre-Incarnate Task Forces and Trials offer few challenges more stringent than going places, defeating bunches of enemies, and clicking on glowies. (You don’t even have to click on them simultaneously anymore, since Homecoming did away with that requirement.) However, most Incarnate Trials are considerably more nuanced—and more challenging. Goals vary by the individual Trial, but can include things like killing more than one archvillain simultaneously, killing enemies next to an archvillain to render it vulnerable, dragging an archvillain to a particular spot so other players can use temp powers on something there, and so on.

At the simplest end of the trial spectrum are the Lambda Complex (LAM), Behavioral Adjustment Facility (BAF), and the (slightly more complicated) Keyes Reactor Trial. These are also the trials you see run the most often (to the chagrin of those who prefer some of the other, more complex ones).

LAM is pretty basic—you just collect two sets of temp powers, then use them in a fight against the AV Marauder. It’s basic enough that a lot of people are bored of it, which is probably why it doesn’t get run as often as some of the other simple ones.

(I would advise to stay away from “speed LAMs,” which is where you only bother collecting one set of the temp powers and disregard the other. It can be faster, but it can also be chancier because without the time boost from getting the second temps, you can run out of time to get enough of the first—and without using the second temp power, you can be overwhelmed by adds if you don’t kill Marauder fast enough. Maybe you’ll want to do things that way when you’re more experienced, but it’s just too easy for those to fail when inexperienced people are involved, resulting in a lot of frustration for all concerned.)

BAF is probably the one that gets run more than any other, because it’s simple and short but also has some nice challenges (including defeating two different AVs twice each). It’s generally pretty hard to fail a BAF. If you’re just getting into Incarnate Trials, you should probably get your feet wet with these.

Keyes is more complex, and has more of a requirement that its leaders know what they’re doing. There are a few aspects of the trial that are counterintuitive and require explanation (like the need not to damage the AV, Antimatter, until a certain point), and a few people who don’t know what they’re doing during the final AV fight can easily torpedo the trial for everybody.

It’s worth noting that the Praetorian AVs from these trials do count toward the Dimensional Warder badge. If you do Maria Jenkins’s arc and then a BAF and a Keyes to get Siege and Antimatter, you will have that badge without having to do Tina McIntyre’s arc as well.

Before doing these or any other Incarnate Trial, I would recommend that you review the write-up on ParagonWiki about the trial in question—but also pay attention to the trial leader. Are they willing to explain to newbies how this particular trial works (preferably while the team is forming up ahead of the event itself)? Do they seem to have a plan in mind for how to handle it? If not, you might be best off to bow out, at least until you have more experience with the trial yourself. My first Homecoming experience with the Keyes came from a so-called leader who had basically no instructions or guidance for the rest of the league, and just trusted everyone to know what to do already and work it out for themselves. It was such a disaster that it put me off trying another one for weeks.

But if the leader does have it together, and is issuing orders, and the more experienced players don't seem to have any problem with them, you should follow those orders to the best of your abilities even if they don't seem to make any sense. There may be things they just haven't had time to explain yet.

And once in a very great while, you might just run across a leader who has it so together that they make even the most complex trials seem simple without even trying. If you run across any amazing Incarnate Trial leaders like that, I recommend that you friend them, global-friend them if they’ll let you, and see if they have a global channel where they organize their trials. Good leaders are so rare, you definitely shouldn’t let them get away.

You’ll get a choice of Thread-based component on successful completion of a trial—hopefully a Very Rare, possibly a Rare, more likely an Uncommon, or probably a Common. When you’re given the choice, you may want to pop up your Incarnate powers window and see which components you need to make the next power you want so you’ll know which one you should choose. (Unless it’s a Common, in which case you can pick pretty much any at random because you’ll probably need it sooner or later.) You can exit the trial and the window will stay open if you haven't yet made your choice.

You will also get Astral Merits from completing specific challenges within Incarnate Trials, and an Empyrean merit at the end. You can also convert 5 Astral Merits into 1 Empyrean Merit at Luna on Ouroboros (which is really about the only useful thing you can do with Astral Merits).

Earning the optional badges and Master Of from Incarnate Trials will earn you additional components and Astral Merits, which is nice if you're just starting out.

Also, you shouldn't just limit yourself to those three. The other Incarnate Trials do get run from time to time, and are often very interesting—especially if you have one of those aforementioned amazingly good leaders at the helm. So, if you see one being announced on LFG, don't be afraid to jump in. The final Trial, Magisterium, does require that your character has their Lore and Destiny slots unlocked, but also offers a higher-than-usual chance of Rare or Very Rare components at the end.

**FURTHER INCARNATE PROGRESSION**

However you get components—be it missions, Task Forces, or Trials—the more of them you get, the more quickly you’re going to level up your Incarnate powers. I would strongly recommend saving the Threads and Empyrean Merits you get for leveling against the proverbial rainy day, and scooping as many components as you can from game content instead. Every Rare component you get saves you 8 Empyreans, and every Very Rare saves you 30. And as rarely as those two drop, you’ll have to do a lot of stuff to earn any.

After you stop getting Threads as level-up rewards at Vet Level 11, you’re hardly ever going to get so many Threads at once again (save for converting Notices of the Well, at least), and those Empyreans slack off into a trickle as you near Vet Level 99. The farther you can stretch those threads and Merits, the more quickly you can build up all the Incarnate powers you need. Remember, you’re going to have to build Tier 1 through Tier 3 twice for each power before you can build its Tier 4.

Another reason to save Empyreans is that, after you finish getting everything you could want to Tier 4, you can convert 50 of them into one Transcendent Merit for easier emailing to your other characters, to give them a head-start on building up their own Incarnate powers. But you have to have at least 50 to make the conversion—otherwise, you’re going to have to compose and send one email for each and every Empyrean you have.

**CLOSING THOUGHTS**

I hope that this guide has helped fill in some of the gaps in your Incarnate knowledge, and given you some ideas about how to use this system going forward. If there's anything that's still unclear, please let me know in the comments. If I didn't do a good enough job explaining something, I'll be happy to go back and expand the section to cover it better.

Likewise, if I made any factual errors or mistakes, please tell me about those and I'll fix them. And, as previously stated, if you disagree with my opinions about anything, I'd love to have a discussion for the further elucidation of the people who come to read this later.

Thanks for reading, and I'll see you in the City of Heroes!

**Edited June 18, 2021 by GM Kal**
