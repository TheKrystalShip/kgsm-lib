# The maintenance grammar

`Instance.MaintenanceWindows` is one packed value — `daily@05:00/backup;weekly.sun@04:00/update,restart`
— and this directory is the **only** place in the ecosystem that reads it. `MaintenanceWindowParser` is
the grammar's one implementation and its one validator: kgsm-api refuses a bad expression with the
error it produces and kgsm-scheduler fires on the windows it returns, so the two cannot disagree about
what an expression means. It lives in the library for the same reason `KgsmEventCatalog` does.

- **A parse failure is data, not an exception.** Nothing throws for anything a person can type. `Parse`
  returns one `MaintenanceWindow` per expression written, **each with its own `IsValid` and `Error`** —
  an unreadable window disables itself and leaves the instance's other windows firing, and it comes back
  invalid rather than dropped, because a silent drop is indistinguishable from a window nobody
  configured.
- **A window's id is its schedule expression, canonicalised.** Nothing stores it. Duplicate detection,
  postpone, skip and announcement bookkeeping all key on it, and editing the schedule deliberately
  produces a *different* window — which is what makes anything announced about the old one retractable.
  An interval therefore keeps the unit it was written with: rewriting `120m` as `2h` would silently
  re-identify the window.
- **Canonical task order is fixed** (`backup` → `update` → `restart`) and the written order carries no
  meaning. The order is a property of what the tasks are: a backup taken after an update archives the
  new build instead of the rollback point.
- **`ScheduleClock` answers "strictly after this instant", never "is it due now".** A caller stores the
  target it computed on an earlier tick rather than recomputing and comparing to the present.
  Appointments compare in UTC rather than on the wall clock, which is what keeps them right through a
  daylight-saving transition — a local comparison has two candidates in autumn and none in spring.
  Intervals are epoch-aligned to whole multiples from `1970-01-01T00:00Z`, so nothing is anchored at
  install time and every host answers identically.
- **An invalid window has no next fire.** `NextFire` returns null for it, and that null beside
  `IsValid = false` is what distinguishes it from a window that is simply not due.
