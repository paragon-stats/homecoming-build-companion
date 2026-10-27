---
title: "Guide to Using Mids' Hero Designer"
url: "https://forums.homecomingservers.com/topic/19501-guide-to-using-mids-hero-designer/"
source: "forums.homecomingservers.com"
authors: ["Derek Icelord"]
date_posted: "2020-05-25"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "set-bonuses-io", "leveling-content", "tools"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "8badd22227a3a21b"
---

# Guide to Using Mids' Hero Designer

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Derek Icelord, 2020-05-25

Welcome to Derek Icelord’s Guide to Mids’ Reborn.

Everything you wanted to know about Mids’, but were afraid to ask!

Before we get to the meat of it, a few disclaimers:

-
This is a guide for how to use Mids’ Reborn, not how to build super OP characters. Go to the Archetype forums for those discussions.

-
Mids’ Reborn is only a near perfect representation of the game. The dev team has put in a ton of effort, but Mids’ has been passed down through at least 3 development teams over the years and not everything is as easy as changing a couple numbers around. There may be cases where Mids’ says you can do something you cannot do in game, or where numbers don't perfectly match.

-
The /build_save command now works! The /build_save and /build_save_file <name> commands save a text file in your profile’s account folder that can be opened in Mids'.

-
No the UI does not get any bigger.

With that out of the way, let’s dive in!

**Quick Start**

For those of you with short attention spans.

-
Name and Origin have no effect on the build itself. Origin will slightly change the icons for DO and SO enhancements.

-
Mids’ has 3 build modes, selected by clicking the button next to the Name box.

-
Normal lets you place powers and slots in any order, but still obeys the slot restrictions as if you were leveling up normally (ie no 6 slotting the level 49 power pick)

-
Respec allows placing of slots in any order, in line with the game’s respect logic.

-
Level-Up alternates power picks and slot placement as if you were training from level 1 to 50 in-game.

-
Picking your Archetype will populate the Primary and Secondary power drop downs with the available options.

-
Selecting a power from either will lock your archetype selection.

-
Single click a power from beneath the drop downs to place or remove it from a build. If a power’s name appears in red italics, it has been placed in an illegal position. The most common reason is placing a pool power without meeting the prerequisites or picking more than one origin pool (Experimentation, Force of Will, or Sorcery).

-
Once placed in a build, powers can be dragged to different power placement levels. If a power is in the destination area, the powers will swap places.

-
Once a power is placed, single click to place an enhancement slot on it. If you wish to remove a slot, hold Shift and click on the slot to remove.

-
Every power can have its enhancement slots flipped to an alternate. This is useful for comparing enhancement values or set bonuses across different options. To flip a single power to its alternate slotting, hold Shift and right click the power. You can flip all the slots on a build to alternate using the menu option under Slots/Enhancements -> Slots -> Flip All to Alternate.

-
To place an enhancement in a slot, right click the slot to bring up the enhancement window. Dragging the cursor outside the window dismisses it.

-

-
In the enhancement window you can select regular, non-set IO, special, and Set IO enhancements. Clicking on Set will show the types of sets available. Event and Archetype enhancements are listed under the Set category.

-
Relative levels can be altered using the ‘+’ and ‘-’ keys.

-
IO level can be set using the number keys. Non-set IOs are only available in 5 level increments. Selecting a level outside set’s range will put the level at the closest available level for the set.

-
The empty icon left of normal enhancements will change the slot to empty.

-
The middle click function will repeat the last enhancement placed, both (relative) level and type. If the last enhancement placed was part of a set, middle click will place the highest ordered enhancement not placed in the power. This is according to the Mids’ database ordering, which may not always perfectly match the in-game ordering.

-
Note: If the last enhancement placed is not a valid option for the power clicked on, middle clicking will clear the enhancement slot.

-
When placing a set enhancement, if Pop-Up is enabled it will show a detailed view of the set’s bonuses. This info is always available in the Info box in the lower left.

-
Note: There is a minor UI bug in the Info box. To scroll through the set bonuses, move the mouse wheel to focus on the Info tab the use the up and down arrow keys to scroll.

-
City of Heroes caps set bonuses at 5 for a given bonus and value. This means that a set bonus of +12% Regeneration is not counted the same as a bonus of +16% Regeneration. Set bonuses and global bonuses (such as Luck of the Gambler’s +7.5% Recharge) are also counted separately. Any bonus that exceeds 5 of the same type/value has no effect.

A More Detailed Dive:

**UI Buttons**

-
The Enemy Relative Level dropdown lets you set relative level for determining final accuracy of powers.

-
Build Comments lets you leave comments, such as if a power is just a set mule and not intended to be used.

-
Team Members allows you to pick how many team members and what AT they are for calculating powers like the Kheldian inherents.

-
The Hero/Villain button changes which set of accolade powers a build has access to. It also changes Mids’ color scheme between blue and red. Hero and Sentinel ATs default to hero, Villain ATs default to villain when creating a new build.

-
PvE/PvP mode toggles between PvE and PvP numbers and mechanics.

-
Note: Homecoming altered PvP tables/values from Live, and Mids’ is not an accurate representation of Homecoming PvP.

-
Recipes and Pop-Up toggle pop up boxes on mouse over for power and enhancement details. If Recipes is on, mousing over a set IO shows the recipe instead of the set details.

-
Note: Recipes has no effect if Pop-Up is not toggled on.

-
Prestige Powers opens a window to select powers available from the P2W Vendor. Selected powers are added to the inherent section of the UI, and can be individually toggled with the green radio buttons.

-
Accolades opens a window to select accolade powers. Selected accolade powers are added to the inherent section of the UI, and can be individually toggled with the green radio buttons.

-
Temp Powers toggle temporary powers on or off with a single click. A double click opens the window to select which powers are toggled on/off.

-
Incarnate Powers opens the incarnate power window with a single click. Selecting an incarnate power will add a power in the inherent section of the UI with toggle buttons when appropriate.

-
Normal/Respec/Level-Up toggles between slots obeying level placement rules as when leveling a character up, and free form placement when respecting. Most notably that means allowing 6 slots on the level 49 power pick.

-
Power Only/Power / Slot toggles between placing powers only, or both powers and slots.

-
View Active Sets brings up the same window as the Sets & Bonuses menu option described above.

-
Slot Levels toggles the display of slot placement levels, same as the option in the View menu.

-
Slots to go/slots placed changes if you see how many slots are available to place or how many slots have already been placed.

**Menus**

**File:** The usual stuff that’s been listed under File since at least Windows 95. If you need this explained, please ask your grandchildren. Mids’ will prompt you to save when opening or clearing a build if changes have been made since the last save. Of note, Export -> Export to Beta Server creates a **popmenu** that will grant you the enhancements for you build on the Open Beta (Brainstorm) server. It does not automatically load the build onto a character.

**Options:**

-
Change Database lets you switch between installed Databases for different versions of City of Heroes. A fresh install of Mids’ comes with Homecoming and Rebirth, with Homecoming being the default.

-
Configuration allows you to change the default settings of Mids’.

-
It is covered in more detail in the Advanced section.

-
Updates & Paths is where you can change the location of saved builds, and if you want Mids’ to load the last open build at startup.

-
Note: Mids’ doesn’t always like to remember that you’ve changed this.

**Build Sharing:** The various ways to share a Mids’ build other than sending someone the .mxd or .mdb file. Note: Many of these ways do not play nice across different versions of the program.

-
**Secure Share**

-
Build (DataChunk) is a non-human readable code chunk that can be imported into Mids’

-
Forum Post (Formatted) is a more human friendly listing of the powers and enhancements. BBCode works for the Homecoming forums, unless you want to include the Bonus Totals and Set Breakdown. Use Plain Text for that.

-
Build Link (API) will generate a temporary API link that can be shared via Discord or as a URL. The expiry date cannot be altered.

-
InfoGraphic (Image) exports an image of the basic totals in a build to show off to others.

-
**Legacy Share** appears to be broken in all the tests I ran.

-
**Import DataChunk** will import a data chunk mentioned earlier.

-
View Secure Shared Builds will show a list of previously imported API builds.

**Slots/Enhancements:** these options let you alter your slotting and enhancements en mass

-
Set all IOs to…

-
Default puts all IOs to the default setting

-
By default this is level 50. Set IOs that stop before 50 will be set to their highest level.

-
This can be changed in the Configuration menu in the Enhancement & Views tab

-
Maximum: maximum allowed level

-
Minimum: minimum allowed level

-
Set all Enhancement Origins, pick Single Origin, Dual Origin, Training Origin

-
Note: Mids’ does not list enhancements by character origin, as the effect is the same regardless

-
Set all Relative Levels lets you put all enhancements to a relative level from -3 to +5

-
IOs cannot have a negative relative level; they will be set to even if a negative value is selected.

-
Only IOs can go above +3, non-IOs will be set to +3 if a higher value is selected.

-
None disables normal and special enhancements (the equivalent of being more than -4 in game) and sets IOs to even level.

-
Slots

-
Flip All to Alternate will flip all slots on the build to their alternate (shockingly counterintuitive, I know).

-
Clear All Enhancements will clear all active slots, but not the alternate slotting.

-
Remove All Slots will remove all the slots except the auto-granted slots each power gets automatically.

-
Auto-Arrange All Slots will shuffle slot levels around to the earliest they could be placed in the build

-
Toggle Enhancement Check Mode puts a shopping list of Enhancement Catalysts and Enhancement Boosters in the lower left of the UI.

-
You can click Superior enhancements in the build to “check off” catalysts

-
You can click boosted enhancements in the build to “check off” boosters

-
Note: Currently the UI doesn’t have a way to “check off” more than one booster per boosted enhancement, though the total number of Boosters listed in the build is correct.

**View:** this lets you change how you view Mids’. Highly counter-intuitive, I know!

-
Columns: pick 2-6. 3 is the default, 4 is particularly useful for Kheldians with shape shift powers.

-
Show IO Levels puts the IO’s level on the top level UI so you don’t have to mouse over to see it

-
Show Enhancement Relative Levels does the same for normal/special enhancements and boosted IOs

-
Show Slot Placement Levels shows the level the slot was placed, this can also be toggled with a UI button

-
Show Relative Levels with Signs changes display of Enhancement relative levels from numbers to 1-3 “-” or “+” symbols. Boosted IOs will always display numbers.

-
Show Damage Per…

-
Activation is the total damage the power deals (i.e. damage per click)

-
Second is ye olde DPS

-
Animation is for powers with damage broken up over more than one animation cycle (e.g. Shadow Maul)

**Window:** some options are also available with a UI button. All open a new window, and most can be open at once if you love screen clutter. This causes performance issues and is very much **not** recommended.

-
Sets & Bonuses (View Active Sets button) shows you all the IO sets and their set bonuses currently in the build. It also alerts you if a set bonus exceeds the Rule of 5 (see Enhancements section for more information)

-
Power Graphs show bar graphs of a build’s powers (both taken and available) with lots of stats to choose from.

-
Powerset Comparison compares the stats of powers across different powersets and Archetypes. It compares straight across tiers, so usefulness may be limited (i.e. a set that has a utility/control power as a T5 vs. a set with a pure damage power at T5)

-
Data View gives high detail information about a power when moused over.

-
Advanced Totals (View Totals button) shows more character stats than the Totals tab in the Info box. This can also be opened with a UI button.

-
Recipe Viewer shows the recipes for all IOs the current build. This includes buy costs for normal IOs, crafting costs, and salvage requirements. It can also generate a shopping list that stays open over other windows to help with market purchases.

-
Rotation Helper is meant to aid in attack power rotations. This is in beta, results may vary.

-
Set Inspector is a database to search for specific set bonuses, in case you don’t have them all memorized already.

-
In-Game Respec Helper stays on top to help you with respecs in game.

-
Profile and History are same. This may have been different in the long ago, but not anymore.

-
Short only lists powers and slots, Long also lists what enhancement is put into each slot.

**Viewing Your Totals**

Now you’ve made your shiny new build. How do you see what everything does?

-
The Info box in the lower left shows a lot of data, broken up into 4 tabs. It can be floated into a separate window by clicking the ‘F’ icon to the left of the power name.

-
If your Totals box shows lots of big negative numbers, turn off Rest.

-
Mouse over a power, or enhancement, to see its specific data in the info box. You can right click a power to lock it, allowing you to switch tabs without having to hover over the power again. To unlock the power, click on the [Unlock] text near the power’s name in the Info box. A power does not need to be in the build to see its info, or locked into the info box.

-
The Info tab shows a power’s short and long description, as well as essential stats like endurance cost, accuracy, recharge, etc… If an enhancement is moused over, the main box will show the enhancement’s values, and set bonuses if part of a set. If a power scales off of number of enemies (e.g. Invincibility or Rise to the Challenge), the Info tab will have a bar allowing adjustment for how many enemies are contributing to the effect.

-
The Effects tab shows more detailed info about secondary effects in a power, such as mez magnitude, specific damage defense and resist values and so on.

-
The Totals tab shows totals for the entire build. Individual powers can be toggled on or off using the green radio buttons on the right side of the power name in the build. If a power has a proc, it can be toggled individually from the power using the yellow radio button.

-
The Enhance tab shows how much enhancement a given value for the selected power has. Mids’ shows both current and pre-enhancement diversification (Pre-ED) values. The UI will color the numbers based on how much enhancement value is lost due to ED. White for no loss, green for minimal loss, yellow for moderate loss, and red for high loss.

-
For Archetypes with access to Sniper attacks, there is a Quick Form button located in the inherent section. Toggling this on will show the values for fast snipe.

**Advanced**

The section where we talk about all the exciting and wonderful ways you can break your version of Mids’! While nothing here will destroy your saved builds (hopefully), it may lead to reinstalling the program. Proceed with caution. (Obvious warning is obvious.)

**Configuration menu:**

Enhancements & View is where you can change the defaults on screen view and enhancement type. Of note, the Set all IO and SetO button affects the currently open build. Info Tab text is the tab in the Info window, while Stats/Powers is the list of powers under the Primary, Secondary, and Pool drop downs. Tick the bold box to make the text bold. Note: Stats/Powers text appears to be fixed at 8.25 point size regardless of the setting. Use High-Visibility text turns the power bar text from black to white.

Effects & Maths settings are for specific states in the game. Chance of Damage deals with effects that do not go off all the time (principally damage procs). Arcana Time shows animation time as the server breaks down real seconds into ticks. Suppression allows you to toggle the various effects a character can be under. Of particular use is the Attacked effect, which will remove the suppressed DEF of powers like Stealth or Hide while in combat.

Drag & Drop allows changing program behavior on various drag & drop events.

Updates & Paths the default save location for builds, whether or not to load the last build on startup, and automatic update settings.

The DB Version Warning checkbox gives a pop-up warning if a loaded build was created on a different database version than the current one.

For support and feedback on Mids’, join the Discord or post in the thread in Mids’ Subforum

If you questions on the guide, something isn’t clear, or just want to tell me I’m an incompetent prick, post in this thread.

**Edited February 17, 2025 by Derek Icelord**
