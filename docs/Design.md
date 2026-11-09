# Design.md — UI/UX

The brief: *"The UI need not be beautiful. What matters is that it works correctly and is easy to use."* Optimize for **clarity, honesty about state, and zero dead ends**.

## 1. Principles

1. **Always show the truth, live.** Seat numbers and hold status refresh on their own (R9, R10). Show when data is stale.
2. **One obvious next action per screen.** Disable buttons that can't work and say why.
3. **Errors are sentences a parent understands** (R12) — what happened and what to do next. Never a blank page, never only a console error, never raw codes alone.
4. **Identity is always visible** at the top, so parents know whose application they're acting on.
5. **Accessible by default:** semantic HTML, labels, keyboard operable, status never conveyed by color alone.

## 2. Layout

```
┌──────────────────────────────────────────────────────────────────────────┐
│  🏫 School Admission                                                       │
│  Birth Registration No. [ 20152691234567890 ] ✓ Saved   [Change]           │
│  Classes  |  My Applications  |  Admin: Create Class                       │
├──────────────────────────────────────────────────────────────────────────┤
│  ⚠ Can't reach the server. Showing data from 12 s ago. Retrying…   (banner │
│     only when polling fails)                                               │
├──────────────────────────────────────────────────────────────────────────┤
│                              <page content>                                │
└──────────────────────────────────────────────────────────────────────────┘
```
Max content width ~960 px, single column, responsive down to 360 px (table scrolls horizontally inside its own container).

## 3. Student ID bar (every page)

| State | Display |
|-------|---------|
| Empty | Input with placeholder `17-digit birth registration number`; helper text: "Enter the student's birth registration number to apply or see applications." |
| Typing invalid | Inline error: "Must be exactly 17 digits (you entered 15)." |
| Valid | Input shows the number; "✓ Saved" hint; value persisted to `localStorage` (`sas.studentId`). |

Behavior: accepts ASCII and Bengali digits (০১২৩৪৫৬৭৮৯ → 0123456789), strips spaces and dashes, `inputMode="numeric"`, `maxLength` ~ 25 before normalization. Saved only when valid. Changing the ID refreshes "My Applications" and Apply-button states.

## 4. Screens

### 4.1 Class List (`/`) — R10

```
Classes                                         Updated 2 s ago  ⟳ (auto every 3 s)
┌────────────────────────────┬───────┬──────┬───────────┬───────────┬─────────────────────┐
│ Class                      │ Total │ Held │ Confirmed │ Available │                     │
├────────────────────────────┼───────┼──────┼───────────┼───────────┼─────────────────────┤
│ Class 6 - Morning Shift    │  120  │  15  │    80     │    25     │ [ Apply ]           │
│ Class 6 - Day Shift        │  120  │   2  │   118     │     0     │ [ Full ] (disabled) │
│ Class 7 - Morning Shift    │   90  │   0  │     1     │    89     │ [ View application ]│
└────────────────────────────┴───────┴──────┴───────────┴───────────┴─────────────────────┘
```

Apply button rules:
| Condition | Button |
|-----------|--------|
| No valid student ID | `Apply` disabled, tooltip/hint "Enter a birth registration number first" |
| Student has active HELD hold in this class | `View application` → `/applications/{holdId}` |
| Student CONFIRMED in this class | `Admitted ✓` → `/applications/{holdId}` |
| `availableSeats == 0` | `Full` disabled |
| Request in flight | `Applying…` disabled |
| Otherwise | `Apply` |

States: loading skeleton ("Loading classes…"); empty ("No classes yet. An admin can create one from **Admin: Create Class**."); error (ErrorAlert + Retry). On Apply success → navigate to Application page.

### 4.2 Create Class (`/admin/classes/new`)

```
Create a class (school admin)
Each shift is its own class, e.g. "Class 6 - Morning Shift".

Class name     [ Class 6 - Morning Shift            ]
Total seats    [ 120 ]
               [ Create class ]

✓ "Class 6 - Morning Shift" created with 120 seats.  [View classes]
```
Client-side validation mirrors the server (name 1–100 chars after trim; seats integer 1–10000) — but server errors are still displayed (e.g., duplicate name). Form clears on success.

### 4.3 Application (`/applications/:holdId`) — R9

```
Application — Class 6 - Morning Shift

Status:   [ HELD ]  Seat held — awaiting payment
Time left:  07:42        (expires at 10:10:00 AM)

[ Pay Admission Fee ]   [ Cancel application ]

ℹ Paying is simulated in this demo — clicking the button counts as a successful payment.
```

| Effective status | Badge | Message | Pay | Cancel |
|------------------|-------|---------|-----|--------|
| HELD (time left) | `HELD` (amber) | "Seat held — pay before the timer ends." | enabled | enabled |
| HELD but countdown hit 0 | `EXPIRED` (grey) | "Time ran out. The seat was released. You can apply again if seats are available." | **disabled** | hidden |
| CONFIRMED | `CONFIRMED` (green) | "Admission confirmed. 🎉" | hidden | hidden |
| EXPIRED | `EXPIRED` (grey) | as above + link "Back to classes" | disabled | hidden |
| CANCELLED | `CANCELLED` (grey) | "You cancelled this application." + link | hidden | hidden |

- Countdown format `mm:ss` (or `h:mm:ss` if ≥ 1 h); under 60 s show in red with "Hurry!". Uses `aria-live="polite"` announcements only at 60 s and 0 s (not every tick).
- The badge always shows the literal status word (`EXPIRED`), satisfying R9 exactly.
- Cancel asks for confirmation ("Cancel this application? Your seat will be released immediately.").
- If the current student ID ≠ hold owner: show the status read-only with "This application belongs to a different birth registration number."
- Pay while expired (race) → server 409 `HOLD_EXPIRED` → message + status flips to EXPIRED.

### 4.4 My Applications (`/my-applications`) — R11

```
My applications (20152691234567890)
┌────────────────────────────┬─────────────┬────────────┬──────────┐
│ Class                      │ Status      │ Time left  │          │
├────────────────────────────┼─────────────┼────────────┼──────────┤
│ Class 6 - Morning Shift    │ HELD        │ 07:42      │ [Open]   │
│ Class 7 - Morning Shift    │ CONFIRMED   │ —          │ [Open]   │
│ Class 5 - Day Shift        │ EXPIRED     │ —          │ [Open]   │
└────────────────────────────┴─────────────┴────────────┴──────────┘
```
Active (HELD) first, then by newest. No ID → prompt to enter one. Empty → "No applications yet. Go to **Classes** to apply."

## 5. Error copy (single source: `src/i18n/messages.ts`)

| Code / kind | Message shown |
|-------------|---------------|
| `NO_SEATS_AVAILABLE` | "Sorry, all seats in this class are taken right now. Seats may open up if other applicants don't pay in time." |
| `ACTIVE_HOLD_EXISTS` | "You already have an active application for this class." + **Open it** link |
| `ALREADY_ADMITTED` | "This student is already admitted to this class." + **View** link |
| `HOLD_EXPIRED` | "This application has expired, so it can't be paid or cancelled. You can apply again if seats are available." |
| `HOLD_CANCELLED` | "This application was cancelled and can't be paid." |
| `HOLD_ALREADY_CONFIRMED` | "This admission is already confirmed and can't be cancelled here." |
| `HOLD_NOT_OWNED` | "This application belongs to a different birth registration number. Check the number at the top of the page." |
| `INVALID_STUDENT_ID` | "Please enter a valid 17-digit birth registration number at the top of the page." |
| `CLASS_NAME_EXISTS` | "A class with this name already exists. Use a different name (e.g., add the shift)." |
| `VALIDATION_FAILED` | Field messages from `errors`; fallback "Some information is missing or invalid. Please check the form." |
| `MALFORMED_JSON` | "Something was wrong with the information sent. Please try again." |
| `CLASS_NOT_FOUND` | "This class no longer exists." |
| `HOLD_NOT_FOUND` / `NOT_FOUND` | "We couldn't find this application. It may have been entered incorrectly." |
| `SERVICE_UNAVAILABLE` / any 5xx | "The server is having trouble right now. Please try again in a moment." |
| `network` | "Can't reach the server. Check your internet connection — we'll keep retrying." |
| `timeout` | "The server is taking too long to respond. Please try again." |
| unknown | "Something went wrong. Please try again." (log details with `console.error` *in addition*) |

Unexpected render crash → `ErrorBoundary`: "Something went wrong on this page." + **Reload** + **Go to classes**.

## 6. Visual tokens (plain CSS)

```css
:root {
  --bg: #ffffff; --fg: #1f2933; --muted: #616e7c; --border: #d9dee3;
  --primary: #1d4ed8; --primary-fg: #ffffff;
  --held: #b45309; --held-bg: #fef3c7;
  --confirmed: #15803d; --confirmed-bg: #dcfce7;
  --inactive: #4b5563; --inactive-bg: #e5e7eb;
  --danger: #b91c1c; --danger-bg: #fee2e2;
  --radius: 8px; --gap: 12px;
  font-family: system-ui, "Noto Sans Bengali", sans-serif;
}
@media (prefers-color-scheme: dark) {
  :root { --bg: #111827; --fg: #f3f4f6; --muted: #9ca3af; --border: #374151; }
}
```
Buttons ≥ 40 px tall; visible focus ring; disabled buttons have reduced contrast **and** a text reason.

## 7. Accessibility checklist

- Every input has a `<label>`; errors linked via `aria-describedby`.
- Tables use `<th scope="col">`.
- Alerts use `role="alert"`; the connection banner `role="status"`.
- All actions reachable by keyboard; dialog traps focus and closes on Esc.
- Times displayed in the browser's local time zone; `expiresAt` also in `title` attribute as ISO.
