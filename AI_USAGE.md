# AI_USAGE.md

## Tools used
- **Claude Code** (Anthropic; model Claude Opus 5.5), run in the VS Code extension, with tool access to the repository, a shell and Docker.
- No other AI tools, code generators or AI review bots.

## What it was used for
- **Implementation, phase by phase.** The specification in `docs/` (requirements, frozen API contract, architecture, database, design, test plan, git plan) was prepared before coding and given to the AI as the source of truth. The AI then wrote the backend (.NET 10, MediatR CQRS, EF Core/PostgreSQL), the React frontend, the Dockerfiles and compose files, the tests and the smoke script, one planned git branch per phase.
- **Tests and verification.** The AI wrote the backend (xUnit against a real PostgreSQL) and frontend (Vitest, Testing Library, MSW) tests, ran everything in Docker, verified each phase from a fresh `git clone`, and "mutation-checked" the key tests (e.g. removed the `FOR UPDATE` row lock to prove the concurrency tests then fail).
- **Documentation and logging.** README, ASSUMPTIONS, and `todo.md`, which records every commit, decision and AI mistake.
- **Human control.** The AI stopped after every phase with a report (commits, test results, push status) and continued only after I said so. I reviewed the reports and repository, and made the decisions the AI couldn't, such as GitHub settings.

## Where the AI was wrong and how it was caught
The full list (24 entries) is in `todo.md` → "AI Mistakes & Fixes". The most important ones:

- **[API JSON]** It assumed ASP.NET Core rejects `"totalSeats":"120"` (a number in a string). The web defaults accept it, which breaks the contract. **Caught by:** a malformed-JSON test that got 200. **Fix:** strict number handling.
- **[Robustness]** The class-name validator let a NUL character through; PostgreSQL rejected it → **500**, a forbidden result. **Caught by:** the hostile-input robustness matrix. **Fix:** control characters are rejected with a 400.
- **[Routing]** `GET /classes/%20` returned 400 instead of 404 (non-nullable route parameter). **Caught by:** the malformed-id test theory. **Fix:** nullable route ids mapped to 404.
- **[Database query]** It ordered the class list by a member of a projected record, which EF Core can't translate → 500 on every `GET /classes`. **Caught by:** a curl smoke test against the Docker stack before committing.
- **[Privacy]** The student ID header was bound as a plain string, so ASP.NET's Trace logging would have written full birth registration numbers to the logs. **Caught by:** a logging test that captures every log category. **Fix:** a wrapper type that only ever prints the last 4 digits.
- **[Frontend]** The student-ID box kept its own copy of the text: when another tab changed the number, the app used the new number while the box still showed the old one. **Caught by:** a new tab-sync test. **Fix:** the box follows identity changes.
- **[Frontend]** The cancel dialog stayed open after the hold expired, offering an action that could no longer work. **Caught by:** an expiry-while-dialog-open test. **Fix:** the dialog closes when the hold leaves HELD.
- **[Tests]** Several tests were wrong in ways that made them pass or fail for the wrong reason: a fake clock set in the past, a `fetch` stub that answered every URL with the health response, a test that changed fake data after the refresh it meant to observe, a fake hold whose deadline moved on every poll. **Caught by:** failing runs and reading the tests back. **Fix:** each test rewritten; key tests mutation-checked.
- **[Build]** An EF Core version conflict warning (MSB3277) existed for a whole phase, hidden by incremental builds. **Caught by:** a full `--no-incremental` rebuild. **Fix:** pinned versions.
- **[Docker]** Every backend container printed `Error: libgssapi_krb5.so.2 …` at start-up from Phase 01 on; the AI's log checks only looked for `fail:`. **Caught by:** reading both instances' logs during the multi-instance work. **Fix:** disabled the unused GSS-encryption probe in the connection strings.
- **[Frontend layout]** At phone width (360 px) the class list was 395 px wide and scrolled sideways: the visually hidden column headings are absolutely positioned and escaped the table's scroll box. A 17-digit number in a heading also stuck out. jsdom can't measure layout, so no unit test could see it. **Caught by:** the clean-clone QA run in a real headless Chromium (Playwright), which measured every screen at 360 px. **Fix:** the scroll box became the headings' containing block, and long unbroken text now wraps.
- **[Project log]** It first wrote estimated times into the log instead of the real commit times. **Caught by:** comparing with `git log`. **Fix:** all times now come from git.
