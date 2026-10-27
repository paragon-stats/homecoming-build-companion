---
title: "Advanced Binds - Cycling Attribute Monitor Stats on Key Press"
url: "https://forums.homecomingservers.com/topic/27236-advanced-binds-cycling-attribute-monitor-stats-on-key-press/"
source: "forums.homecomingservers.com"
authors: ["Avernal"]
date_posted: "2021-03-18"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "recharge-endurance"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "9e91173f5b1b9c2d"
---

# Advanced Binds - Cycling Attribute Monitor Stats on Key Press

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Avernal, 2021-03-18

This one ties in with my custom default binds topic, as I use this same set of monitored stats on all my new alts. We're going to set up our 'O' key so that each time you press it, it will change the set of stats shown on your screen in the combat attributes monitor window. It'll cycle between general stats like your regen, recovery, recharge speed and influence total, defensive stats and resistance stats.

One important thing to note here is that a couple of the combat monitor attributes are actually bugged in the game client and the slash command for them doesn't work - so there are a couple of stats (in the Resistances section if I recall correctly) that cannot be shown on the monitor window - they will display if you go in to the full combat stats window with all the dropdowns, but there's no way to display them onscreen unless the HC devs figure out how to fix the commands unfortunately.

This is probably the bind that uses the most text files of any I have (TWELVE of them all up!) but the actual content of those files is really simple - it's a repeat of the '+down$$-down$$' beginning, which makes it a toggle that runs on both the key being pushed in and again when let go, then it's literally just telling the game to start (or stop) monitoring certain attributes. I'll break down how each pair/quad of files relates to what is happening ingame when you hit O, so it's all in plain English and easy to understand.

The reason it needs so many files is the ingame slash commands use really long names and you have to actively tell the game to STOP monitoring a stat before you start monitoring a new one if you want your stats window to remain consistent, with everything always appearing in the right order and not bugging out or scrolling stuff off the bottom by accident.

First up - the basics, and some things you'll benefit from knowing, in plain English:

-
Binds have a hard limit of around 255 characters, so for more complex ones you need to split them in to chunks (files) that fit those restrictions. That's why binds like this use multiple text files.

-
The 'bindloadfile' command will default to the 'root' game directory if you use relative paths ("bindloadfile file1.txt" would look for file1.txt in the root directory), this means you're cutting out however many characters you would waste on typing out the full HDD path, so you don't need to use "C:/Binds/whatever.txt" as a sloppy workaround for that - "binds/whatever.txt" is MUCH shorter than even that and lets you keep your binds subfolder in the game's directory. Win-win. Now this will differ from launcher to launcher - but essentially Tequila will look in the 'Tequila' directory if you don't use a full path, whereas Homecoming's launcher would look in "settings/live" (for the live servers). You may need to fiddle with this if you're using other launchers to find out where the game is looking by default, generally it'll be in the same location as the City of Heroes executable file, Homecoming have just sorted their versions to their own subfolders.

-
Removing the underscores from slash commands makes them shorter and the game still understands them - you don't need "bind_load_file", because "bindloadfile" works too and is two characters shorter. I like "bindloadfilesilent" because whilst it's a little longer, it means my chat window isn't getting spammed with bind files loading in, and I can still split most of my long bind loops in to 2 or 4 files to avoid the hard limit of text characters.

-
If you add "+ $$- $$" (note the spaces) to the beginning of your multiple file bind, it will actually execute two steps - once when you press the key down, and again when you release it. In a bind that uses multiple files - this means one keypress will run *two* bind files on a single push and release, speeding things up (this is the shortest possible number of characters and is thanks to JAMMan0000's suggestion on my 'Lazy Kin' bind topic). We're using this here so we only have to hit Q once and it'll slap them with Speed Boost then queue up Increase Density immediately after.

This is going to be used here because the monitor attribute commands are quite long and hit the 255 character limit quickly, so for each set of stats the window displays we're going to need them to be split between two files - one that runs when you push O down, and the second that runs when you let go.

-

Now that's out of the way, the binds...

**File 1 'mon1.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Monitor certain stats then load in the next file in sequence (this is half of the first set of stats - loaded in when you PRESS 'O').

O "+ $$- $$monitorattribute regeneration rate$$monitorattribute recovery rate$$monitorattribute Endurance Consumption$$monitorattribute to hit bonus$$monitorattribute damage bonus$$bindloadfilesilent binds/mon2.txt"

**

File 2 'mon2.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Monitor certain stats then load in the next file in sequence (this is the second half of the first set of stats - loaded in when you RELEASE 'O').

O "+ $$- $$monitorattribute recharge time bonus$$monitorattribute Influence$$bindloadfilesilent binds/mon3.txt"

So these seem more complicated than my other bind topics, but they really aren't - file 1 and 2 are essentially the 'main' part of the bind, they're loading in the first set of stats we want to monitor (7 in total so we don't start losing them as we hit the limit and scroll them off the bottom of the window) - the content of these two files all happens in the space of time it takes you to hit O once.

Now, the next two files will be the reverse of this - remove all seven of these monitored stats from the combat attribute monitor window, so that we have a blank slate before the next seven get loaded in, and our bind doesn't bug out/get out of sync/scroll excess stats off the bottom of the window.

** File 3 'mon3.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Stop monitoring certain stats then load in the next file in sequence (this is half of the first set of stats - loaded in when you PRESS 'O').

O "+ $$- $$stopmonitorattribute regeneration rate$$stopmonitorattribute recovery rate$$stopmonitorattribute Endurance Consumption$$stopmonitorattribute to hit bonus$$stopmonitorattribute damage bonus$$bindloadfilesilent binds/mon4.txt"

** File 4 'mon4.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Stop monitoring certain stats then load in the next file in sequence (this is the second half of the first set of stats - loaded in when you RELEASE 'O').

O "+ $$- $$stopmonitorattribute recharge time bonus$$stopmonitorattribute Influence$$bindloadfilesilent binds/mon5.txt"

Those four files are now a complete cycle of loading in seven stats when O is pushed once, and then removing those stats when you push O again (four steps in total when you count that pressing O counts as one step and releasing it counts as a second). Every file we add from now on is going to be a repetition of these same four steps but we'll use different attribute names each time. So the twelve bind files, divided by these four steps, means you're cycling through seeing three different sets of stats on your screen - and between each set of stats 'O' is blanking them/turning off stat monitoring altogether - meaning you can play with no stats visible/general recovery and recharge stats/defensive stats/resistance stats.

**File 5 'mon5.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Defense attributes

O "+ $$- $$monitorattribute Melee Defense$$monitorattribute Ranged Defense$$monitorattribute AoE Defense$$monitorattribute Smashing Defense$$monitorattribute Lethal Defense$$monitorattribute Fire Defense$$bindloadfilesilent binds/mon6.txt"

**

File 6 'mon6.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Defense attributes

O "+ $$- $$monitorattribute Cold Defense$$monitorattribute Energy Defense$$monitorattribute Negative Energy Defense$$monitorattribute Psionic Defense$$bindloadfilesilent binds/mon7.txt"

**

File 7 'mon7.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Defense attributes

O "+ $$- $$stopmonitorattribute Melee Defense$$stopmonitorattribute Ranged Defense$$stopmonitorattribute AoE Defense$$stopmonitorattribute Smashing Defense$$stopmonitorattribute Lethal Defense$$bindloadfilesilent binds/mon8.txt"

**

File 8 'mon8.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Defense attributes

O "+ $$- $$stopmonitorattribute Fire Defense$$stopmonitorattribute Cold Defense$$stopmonitorattribute Energy Defense$$stopmonitorattribute Negative Energy Defense$$stopmonitorattribute Psionic Defense$$bindloadfilesilent binds/mon9.txt"

That is all the Defense stats - notice how it's functionally similar to the first four files that show General stats? We're going to do the same again now with another block of four files, but this time for Resistance stats.

** File 9 'mon9.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Resistance attributes

O "+ $$- $$monitorattribute Smashing Resistance$$monitorattribute Lethal Resistance$$monitorattribute Fire Resistance$$monitorattribute Cold Resistance$$monitorattribute Energy Resistance$$bindloadfilesilent binds/mon10.txt"

** File 10 'mon10.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Resistance attributes

O "+ $$- $$monitorattribute Negative Energy Resistance$$monitorattribute Psionic Resistance$$monitorattribute Toxic Resistance$$bindloadfilesilent binds/mon11.txt"

**

File 11 'mon11.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Resistance attributes

O "+ $$- $$stopmonitorattribute Smashing Resistance$$stopmonitorattribute Lethal Resistance$$stopmonitorattribute Fire Resistance$$stopmonitorattribute Cold Resistance$$stopmonitorattribute Energy Resistance$$bindloadfilesilent binds/mon12.txt"

** File 12 'mon12.txt'** - located in a '/binds' sub-folder of my Homecoming installation ('/settings/live/')
Follows the same pattern as the four files above, this time we're loading and unloading Resistance attributes

O "+ $$- $$stopmonitorattribute Negative Energy Resistance$$stopmonitorattribute Psionic Resistance$$stopmonitorattribute Toxic Resistance$$bindloadfilesilent binds/mon1.txt"

Phew! A lot of files but that's it - mon12.txt loads in mon1.txt at the end, which starts the loop over from the beginning.

**To initially load this bind ingame you would use the following command:

/bindloadfile "binds/mon1.txt"**

Once that's done it will automatically keep rotating between the 12 files as you press O on the keyboard.

A whole lot of bind files there but you only have to create them once and then they'll provide you with a nice, quick way to see all the important stats at a glance, and a convenient way to cycle through sets of those stats - turning them off inbetween so you also have the option of not cluttering your screen.

As I mentioned at the start of the topic, I use this as part of my default keybind set-up for new alts - so my O key in that file is a straight copy & paste of the contents of mon1.txt - the game then loads in the rest of the files in sequence for me.

**Edited May 28, 2021 by Avernal**

Updated to use shorter path
