# User Stories: ETHICAL Auras (EA)

This document outlines the user stories for the ETHICAL Auras (EA) mod, corresponding to the features defined in the Product Requirements Document (PRD).

## MVP User Stories

### US1: Configure Tracked Buffs (Relates to F1: Buff Configuration)

*   **As a** player,
*   **I want to** be able to specify a list of buffs I want to track by editing a configuration file,
*   **So that** I can customize the mod to alert me only for the buffs I care about (e.g., "Rested", "GP_Eikthyr", "CookedLoxMeat").

*Acceptance Criteria:*
*   A configuration file (e.g., `com.talitore.ethicalauras.cfg`) is created/used in the BepInEx config folder.
*   The player can list buff names (or agreed-upon identifiers) in this file.
*   The mod correctly reads and parses this list of buffs upon game start or config reload.
*   Invalid entries in the configuration are handled gracefully (e.g., logged, ignored) without crashing the game.

### US2: Receive Visual Alert for Missing Buff (Relates to F2 & F3: Buff State Detection & Visual Alert)

*   **As a** player,
*   **I want to** see a clear text message on my screen when a buff I've configured for tracking is not currently active on my character,
*   **So that** I am immediately aware I need to reapply or acquire it.

*Acceptance Criteria:*
*   When a tracked buff is missing, a text message (e.g., "MISSING: Rested") appears on screen.
*   The text message is easily noticeable but not overly intrusive to gameplay.
*   The message disappears if the buff becomes active.
*   If multiple tracked buffs are missing, alerts for each are displayed (or managed in a clear way).

### US3: Receive Audio Alert for Missing Buff (Relates to F2 & F4: Buff State Detection & Audio Alert)

*   **As a** player,
*   **I want to** hear a distinct sound effect when a buff I've configured for tracking is not currently active on my character,
*   **So that** I am audibly notified even if I'm not looking directly at the visual alert area.

*Acceptance Criteria:*
*   When a tracked buff is missing, a specific sound effect plays.
*   The sound is clear and distinct from common game sounds to avoid confusion.
*   The sound plays once when the buff is first detected as missing, or at a non-spammy interval if it remains missing.
*   The sound does not play if the visual alert is already shown and the sound has already played for that instance of the buff being missing (to avoid continuous noise).

### US4: Alerts Stop When Buff is Active (Relates to F3 & F4: Visual & Audio Alert System)

*   **As a** player,
*   **I want** visual and audio alerts for a specific missing buff to cease once that buff becomes active on my character,
*   **So that** I am not annoyed by outdated or unnecessary notifications.

*Acceptance Criteria:*
*   The on-screen text message for a previously missing buff is removed when the buff is gained.
*   No further audio alerts are played for that buff once it is active.

### US5: Enable/Disable Mod (Relates to F5: Mod Enable/Disable)

*   **As a** player,
*   **I want to** be able to enable or disable the ETHICAL Auras mod through standard mod management (e.g., BepInEx tools or mod manager UI),
*   **So that** I can control when the mod is active or turn it off if needed for troubleshooting or preference.

*Acceptance Criteria:*
*   The mod can be disabled, and when disabled, it performs no checks and shows no alerts.
*   The mod can be enabled, and when enabled, it functions as per the other user stories.
*   The mod correctly loads with BepInEx without errors.

## Future Considerations (Post-MVP User Stories)

### US6: Customize Visual Alert Appearance
*   **As a** player,
*   **I want to** be able to customize the position, and optionally the size and color, of the visual text alerts,
*   **So that** I can integrate them better with my UI setup and personal preferences.

### US7: Customize Audio Alert Sound
*   **As a** player,
*   **I want to** be able to choose from a selection of alert sounds or specify my own sound file,
*   **So that** I can select an audio alert that is most effective or pleasing to me.

### US8: See Buff Timers
*   **As a** player,
*   **I want to** optionally see the remaining duration of a tracked buff as part of its alert or when it's active,
*   **So that** I can better plan when to refresh it.

### US9: In-Game Buff Configuration
*   **As a** player,
*   **I want to** be able to add or remove buffs from the tracking list via a simple in-game UI,
*   **So that** I don't have to exit the game or manually edit a configuration file for quick changes.