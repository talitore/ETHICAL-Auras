# Product Requirements Document: ETHICAL Auras

## 1. Introduction

*   **Product Name:** ETHICAL Auras (EA)
*   **Purpose:** A client-side Valheim mod designed to help players track essential buffs. It provides configurable visual and audio alerts when monitored buffs are detected as missing, ensuring players can maintain optimal buff uptime with minimal manual checking. This version is intentionally "paired down" for simplicity and core functionality.

## 2. Goals

*   Enhance player awareness of critical/desired buff statuses.
*   Reduce the need for players to manually check their active buffs.
*   Provide a simple, clear, and configurable alert system.
*   Ensure minimal performance impact on the game.

## 3. Target Audience

*   Valheim players who rely on specific buffs for survival, combat, or general gameplay efficiency (e.g., food buffs, Rested status, boss powers, mead effects).
*   Players looking for a lightweight, focused utility to manage their buffs.

## 4. Key Features (Minimum Viable Product - MVP)

*   **F1: Buff Configuration:**
    *   Users can define a list of specific buffs to monitor by their in-game names or identifiers.
    *   Configuration Method: Via a text-based configuration file (e.g., `com.talitore.ethicalauras.cfg`) managed by BepInEx's ConfigurationManager. This allows for easy editing outside the game.
*   **F2: Buff State Detection:**
    *   The mod will periodically check the player's character for the presence of the buffs specified in the configuration list.
    *   This will involve identifying how Valheim manages status effects and buffs on the `Player` object.
*   **F3: Visual Alert System:**
    *   When a monitored buff is *not* active on the player, a clear visual alert will be displayed on-screen.
    *   Alert Type (MVP): Simple text message (e.g., "MISSING: Rested").
    *   The alert will be removed once the buff becomes active.
*   **F4: Audio Alert System:**
    *   Simultaneously with the visual alert, when a monitored buff is not active, a distinct audio alert will be played.
    *   Sound Type (MVP): A simple, noticeable sound effect (e.g., a short chime or beep). The sound will be bundled with the mod or utilize a safe, common game sound.
    *   The audio alert for a specific missing buff should ideally play once per transition from active to inactive, or periodically if still missing, but not continuously spam.
*   **F5: Mod Enable/Disable:**
    *   The mod can be enabled or disabled via BepInEx's standard mod management features.

## 5. Technical Considerations

*   **Development Environment:**
    *   IDE: Visual Studio or JetBrains Rider.
    *   Framework: .NET Framework 4.8 (as recommended for Valheim modding).
*   **Core Dependencies:**
    *   **BepInExPack Valheim:** Essential for loading the mod into the game. ([Valheim Modding Wiki](https://github.com/Valheim-Modding/Wiki/wiki/Setting-Up-Mod-Development-Environment))
    *   **HarmonyX:** Used for patching game methods to safely access buff information without altering original game code. ([Valheim Modding Wiki](https://github.com/Valheim-Modding/Wiki/wiki/Setting-Up-Mod-Development-Environment))
*   **Game Code Interaction:**
    *   Requires decompilation of Valheim's `assembly_valheim.dll` (using tools like ILSpy or dnSpy, as mentioned in the guide) to understand how buffs (`StatusEffect`, `SEMan`, etc.) are managed and to identify buff names/IDs.
    *   Patches will likely target methods in the `Player` class or `SEMan` (Status Effect Manager) to read current active buffs.
*   **Configuration Management:**
    *   Utilize BepInEx's built-in `ConfigurationManager` for handling the `com.talitore.ethicalauras.cfg` file. This will manage settings for the list of buffs to track, and potentially future settings like alert positions or sound choices.
*   **UI for Alerts:**
    *   Visual (Text) Alerts: For MVP, implement using Unity's `OnGUI` for simplicity, or by hooking into existing game UI canvases if a more integrated look is desired and achievable without excessive complexity.
*   **Asset Bundling (if custom sounds/graphics are used later):**
    *   If custom assets are used beyond MVP, they would need to be bundled into an AssetBundle and loaded by the mod.

## 6. Out of Scope (for MVP)

*   In-game UI for live configuration of buffs or alert appearance (rely on config file).
*   Complex graphical icons or custom animations for alerts (use text).
*   Tracking buff timers/durations.
*   Server-synced configurations or alerts affecting other players.
*   Automatic detection of all available buffs in the game (user must specify).
*   Advanced customization of alert appearance (font, color, size beyond simple defaults).
*   Support for tracking debuffs (focus on positive buffs first).

## 7. Future Considerations (Post-MVP)

*   Allow users to customize the position, size, and color of visual alerts.
*   Allow users to choose from a selection of alert sounds or use their own sound files.
*   Option to display remaining duration for tracked buffs.
*   Simple in-game UI for adding/removing buffs from the tracked list.
*   Profiles for different characters or situations.