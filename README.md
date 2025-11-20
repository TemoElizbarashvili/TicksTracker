# TickTracker

TickTracker is a small, privacy‑friendly Windows utility that keeps track of **which applications you actively use and for how long**. It’s useful if you want to understand where your time goes during the day without sending any data to third‑party services.

## What it does

- Tracks the **currently active (foreground) application** while you use your PC.
- Stores how long each app stays in focus and how many sessions you’ve had with it.
- Lets you explore your usage with a **clean WPF UI**: top apps, total time, filters, and date ranges.
- Runs a lightweight background tracker so you don’t have to remember to start it.

## How it works (high level)

- A background tracker (Windows service) periodically asks Windows **which window is currently active**.
- It records:
  - The process name (for example `chrome`, `code`, `outlook`)
  - Start and end timestamps for each “session”
- The data is stored in a local **SQLite database** at:
  - `C:\ProgramData\TickTracker\usage.db`
- Over time, old raw records are **aggregated** so the database stays small and fast, while still keeping long‑term totals.
- The TickTracker UI reads this local database and shows:
  - Total time per app
  - Session counts
  - First/last time an app was seen
  - Filters by minimum minutes, sessions, date ranges, and search

## Is TickTracker safe?

From the source code in this repository:

- It only records **process names and timestamps** (no keystrokes, no window contents, no screenshots).
- All data is stored **locally on your machine** in the SQLite file mentioned above.
- There is **no network code** in the tracker or UI – nothing is uploaded anywhere.
- You can stop or uninstall it at any time like any other Windows program.

That said, always treat time‑tracking tools as sensitive: your usage history can reveal your habits. You can delete all data by removing the `usage.db` file (with TickTracker closed) or uninstalling the app.

## Download TickTracker

👉 CHeck `Releases` And download latest version: `TickTrackerSetup.exe`.

The installer:

- Installs the background tracker and the TickTracker UI.
- Creates a Start Menu entry (and optional desktop shortcut).
- Adds a startup entry so tracking can begin automatically when you log in.

## For developers

- `TickTracker.Service` – background service that tracks the active window and writes to SQLite.
- `TickTracker.Shared` – shared tracking and data logic (EF Core models, DB context).
- `TickTracker.UI` – WPF application that visualises your app usage.
