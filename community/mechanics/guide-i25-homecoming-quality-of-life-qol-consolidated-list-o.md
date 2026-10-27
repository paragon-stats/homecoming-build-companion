---
title: "GUIDE: i25 Homecoming Quality of Life (QoL) consolidated list of tweaks"
url: "https://forums.homecomingservers.com/topic/6426-guide-i25-homecoming-quality-of-life-qol-consolidated-list-of-tweaks/"
source: "forums.homecomingservers.com"
authors: ["LaconicLemur"]
date_posted: "2019-07-16"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "recharge-endurance"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "70f5ae03692a7e8e"
---

# GUIDE: i25 Homecoming Quality of Life (QoL) consolidated list of tweaks

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — LaconicLemur, 2019-07-16

City of Heroes is full of hidden settings and options that can make playing the game a lot easier. It's a mix of commands, binds, and in-game "hacks" that I run almost each time I start playing an alt.  Most, if not all, of these have been posted in the forums. Almost all you can reference more info from ParagonWiki. Some are only available on the Homecoming servers. Here I've consolidated the ones I use the most and find the most helpful.  Links to sources where I could find them.

Note: Hero-side locations.  Also, obviously there is subjectivity to what is useful. Would welcome posts on other ideas, or links to other sources that you find useful.

***

**Tweaking your HUD:**

- Map can be undocked from Compass/Nav bar and scaled so you can have a perma mini-map

- Seeing and tracking additional combat info

Powers Bar > Powers > Combat Attributes > right-click on attribute and "track"

- Team buff icons and info  Team window > Arrow on the right side to expand and view buffs for each teammate. Also to see their current location (sometimes buggy if they are on a mission map).

- View numerical cooldowns for powers (ex. Hasten): Options>Windows>Beta

- Command to consolidate buff icons

"/optionset buffsettings [numCode]"

4096 - group window numeric stacking

16  - status window numeric stacking

1048576 - pet window numeric stacking

- Change the font for chat and other texts (https://fonts.google.com/ has some good ones)

- Change your mouse cursor size via Options

***

**Window and text scaling:**

- For scaling all windows, use the setting under Options

- Chat window can be further customised under Options, including font size

- For specific windows, you need to undock the particular window from its parent.

Then you can use the "/window_scale [windowName] [scale]" command. Common windows are (with sample scaling):

/window_scale compass 1.20

/window_scale Email 1.20

/window_scale PowerList 1.30

/window_scale Salvage 1.25

/window_scale Chat 1.10

/window_scale Enhancements 1.15

/window_scale Power 1.05

/window_scale Health 1.05

/window_scale contact 1.20

/window_scale Info 1.20

***

**Helpful hacks:**

- Autofire for Hasten (or any power): Ctrl+ left-click on the power

- Change your mission difficulty settings anywhere: click on the chat bubble icon on the chat window > "Notoriety"

- P2W Vendor (Atlas Park, FBZ, Mercy Island, Grandville, Pocket D.  Nova Praetoria is T4V in map) useful temp powers - starter kit (Look under "Prestige Powers") "

Use Sands of Mu, Nemesis Staff or Black Wand (all free) for additional attacks. Useful until around level 15-22.

Buy the generic Jet Pack for 5,000 influence per 30 mins of flight time. Buy at least 2 hrs' worth.

- P2W Vendor useful temp powers - teaming kit (Look under "Prestige Powers")

Team Transporter (to Mission); 10M influence

Supergroup Portal (create an SG portal at your location); 10M influence

Mission Transporter (user only); 1M influence

Base Transporter; 1M influence

Assemble The Team; 1M influence

Reveal; 10k influence  (reveals entire mission map when available. Can also reveal zones like Firebase Zulu. More helpful than you think)

- Custom window maker.  Step-by-step guide here.  Popmenu guide here.  Helpful for organising travel temp powers.  Original guide here.

Note: I could not get the window Edit option to work. Had to create a custom macro once then edited the custom.window in notepad.

Note: This will add the custom window to *all* accounts using the same CoH game folder/directory.

- How to manage your character setups when you have alt-itis (advanced guide)

- Create macro for emailing yourself inf: /macro "Money" emailsendattachment Global Subject Infl AttachmentType InventoryLocation Message

->Not much known about command, but: AttachmentType values are: 2=inspirations, 11=salvage, 12=recipes; Global only works for single-name chars.

-> ex: /macro "Money" emailsendattachment @MyGlobalName "More inf" 2000000 "0" "0" "Cash from @Name"

***

**Useful keybinds:**

- Creating keybinds and macros @ ParagonWiki

- Bind Tray 1, Power 10 to mousebutton: "/bind button4, powexec_slot 10" (Or change via Options>Keybinds)

- Change Tab to target nearest enemy: "/bind TAB "target_enemy_near"  (Or change via Options>Keybinds)

- Find custom targets (e.g. NPC to save, or boss to kill): "/bind alt+g beginchat /bind g target_custom_next [enemy]" (replace g as necessary. Can use partial name, like "Joe" instead of "Joe Smith")

- Auto-convert any 3 small inspirations to a small purple (Luck) inspiration:

"/bind p, insp_combine insight Luck$$insp_combine sturdy Luck$$insp_combine respite Luck$$insp_combine break_free Luck$$insp_combine awaken Luck$$insp_combine catch_a_breath Luck"  (replace p as necessary)

***

**Useful commands:**

- Access auction house from almost anywhere: /ah

- Print your current game coordinates: /loc

- Print local time: /localtime

- Clear chat: /clearchat

- Clear all powers from all trays except any custom macros (useful after a respec): /cleartray

- Export your build to a .txt file. You can then import the build into Mids Reborn: /buildsave

--> In Mids, click "File => Open". Navigate to "<YourCOHDirectory>\<YourAccountName>\Builds". Select the "build.txt" file and click "Open"

- In the Options window, you can save and load Options settings. Useful for having consistent options for all alts.

Reminder: Task forces ONLY NEED 1 person to start. Full team not required. (setting is specific to Homecoming servers)

Reminder: Null the Gull (Pocket D, bird on a truck villain-side) can change your alignment. Helpful if you want access to Villain Epic Powers. (Completing any arc will allow you to choose any Epic powerset. Black Scorpion arc is easiest.)

***

**Useful Supergroup Bases**

- Create an SG - go inside City Hall in AP and go to "Registration".  All bases and items are free.

- Invite alt to SG: "/altinvite [altName]"

- To generate your SG code, type: "/sg_passcode [word]"

- Install storage racks and tables to store salvage, enhancements, and inspirations. Permissions are editable.

- Install invention tables to create IO's without going to a university.

- Install beacons and portals to zones. You can install 10(?) beacons for each portal.  Suggest 5 or less for ease of use.

- Install a Pillar of Ice and Flame for access to Ouroboros missions

- Install a merit vendor ATM

- Install arcane/tech power items for energy and control if you want to install a rez station ("Teleport to base" option when killed )

- Base building video guide here

***

**Getting Around Quickly**

- Review the various teleport options that were revamped for HC in i27, such as the Long Range Teleporter Accolade

- Earn the Long Range Teleporter by parking your character in Pocket D for 1 hour

- Check the P2W Vendor for various Travel powers you can buy (see section above on useful P2W Vendor items)

- Options to get the Ouroboros travel power (insta TP to Ouroboros with access to multiple city zones)

(1) Ask someone to open an Oro portal.

(2) Get the Midnighter Badge (see tip following this) and enter Cimerora

(2B) Enter Recluse's Victory pvp zone

(3) Complete a mission (any story arc) via the Pillar of Ice & Flame at a super group base

(4) Complete a time-travel mission

- Options for Midnighter Badge: access to Ouroboros, Cimerora story arcs and ITF (Imperious Task Force)

(1) Go to Night Ward via the TUNNEL system and enter the mansion (5 minutes)

(2) Complete Montague Castellana's story arc

(3) Use LFG hack*

- Getting to Cimerora for ITF's and more: Steel Canyon South, Founders Falls, or Croatoa

- The smaller building to the right of the university.  Has a crescent moon on top.

- Getting to Shard Task Forces / Firebase Zulu: Long Range Teleport, TUNNEL system or use LFG hack*

- Getting to Rikti War Zone: Long Range Teleport, SG base teleport beacon, LFG hack* (Lady Grey TF) or Vanguard Depots @ Atlas Park & PI

- Earn the Monitor Duty or Rapid Response temp powers

*LFG hack: click on the LFG tab in chat window, select task force in desired zone. Queue solo, make sure to lock the queue to current group (you). Click the green button and you will get transported to the TF starting contact's zone.

***

**Saving influence when starting out**

- XP and Influence gain options are in the Options Menu

- XP boosters are available at the P2W vendor

- Email influence to yourself using your @globalName from a richer alt

- Need about 2.5 million influence to kit out to level 25 IO enhancements

- Level 25 IO's are functionally equivalent to level 50 SO enhancements

- @ Level 15, can go to University (Steel Canyon South) and talk to Administrator Lenk to learn about IO's

- Universities and Vanguard HQ in RWZ have invention tables for crafting IO's

- You can Memorise some recipes so that you never have to buy it again, thus 50% cheaper crafting cost. Useful if you plan to play multiple alts.

***

**Earning influence (check AH prices to confirm profit margins)**

- Buy "Inner Inspiration" (free) from the P2W vendor. Sell the inspirations you get for ~50k each.

- Complete 1-2 TF's and convert merits to Enhancement Converters. They go for ~ 100k in the Auction House (AH).

- Check the prices for the "Overwhelming Force Set". Join and complete a Summer Blockbuster (lvl 15+) and sell your (guaranteed) drop.

- Know that the Auction House will blend all crafted IO's together, no matter what level.  So a lvl 30 IO can be sold as a lvl 50 IO.

- At any level, craft any PVP sets and sell.

- Below level 30, craft and and convert any yellow healing recipes to get either Regen Tissue (lvl 10-30) or Miracle unique enhancements (+Regen and +Recovery, respectively).

- Between level 30 to level 50, craft any yellow healing IO recipes you find, and spend 2 or more converters to get a Numina IO. Either sell the Numina or spend more converters to convert it to the Numina unique. Same idea for defense sets and LotG unique.

- Craft and convert other enhancements to sell

- Run an AE low-level mission and buy rares or recipe rolls from tickets gained to sell on the AH

- Make a spines/fire brute and farm @ the AE buildings. Or look at other farming builds here.

***

**Ouroboros Story Arcs for Merits and Incarnate drops**

- *very* helpful to have Reveal Power (P2W vendor), and stealth/invis

- Freakshow War (55 merits / ~ 1 hr). Note the board transit mission, the bosses don't come out until 3-5 mins after you enter the map.

- Freakalympics (34 merits)

- Smoke & Mirrors (12 merits)

- Heather Townsend (Burden of the Past part 1) - 1 Emp Merit + random common Incarnate salvage + Incarnate salvage random roll

- Other: The fast way to gain all 4 Accolade Powers, even solo. (passive +HP, +End, + Rcvry, etc.)

- Other: - Guide to the Incarnate System

***

**Useful Powers & Generic slotting using generic IO or SOs

-Primary and Secondary generic slotting:

3x main effect + 1-2x end for toggles

3x main effect, 1/1/1 end / rech / acc for activated powers (at least 1 end for Brute damage);

3x main effect for clicky buffs

3x main effect + 2-3x rech for clicky buffs that have longer cooldowns

1-2x acc at first for damage-dealing powers

- Stamina: 1-2, depending need as you level. Slot Performance Shifter Chance for +End when IO'ing.

- Health: 1-2. Slot Panacea Unique while leveling if you can afford it. Slot Numina and Miracle uniques when IO'ing

- Hasten: 2-3 RchrgRdxn

- Leadership Pool (Maneuvers, Assault, Tactics, Vengeance): 1-2 EndRdxn then use as LotG "mule" slots for LotG IO's

- Travel Powers: Super Jump (faster) or Flight / Mystic Flight are easiest to use when starting out.

- Travel Powers: bind for using Teleport with mouse:

option 1: /bind shift+lbutton "powexec_name Teleport" (target to teleport)

option 2: /bind shift+lbutton "powexec_location camera:max Teleport"  (1-click teleport to max distance)

- Travel Power stealth uniques: Must slot on toggle travel power, won't work on non-toggle, e.g. Hurdle (I think)

- When IO'ing, 5 of each bonus effect is max. Rest is ignored. For example, Luck of the Gambler (LotG) +7.5% recharge max is 5 enhancements of this type. The bonus of the 6th will not count.

- Quick guide for understanding IO's (Gulbasaur)

- The old forum is archived and each AT sub-forum has a consolidated list of build guides (circa 2012, aka issue 24)

** This is fairly subjective but should give an idea of what to aim for in general. Then tweaked to suit your AT and playstyle.

***

**Useful Software**

Mid's Reborn / Hero Designer and link for zip file/custom installer here.

- Guide to using Mid's here.

- Ctrl-T to see summary buff window

- Defense softcap @ 45%, 59% for Incarnate (need more if fighting +5 enemies or those with high to-hit)

- Resistance hardcap @ 90%  for Brutes and Tanks. Epic ATs (khelds, soldiers, widows)@ 85%. All other ATs  @ 75%.

- Perma Hasten around +214% (depending on AT. Basically get recharge rate to 2 mins or less)

- See the comment above under "Useful commands" about how to use /buildsave to export your build from COH and import it into the app

Vidiot Maps

- Best way to find badges and lore

- Marks places of interest for PvP zone missions

- Other stuff

Hero Stats (HeroStats not working as of 2019-Dec)

- live stats tracker and buff cooldown tracker

Power themes by Archetype

- For help creating thematic characters

Abbreviations:

AT - Archetype

AE - Architect Entertainment

FBZ - Firebase Zulu

HUD - Heads-up Display

IO - Invention Origin (Enhancement)

LotG - Luck of the Gambler defense set (enhancement)

P2W - Pay to Win

PI - Peregrine Island

SO - Single Origin (Enhancement)

**Edited March 27, 2021 by LaconicLemur**

Cleaned up email money formatting
