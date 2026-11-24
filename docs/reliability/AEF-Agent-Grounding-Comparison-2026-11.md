# Agent Grounding Comparison — Pilot (2026-11-24)

**Result: on this pilot the grounding tools did not help. The plain agent did as well or better on every measure, for less money and time.** Three tasks, one run each, is too little to settle the question, but it is the first real evidence and it does not support the product goal yet.

## What was run

The same loose prompt was given to headless Claude Code (`claude -p`, model `sonnet`) twice: once with no MCP servers, and once with only the AEF server and an instruction to call `aef_refine_prompt` first and `aef_check_change` at the end.

Each task is a real past commit. The prompt is the commit subject; the starting point is a throwaway clone at the commit's parent; the commit's own file list is the reference for which files should change. For the AEF arm the clone was indexed by tokensave at the parent commit, so the index did not contain the answer. User-level settings and plugins were excluded from both arms. Script: `tools/grounding-comparison/run.ps1`.

A headless run has no user, so live back-and-forth cannot be measured. The proxies are: real files changed, files changed that the real commit did not touch, paths named in the final answer that do not exist, tool calls, cost and time.

## Results

| Task | Arm | Real files changed | Extra files | Paths that do not exist | Tool calls | Cost | Time |
|---|---|---|---|---|---|---|---|
| marksy-api `2e30a5e` — refuse only recent gaps in the T4 window | plain | 2 of 2 | 0 | 0 | 15 | $0.146 | 65 s |
| | with AEF | 1 of 2 | 0 | 0 | 14 | $0.240 | 80 s |
| marksy-os `d28fc31` — read the setup QR from a picked image | plain | 2 of 2 | 0 | 0 | 9 | $0.158 | 52 s |
| | with AEF | 2 of 2 | 0 | 0 | 11 | $0.167 | 72 s |
| Ferret `ec61546` — fill in the completion row now the PR is merged | plain | 1 of 1 | 0 | 0 | 7 | $0.090 | 35 s |
| | with AEF | 1 of 1 | 0 | 0 | 8 | $0.100 | 64 s |
| **Total** | plain | **5 of 5** | 0 | 0 | 31 | **$0.394** | 152 s |
| | with AEF | **4 of 5** | 0 | 0 | 33 | **$0.507** | 216 s |

Neither arm named a path that does not exist, and neither changed a file the real commit did not touch.

## Why it did not help

In the marksy-api task the brief the agent received was accurate but not useful:

- **Verified facts: none.** The prompt names no file or symbol, so there was nothing to verify.
- **Candidates missed the right file.** Nine search matches were listed, all marked unconfirmed. `app/strategies/picks.py`, the file that needed to change, was not among them. The agent found it with its own searches, as the plain agent did.
- **Open questions: none.** The prompt has a real ambiguity — how recent is "recent"? — and both agents silently picked a number (60 sessions with AEF, 20 without). Spotting that needs judgement about meaning, which a deterministic brief does not have.
- **The one missed file was the test.** The plain agent added a test; the AEF-assisted agent did not, even though the brief listed the test file as covering tests.

The grounding tools add two tool calls and several thousand characters to every task. On prompts like these, a capable agent already locates the code by searching.

## Where the tools are expected to matter, not yet measured

- A prompt that names something that does not exist. The brief turns that into a question with the closest real matches, instead of the agent building on it. Covered by tests, not by a with/without run.
- A prompt that names files. They are confirmed on disk and the check step then flags changes outside them.
- Weaker or cheaper agent models, and larger repositories, where the agent's own search is less reliable.

## Limits of this pilot

- Three tasks, one run per arm, one model. No repeat runs, so run-to-run variation is unknown.
- Commit subjects are terse and written after the fact; they are not typical user prompts.
- Bash was not permitted in either arm, so neither could run tests.
- Codex was not run. It is installed, but giving it the AEF server needs a change to the user's Codex configuration, which was not made.
- The full 15-task set was not run. A default headless `claude -p` call costs about $0.77 before doing any work; with user settings excluded and `sonnet`, these six runs cost $0.90 in total. The script takes `-Only` and `-MaxBudgetUsd` to run the rest.
