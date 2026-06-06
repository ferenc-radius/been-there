# ADR-0010: In-app GPX export guidance (AllTrails primary, Locus Map secondary)

**Status:** Accepted

## Context

Users may not know how to export their recorded walks from the apps they use in the field. Without clear in-app guidance, the import drag-and-drop surface is useless because users arrive with no files. **AllTrails is the primary source in practice.** Locus Map is supported as a secondary option for users who record with it.

## Decision

The import UI must include a collapsible **"How do I get my GPX files?"** help section, shown by default on first visit and collapsible thereafter. AllTrails steps are shown first and most prominently.

### AllTrails (primary)
**Mobile (iOS / Android)**
1. Open the app → **Profile** → **Completed** tab.
2. Tap a recorded activity → **⋮** → **Export** → choose **GPX**.
3. Share or save to Files / Downloads.

**Web (alltrails.com)**
1. Profile → Completed → click a recording → **Download GPX**.

### Locus Map 4 (secondary, Android only)
**Single track**
1. Open **My Library** → tap your track.
2. In the detail panel tap **⋮** → **Export**.
3. Select format **GPX** → tap **EXPORT**.
4. Save to device storage, then transfer the file to BeenThere.

**Multiple tracks**
1. **My Library** → folder → topbar menu → **Select**.
2. Check the tracks → tool menu (bottom-right) → **Export** → **GPX** → **EXPORT**.

> ⚠️ Since September 2025 Locus Map no longer supports direct export to Google Drive. Export to device storage and transfer manually (e-mail, cable, AirDrop, etc.).

> Tracks are also accessible via USB at `Android/data/menion.android.locus/files/tracks/`.

### Notes shown to the user
- Both apps export **GPX 1.1**, which BeenThere imports natively.
- Locus Map exports include heart rate, cadence, and power data if your device recorded them.
- After exporting, drag the `.gpx` files directly onto the import area below.

## Implementation notes

- The help section is a Blazor `<details>` / `<summary>` element (no JS required) in the import page component.
- Copy is stored in a `.razor` component or a resource file — not hardcoded in a service — so it can be updated without touching business logic.
- The section is hidden (collapsed) after the user successfully completes their first import (`user_preferences.importHelpDismissed: true`).

## Consequences

- `user_preferences` jsonb column (see ADR-0007) gains a new key `importHelpDismissed`.
- No new dependencies; plain HTML `<details>` is sufficient.
- Future source apps (Garmin Connect, Komoot, Strava) can be added as additional accordion entries without structural changes.
