---
title: "Configuring Power Trays: a post-Page 7 cheat sheet!"
url: "https://forums.homecomingservers.com/topic/49164-configuring-power-trays-a-post-page-7-cheat-sheet/"
source: "forums.homecomingservers.com"
authors: ["Shenanigunner"]
date_posted: "2024-02-21"
date_captured: "2026-10-26"
captured_by: "local-capture"
topic_tags: ["mechanics", "recharge-endurance"]
trust: single-author-opinion
contradicts_data: "none"
content_hash: "a2699415656082dc"
---

# Configuring Power Trays: a post-Page 7 cheat sheet!

## Summary

- Summary pending review: captured from the opening post by the forum guide scraper.

## Verbatim excerpt

### Post 1 — Shenanigunner, 2024-02-21

An update earlier in Issue 27 added the option of numeric recharge timers for power tray icons. With Page 7, this simple feature was expanded with a full range of configurability. Since defaults were changed (with timer display now the default setting) some vast number of players logging on thought timers were new, and the rest howled at the fake moon about how their beloved icons had been besmirched.

While a "GO SEE THE POWERS SECTION OF THE WINDOWS MENU" chat macro may be needed for some time, as players return to the horror that are recharge timers, it is true that the configuration of the timers is a bit convoluted. I just updated the **Tech Guide** with comprehensive information on the features and the associated slash commands, and I really recommend serious (or seriously angry) users refer to it for complete information.

But for those who prefer online info, here's a concise rundown of how to get your power tray icons to behave exactly as you want, even if it is the 19-year-old legacy behavior.

| Super-Quick Gimme Back My Icons Fix: In the Options | Window | Powers menu, set— — Power Tray Animations to Enabled, and — Power Recharge Timer to None. There. Just like it's been for 19 years. Whew. |

All of these changes can be accessed through slash commands (and thus binds or macros). All but one can be adjusted using that Power section under the Windows page of the game menu —

—but there is some fuzziness of terminology and combined actions that can be frustrating. So here's how it works:

**Tray Slot Labels**

From the dawn of time until Page 7, each slot on each tray was given a number label from 1 to 10. The Page 7 update added a menu option (**Power Tray Labels**) that allows you to set the slot labels to—

-
**Numbers **– those same the numbers (1-10) on each tray.

-
**Keybinds **– the first associated keybind (1, 2, 3 … 0, or C-1, C-2 etc.) on the primary and secondary trays.

-
**None **– no labels at all.

The associated slash command is **/tray_labels [0-1-2]**.

I suggest the clean, uncluttered look of None, since the numbers aren't really useful during gameplay and the keybind display is so limited. (Do you really need to be told Tray 1 is activated with 1-0?)

**Enable Power Recharge Timers**

To enable the countdown timers on your tray icons, set **Power Recharge Timer** to any setting but **Off**. Selecting **Always **means all icons will always display a timer while charging.

**Selective Timer Appearance**

You have some choices there, to suppress the timers on short-recharge powers where you don't need detailed info and the timer is just visual clutter and busyness. Set **Power Recharge Timer** to suppress powers with recharge times under 5, 10, 15, 30 or 60 seconds if that suits, you. The associated slash command is **/recharge_timer_threshold**, where **0** is off, **1** is always, and **any value of 2 or more** is the number of seconds under which to suppress timer display.

**Power Recharge Timer Format**

The power timers can be configured in several ways.

**Time Display Format**

The time display can be configured using **Recharge Timer Format** to show—

-
**Seconds **(including over 60).

-
**Seconds with tenth-seconds **under 10 seconds.

-
**Minutes:Seconds.**

-
**Minutes:Seconds with tenths.**

The associated slash command is **recharge_timer_format [0-4]** (0 and 1 select Seconds).

**Time Display Location**

Timers can be placed across the top, middle or bottom of the icons using the Power Timers Location option.

The associated slash command is **/recharge_indicator [0-2]**.

Note: this legacy command used to toggle Off/Top/Center/Bottom but was changed for Page 7.

**Time Display Color & Opacity**

Timers are white by default, but two color-change sets can be applied to help indicate recharge status. (The colors slowly blend between stages across the thirds of recharge time.) The **Recharge Timer Color** option allows—

-
**None **(white numerals).

-
**Red-Yellow-Green** progression across recharge time.

-
**Blue-Yellow-Red** progression across recharge time.

The slash command is **recharge_timer_color [0-2]**.

The timer opacity can be adjusted in all cases with the **Recharge Timer Opacity** slider, from **10-100%**.

The associated slash command is **recharge_timer_opacity [0.10 - 1.00]**.

**Icon Animation/Bloomback**

In the original update, the icons still shrunk and grew back whether timers were enabled or not. With Page 7, the animation can be disabled (and apparently is, by default). Follow along carefully; this gets a little tricky.

If **Power Tray Animations** are disabled, the icons will dim but not shrink/bloom.

The associated slash command is **tray_animations [0-1]**.

If **Power Tray Animations** are enabled, the icons will dim and shrink/bloom only if timers are disabled (legacy mode).

If you want animated icons with the timers, you're in deep trouble, because there's no menu option for this.

There is, however, a slash command: **/tray_always_shrinks [0-1]**.

Enable that with the 1 argument and you will have shrinky-bloomy icons at all times, with or without timers enable.

Any questions?
