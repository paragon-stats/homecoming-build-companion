# Capture Workflow — concrete steps

How to take one forum URL from `<source root>/community/CAPTURE_QUEUE.md` and ingest it as a markdown file in `<source root>/community/`.
The source root is the private `homecoming-source-material` checkout (`--source-root`, else `COH_SOURCE_ROOT`, else `../homecoming-source-material`).
Every step spelled out — no hand-waving.

Time per capture: 2-4 minutes once you've done one. First time: ~10 minutes including extension setup.

---

## One-time setup

### A. Install Claude for Chrome

1. Open Chrome.
2. Go to <https://www.anthropic.com/news/claude-for-chrome>.
3. Click **Install** (it'll redirect to the Chrome Web Store) → **Add to Chrome**.
4. After install, an icon appears at top-right of Chrome. If you don't see it, click the puzzle-piece icon next to your profile picture and **pin** "Claude" so it stays visible.
5. Click the icon. It opens a side panel on the right of Chrome. Sign in with your Anthropic account if prompted (same account as Claude.ai).

> Don't have / don't want the extension? **Fallback:** open <https://claude.ai/> in a separate tab, paste the forum thread's text into a new chat, then paste the prompt. Same outcome, more clicks.

### B. Generate the per-URL prompts (one command, one time)

Open **PowerShell** (not bash, not cmd):

- Press `Win+X` → click **Terminal** (Windows 11) or **Windows PowerShell**.
- Or press `Win`, type `powershell`, hit `Enter`.

Paste this and hit `Enter`:

```powershell
& "$env:USERPROFILE\repos\homecoming-build-companion\scripts\generate-prompts.ps1"
```

Writes [`inbox/PROMPTS.md`](../inbox/PROMPTS.md) — 24 ready-to-copy prompt blocks, one per URL in the queue, with URL, today's date, tags, and trust level pre-filled.

Re-run this any time you change `CAPTURE_QUEUE.md` (add new URLs from the Guide Index, etc.). The `inbox/PROMPTS.md` file is the workbench.

### C. Open the prompts file in your editor

Open `homecoming-build-companion/inbox/PROMPTS.md` in VS Code, Notepad++, or whatever you have. You'll be scrolling to specific URL sections and copying prompt blocks from here.

> Tip: you can also `Ctrl+F` for the URL number ("## 5.") to jump to that capture's prompt.

---

## Per-capture flow (repeat for each URL in the queue)

Numbers below match what you'll see in `inbox/PROMPTS.md`. Pick any URL — they're independent. Recommended order is the queue's priority order (do Priority 1 first).

### Step 1 — Open the forum URL in Chrome

In `inbox/PROMPTS.md`, find the section for the URL you want (e.g. `## 1. PPM Information Guide`). Click the **URL** link in that section's bullet — it opens in Chrome.

Wait for the forum thread to fully load (let images and replies render).

### Step 2 — Open the Claude for Chrome side panel

Click the **Claude icon** at top-right of Chrome (the orange/red one). The side panel slides in from the right side of the window.

If this is the first time on a forum page: Claude may ask permission to read this site. Allow it (or "always for this site").

### Step 3 — Copy the prompt block from `PROMPTS.md`

In the matching `## N. ...` section, find the block starting `You're reading a Homecoming City of Heroes forum thread...`. **Copy everything BETWEEN the backticks**.

Copy methods:

- VS Code: click inside the block, hover for the **Copy** icon at top-right of the code block; or use `Ctrl+A` after clicking inside.
- Notepad / Notepad++: select from `You're reading a Homecoming...` to just before the outer fence's closing backticks, then Ctrl+C.

Now your clipboard holds the prompt with the URL pre-filled.

### Step 4 — Paste into the Claude for Chrome side panel

In the Claude side panel: click the message input box at the bottom. Press `Ctrl+V`. Press `Enter` (or click the send button).

Claude reads the page DOM (because that's what the extension does — it has access to the active tab's content) and produces a markdown block matching the format you sent.

Wait for the response to finish streaming. It's a fenced markdown block containing `---` frontmatter, `## Summary`, `## Verbatim excerpt`, and `## Build attachments` sections.

If Claude **refuses** or **summarizes instead of extracting**: re-send and add "Output ONLY the markdown block, no preamble."

If still wrong: use the claude.ai fallback — paste the forum text into a Claude.ai chat with the same prompt.

### Step 5 — Copy Claude's response

In the side panel, hover over Claude's reply. A row of icons appears at the bottom. Click **copy** (overlapping-squares icon). The full reply goes to your clipboard.

Alternative: select the response with the mouse (click at the start, shift-click at the end), Ctrl+C.

### Step 6 — Save and ingest in PowerShell

Switch to your **PowerShell** window (the one from one-time setup B; reopen if you closed it).

Paste this and hit `Enter`:

```powershell
& "$env:USERPROFILE\repos\homecoming-build-companion\scripts\capture.ps1" -Paste -SourceRoot "$env:USERPROFILE\repos\homecoming-source-material"
```

What you'll see if it works:

```text
Saved → %USERPROFILE%\repos\homecoming-build-companion\inbox\<slug>.md
Target derived from topic_tags[0]='mechanics': mechanics/<slug>.md
Copied → <source root>\community\mechanics\<slug>.md
Re-generating <source root>/community/INDEX.md ...
Wrote ...\community\INDEX.md  (N captures indexed)
```

Done. The capture is now in the project, and `<source root>/community/INDEX.md` lists it.

If you want to be paranoid and also pass the URL for a sanity check (the script verifies the frontmatter URL matches):

```powershell
$url = "https://forums.homecomingservers.com/topic/5290-procs-per-minute-ppm-information-guide/"
& "$env:USERPROFILE\repos\homecoming-build-companion\scripts\capture.ps1" -Paste -SourceRoot "$env:USERPROFILE\repos\homecoming-source-material" -Url $url
```

Move on to the next URL.

---

## What if something goes wrong

### "Clipboard is empty" / "Clipboard content does not start with YAML frontmatter"

You either didn't copy Claude's response, or you copied something that wasn't markdown. Re-run Step 5, then Step 6.

### "Frontmatter missing required field(s): ..."

Claude's response missed required fields: `title`, `url`, `source`, `date_captured`, `captured_by`, `topic_tags`, `trust`. Open the file in `inbox/`, fix the frontmatter by hand, then run:

```powershell
& "$env:USERPROFILE\repos\homecoming-build-companion\scripts\capture.ps1" -Drain
```

This processes every file in `inbox/` — yours plus any others that previously failed.

### "Destination exists"

You already captured this URL. Either skip it, or pass `-Force` to overwrite:

```powershell
& "$env:USERPROFILE\repos\homecoming-build-companion\scripts\capture.ps1" -Paste -SourceRoot "$env:USERPROFILE\repos\homecoming-source-material" -Force
```

### Claude in the extension produces a long preamble before the markdown

The script peels a markdown fence if present. If stripped correctly, you're fine. Otherwise edit the file in `inbox/` to remove the preamble (everything before `---`), then `-Drain`.

### Forum is rate-limiting / asking for login

You may need to be logged into the forum for the extension to read full thread content. Sign in to forums.homecomingservers.com once in Chrome, then redo Step 4.

---

## Batch workflow (do several at once)

If you want to grind through 5-10 captures in a row without switching to PowerShell each time:

1. Capture URL 1 → copy response → paste into a new file in `inbox/<some-name>.md` (manually, in any editor).
2. Capture URL 2 → same.
3. ... repeat for as many as you want.
4. Once done, run:

```powershell
& "$env:USERPROFILE\repos\homecoming-build-companion\scripts\capture.ps1" -Drain
```

It ingests every `inbox/*.md` in turn, moves successful ones to `inbox/.processed/`, and leaves any that fail validation in `inbox/` for fixing.

---

## When you're done with a session

Tell me (Claude Code) "I've captured guides X, Y, Z." I'll glob `<source root>/community/**/*.md` and pull them into context for the next task — typically synthesizing Top 5 by AT.

You don't need to do anything special to "link" the sessions — the captures live on disk. I just need to know to read them.
