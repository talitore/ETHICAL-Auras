using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // Added for GUI and other Unity functionalities
using System.Reflection;
using System.IO;

namespace ETHICAL_Auras
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class EthicalAurasPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.talitore.ethical-auras";
        public const string PluginName = "ETHICAL Auras";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource Log;
        private Harmony harmony;
        private AssetBundle ethicalAurasBundle; // Added to store the loaded asset bundle

        // Configuration Entries
        public static ConfigEntry<string> TrackedBuffsConfig;
        public static List<string> TrackedBuffNames = new List<string>();

        public static ConfigEntry<bool> EnableAudioAlertsConfig;
        public static ConfigEntry<bool> LoopAudioAlertConfig;
        public static ConfigEntry<string> SelectedAudioClipNameConfig;

        // Zone awareness configuration
        public static ConfigEntry<bool> EnableZoneAwarenessConfig;
        public static ConfigEntry<bool> MeadowsZoneConfig;
        public static ConfigEntry<bool> BlackForestZoneConfig;
        public static ConfigEntry<bool> SwampZoneConfig;
        public static ConfigEntry<bool> MountainZoneConfig;
        public static ConfigEntry<bool> PlainsZoneConfig;
        public static ConfigEntry<bool> MistlandsZoneConfig;
        public static ConfigEntry<bool> OceanZoneConfig;
        public static ConfigEntry<bool> AshlandsZoneConfig;
        public static ConfigEntry<bool> DeepNorthZoneConfig;

        // Icon display configuration
        public static ConfigEntry<float> IconSizeConfig;
        public static ConfigEntry<string> IconPositionConfig;
        public static ConfigEntry<float> IconSpacingConfig;

        // List to store buffs that are currently missing
        private List<StatusEffect> currentlyMissingBuffs = new List<StatusEffect>();

        private AudioClip missingBuffAudioClip;

        private float iconFlashTimer = 0f; // For flashing effect
        private const float ICON_FLASH_SPEED = 2f; // Controls flash speed (higher is faster)
        private const float ICON_SIZE = 40f; // Size of the buff icons
        private const float ICON_PADDING = 5f; // Padding between icons

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) is loading!");

            // Initialize Configuration
            TrackedBuffsConfig = Config.Bind(
                "General", // Section
                "TrackedBuffs", // Key
                "Rested,CampFire,Potion_poisonresist", // Default value (example buffs)
                "Comma-separated list of buff names to track (e.g., Rested,GP_Eikthyr,CookedLoxMeat). Use the internal buff names."); // Description

            EnableAudioAlertsConfig = Config.Bind(
                "AudioAlerts", // Section
                "EnableAudioAlerts", // Key
                false, // Default value
                "Enable or disable audio alerts for missing buffs."); // Description

            LoopAudioAlertConfig = Config.Bind(
                "AudioAlerts", // Section
                "LoopAudioAlert", // Key
                true, // Default value: looping is enabled
                "Enable or disable looping for the audio alert. If false, the sound plays once per missing buff."); // Description

            SelectedAudioClipNameConfig = Config.Bind(
                "AudioAlerts", // Section
                "SelectedAudioClipName", // Key
                "scream", // Default value
                new ConfigDescription(
                    "The name of the audio clip to play. Send me (talitore) clips to be added.",
                    new AcceptableValueList<string>("scream", "creatine"))
                ); // Description

            // Initialize icon display configuration
            IconSizeConfig = Config.Bind(
                "Display", // Section
                "IconSize", // Key
                40f, // Default value
                new ConfigDescription(
                    "Size of the buff icons in pixels",
                    new AcceptableValueRange<float>(20f, 200f)
                )
            );

            IconPositionConfig = Config.Bind(
                "Display", // Section
                "IconPosition", // Key
                "TopCenter", // Default value
                new ConfigDescription(
                    "Position of the buff icons on screen",
                    new AcceptableValueList<string>("TopLeft", "TopCenter", "TopRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight")
                )
            );

            IconSpacingConfig = Config.Bind(
                "Display", // Section
                "IconSpacing", // Key
                5f, // Default value
                new ConfigDescription(
                    "Spacing between buff icons in pixels",
                    new AcceptableValueRange<float>(0f, 50f)
                )
            );

            // Listen for changes to the display configuration
            IconSizeConfig.SettingChanged += (sender, args) => Log.LogInfo($"Icon size changed to: {IconSizeConfig.Value}");
            IconPositionConfig.SettingChanged += (sender, args) => Log.LogInfo($"Icon position changed to: {IconPositionConfig.Value}");
            IconSpacingConfig.SettingChanged += (sender, args) => Log.LogInfo($"Icon spacing changed to: {IconSpacingConfig.Value}");

            ParseTrackedBuffs();
            // Listen for changes to the configuration and update if it's modified while the game is running.
            TrackedBuffsConfig.SettingChanged += (sender, args) => ParseTrackedBuffs();
            EnableAudioAlertsConfig.SettingChanged += (sender, args) => Log.LogInfo($"Audio alerts {(EnableAudioAlertsConfig.Value ? "enabled" : "disabled")}.");
            LoopAudioAlertConfig.SettingChanged += (sender, args) => Log.LogInfo($"Audio alert looping {(LoopAudioAlertConfig.Value ? "enabled" : "disabled")}.");
            SelectedAudioClipNameConfig.SettingChanged += (sender, args) =>
            {
                Log.LogInfo($"Selected audio clip name changed to: {SelectedAudioClipNameConfig.Value}. Reloading clip.");
                LoadSelectedAudioClip(ethicalAurasBundle);
            };

            // Initialize zone awareness configuration
            EnableZoneAwarenessConfig = Config.Bind(
                "ZoneAwareness", // Section
                "EnableZoneAwareness", // Key
                true, // Default value
                "Enable or disable zone-specific buff tracking." // Description
            );

            MeadowsZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "MeadowsZone", // Key
                false, // Default value
                "Track buffs in Meadows biome." // Description
            );

            BlackForestZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "BlackForestZone", // Key
                false, // Default value
                "Track buffs in Black Forest biome." // Description
            );

            SwampZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "SwampZone", // Key
                true, // Default value
                "Track buffs in Swamp biome." // Description
            );

            MountainZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "MountainZone", // Key
                false, // Default value
                "Track buffs in Mountain biome." // Description
            );

            PlainsZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "PlainsZone", // Key
                false, // Default value
                "Track buffs in Plains biome." // Description
            );

            MistlandsZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "MistlandsZone", // Key
                false, // Default value
                "Track buffs in Mistlands biome." // Description
            );

            OceanZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "OceanZone", // Key
                false, // Default value
                "Track buffs in Ocean biome." // Description
            );

            AshlandsZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "AshlandsZone", // Key
                false, // Default value
                "Track buffs in Ashlands biome." // Description
            );

            DeepNorthZoneConfig = Config.Bind(
                "ZoneAwareness", // Section
                "DeepNorthZone", // Key
                false, // Default value
                "Track buffs in Deep North biome." // Description
            );

            // Listen for changes to zone configuration
            EnableZoneAwarenessConfig.SettingChanged += (sender, args) =>
                Log.LogInfo($"Zone awareness {(EnableZoneAwarenessConfig.Value ? "enabled" : "disabled")}.");

            // Initialize Harmony for patching
            harmony = new Harmony(PluginGUID);
            // In future steps, we will add harmony.PatchAll(); here once we have patches.
            // For US1, we are only setting up configuration.

            // Load the AssetBundle
            Assembly assembly = Assembly.GetExecutingAssembly();
            // Assuming the asset bundle is named 'ethical_auras'.
            string resourceName = "ETHICAL_Auras.ethical_auras";
            Stream stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                Log.LogError($"Failed to load embedded resource stream: {resourceName}. Make sure the resource exists and is marked as an Embedded Resource.");
                return; // Or handle error appropriately
            }

            ethicalAurasBundle = AssetBundle.LoadFromStream(stream);
            stream.Close(); // It's good practice to close the stream after loading

            if (ethicalAurasBundle == null)
            {
                Log.LogError($"Failed to load AssetBundle from stream: {resourceName}.");
                return; // Or handle error appropriately
            }

            // Load the audio clip
            LoadSelectedAudioClip(ethicalAurasBundle); // Call the new method to load the clip

            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) has loaded. Currently tracking: {string.Join(", ", TrackedBuffNames)}");
            Log.LogInfo($"Audio alerts: {(EnableAudioAlertsConfig.Value ? "Enabled" : "Disabled")}, Loop: {LoopAudioAlertConfig.Value}, Clip: {SelectedAudioClipNameConfig.Value}");
        }

        private void LoadSelectedAudioClip(AssetBundle bundle)
        {
            if (bundle == null)
            {
                Log.LogError("AssetBundle is null. Cannot load audio clip.");
                missingBuffAudioClip = null;
                return;
            }

            string clipName = SelectedAudioClipNameConfig.Value;
            if (string.IsNullOrEmpty(clipName))
            {
                Log.LogWarning("Selected audio clip name is empty. No audio clip will be loaded.");
                missingBuffAudioClip = null;
                return;
            }

            AudioClip audioClip = bundle.LoadAsset<AudioClip>(clipName);

            if (audioClip == null)
            {
                Log.LogError($"Failed to load AudioClip '{clipName}' from AssetBundle. Ensure the clip exists in the bundle.");
                missingBuffAudioClip = null; // Ensure it's null if loading fails
            }
            else
            {
                missingBuffAudioClip = audioClip;
                Log.LogInfo($"Successfully loaded '{clipName}' audio clip from asset bundle.");
            }
        }

        private void ParseTrackedBuffs()
        {
            var currentConfigString = TrackedBuffsConfig.Value ?? "";
            TrackedBuffNames = currentConfigString
                .Split(',')
                .Select(buffName => buffName.Trim())
                .Where(buffName => !string.IsNullOrEmpty(buffName))
                .ToList();

            if (Log != null) // Logger might not be initialized if called very early or in tests
            {
                Log.LogInfo("Tracked buffs configuration reloaded.");
                if (TrackedBuffNames.Any())
                {
                    Log.LogInfo($"Currently configured to track: {string.Join(", ", TrackedBuffNames)}");
                }
                else
                {
                    Log.LogInfo("No buffs are currently configured for tracking.");
                }
            }
        }

        private bool IsCurrentZoneEnabled()
        {
            if (!EnableZoneAwarenessConfig.Value)
                return true; // If zone awareness is disabled, always return true

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null)
                return false;

            Heightmap.Biome currentBiome = localPlayer.GetCurrentBiome();

            switch (currentBiome)
            {
                case Heightmap.Biome.Meadows:
                    return MeadowsZoneConfig.Value;
                case Heightmap.Biome.BlackForest:
                    return BlackForestZoneConfig.Value;
                case Heightmap.Biome.Swamp:
                    return SwampZoneConfig.Value;
                case Heightmap.Biome.Mountain:
                    return MountainZoneConfig.Value;
                case Heightmap.Biome.Plains:
                    return PlainsZoneConfig.Value;
                case Heightmap.Biome.Mistlands:
                    return MistlandsZoneConfig.Value;
                case Heightmap.Biome.Ocean:
                    return OceanZoneConfig.Value;
                case Heightmap.Biome.AshLands:
                    return AshlandsZoneConfig.Value;
                case Heightmap.Biome.DeepNorth:
                    return DeepNorthZoneConfig.Value;
                default:
                    return true; // For any other biome, default to true
            }
        }

        private void Update()
        {
            if (!IsCurrentZoneEnabled())
            {
                // If we're in a disabled zone, clear any active alerts
                if (currentlyMissingBuffs.Any())
                {
                    currentlyMissingBuffs.Clear();
                    // Stop any playing sounds
                    GameObject soundGo = GameObject.Find("TempLoopingAlertSound");
                    if (soundGo != null)
                    {
                        AudioSource audioSource = soundGo.GetComponent<AudioSource>();
                        if (audioSource != null && audioSource.isPlaying)
                        {
                            audioSource.Stop();
                        }
                    }
                }
                return;
            }

            CheckActiveBuffs();

            // Additional logic to stop the looping sound if all tracked buffs are active
            if (EnableAudioAlertsConfig.Value && currentlyMissingBuffs.Count == 0)
            {
                GameObject soundGo = GameObject.Find("TempLoopingAlertSound");
                if (soundGo != null)
                {
                    AudioSource audioSource = soundGo.GetComponent<AudioSource>();
                    if (audioSource != null && audioSource.isPlaying && audioSource.loop)
                    {
                        audioSource.Stop();
                        Log.LogInfo("All tracked buffs are active. Stopped looping alert sound.");
                    }
                }
            }

            // Ensure sounds are stopped if audio alerts are globally disabled
            if (!EnableAudioAlertsConfig.Value)
            {
                GameObject soundGo = GameObject.Find("TempLoopingAlertSound");
                if (soundGo != null)
                {
                    AudioSource audioSource = soundGo.GetComponent<AudioSource>();
                    // If alerts are off, and the source is playing, stop it.
                    // We don't need to check audioSource.loop here, because if alerts are off, no sound should play.
                    if (audioSource != null && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                        Log.LogInfo("Audio alerts globally disabled. Stopped active alert sound.");
                    }
                }
            }
        }

        private void CheckActiveBuffs()
        {
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || localPlayer.GetSEMan() == null)
            {
                if (currentlyMissingBuffs.Any()) // Clear only if it had items, to avoid new list allocation if already empty
                {
                    currentlyMissingBuffs.Clear();
                }
                return;
            }

            SEMan statusEffectManager = localPlayer.GetSEMan();
            List<StatusEffect> missingBuffsFoundThisFrame = new List<StatusEffect>();

            foreach (string buffName in TrackedBuffNames)
            {
                if (!statusEffectManager.HaveStatusEffect(buffName.GetStableHashCode()))
                {
                    StatusEffect se = ObjectDB.instance.GetStatusEffect(buffName.GetStableHashCode());
                    if (se != null && se.m_icon != null) // Ensure status effect and its icon exist
                    {
                        missingBuffsFoundThisFrame.Add(se);
                    }
                    else if (se == null)
                    {
                        Log.LogWarning($"Could not find StatusEffect for tracked buff name: {buffName}");
                    }
                    // Not logging if se.m_icon is null, as some buffs might legitimately not have icons (though unlikely for player buffs)
                }
            }

            if (EnableAudioAlertsConfig.Value)
            {
                // We need to compare based on name or hash since StatusEffect instances might differ.
                var missingBuffNamesFoundThisFrame = missingBuffsFoundThisFrame.Select(se => se.name).ToList();
                var currentMissingBuffNames = currentlyMissingBuffs.Select(se => se.name).ToList();

                foreach (StatusEffect newMissingSE in missingBuffsFoundThisFrame)
                {
                    if (!currentMissingBuffNames.Contains(newMissingSE.name)) // If it wasn't missing last frame but is now
                    {
                        PlayMissingBuffSoundLogic(newMissingSE.m_name, localPlayer.transform);
                    }
                }
            }

            currentlyMissingBuffs = missingBuffsFoundThisFrame;
        }

        private void PlayMissingBuffSoundLogic(string buffName, Transform anchor)
        {
            // If no custom audio clip is loaded (e.g., due to misconfiguration or empty SelectedAudioClipNameConfig),
            // then don't attempt to play anything.
            if (missingBuffAudioClip == null)
            {
                // The warning about missingBuffAudioClip being null is already logged during LoadSelectedAudioClip
                // if it fails to load, or if SelectedAudioClipNameConfig is empty.
                // A debug log here could be useful if trying to trace why sound isn't playing.
                // Log.LogDebug($"Skipping sound for missing buff {buffName} as missingBuffAudioClip is not available.");
                return;
            }

            if (AudioMan.instance != null && anchor != null)
            {
                // For looping sound, we'll need to manage the AudioSource instance.
                // This is a simplified approach for testing; a more robust solution
                // would involve creating a dedicated AudioSource on the player or a manager
                // and controlling its loop property and playback.

                // Find an existing temporary audio source or create a new one if multiple sounds can loop.
                // For now, let's assume only one sound loops at a time for simplicity in this example.
                // A more robust system would pool or manage these sources.
                GameObject soundGo = GameObject.Find("TempLoopingAlertSound");
                AudioSource audioSource;

                if (soundGo == null)
                {
                    soundGo = new GameObject("TempLoopingAlertSound");
                    audioSource = soundGo.AddComponent<AudioSource>();
                    // Configure the AudioSource for 2D sound
                    audioSource.spatialBlend = 0f; // 0f for 2D, 1f for 3D
                    // Remove or comment out 3D specific settings:
                    // audioSource.minDistance = 1f;
                    // audioSource.maxDistance = 30f;
                    // audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                }
                else
                {
                    audioSource = soundGo.GetComponent<AudioSource>();
                    // Ensure spatialBlend is set correctly if reusing an existing AudioSource that might have been 3D
                    if (audioSource != null) audioSource.spatialBlend = 0f;
                }

                soundGo.transform.position = anchor.position; // Position still set, though less relevant for 2D

                if (missingBuffAudioClip != null)
                {
                    if (!audioSource.isPlaying || audioSource.clip != missingBuffAudioClip)
                    {
                        audioSource.clip = missingBuffAudioClip;
                        audioSource.loop = LoopAudioAlertConfig.Value; // Use the config setting for looping

                        // Check if the specific "scream" clip is selected and loaded
                        if (SelectedAudioClipNameConfig.Value == "scream" && missingBuffAudioClip.name.ToLowerInvariant().Contains("scream"))
                        {
                            audioSource.time = 0.5f; // Start 0.5 seconds into the clip
                            Log.LogInfo("Starting 'scream' clip from 0.5s offset.");
                        }
                        else
                        {
                            audioSource.time = 0f; // Ensure other clips start from the beginning
                        }

                        audioSource.Play();
                        Log.LogInfo($"Started {(LoopAudioAlertConfig.Value ? "looping" : "playing one-shot")} '{SelectedAudioClipNameConfig.Value}' sound for missing buff: {buffName}");
                    }
                }
                else
                {
                    // This case should be rare if the top-level check `if (missingBuffAudioClip == null)` is working.
                    Log.LogWarning($"missingBuffAudioClip became null unexpectedly before playing for buff: {buffName}. This should not happen.");
                }
            }
            else
            {
                if (AudioMan.instance == null) Log.LogWarning($"AudioMan.instance is null. Cannot play missing buff sound for {buffName}.");
                if (anchor == null) Log.LogWarning($"Player transform is null. Cannot play missing buff sound for {buffName} at player location.");

                // Ensure looping sound stops if conditions to play are no longer met
                GameObject soundGo = GameObject.Find("TempLoopingAlertSound");
                if (soundGo != null)
                {
                    AudioSource audioSource = soundGo.GetComponent<AudioSource>();
                    if (audioSource != null && audioSource.isPlaying && audioSource.loop)
                    {
                        audioSource.Stop();
                        Log.LogInfo("Stopped looping alert sound as conditions are no longer met (AudioMan or anchor is null).");
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (currentlyMissingBuffs.Any())
            {
                // Flashing logic: alpha goes from 0 to 1 and back
                iconFlashTimer += Time.deltaTime * ICON_FLASH_SPEED;
                float alpha = Mathf.PingPong(iconFlashTimer, 1.0f);

                Color originalColor = GUI.color;
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

                float iconSize = IconSizeConfig.Value;
                float iconSpacing = IconSpacingConfig.Value;

                // Calculate the total width of the icon block
                float totalBlockWidth = (currentlyMissingBuffs.Count * (iconSize + iconSpacing)) - iconSpacing;

                // Calculate position based on configuration
                float xPos = 0f;
                float yPos = 0f;

                switch (IconPositionConfig.Value)
                {
                    case "TopLeft":
                        xPos = 10f;
                        yPos = 10f;
                        break;
                    case "TopCenter":
                        xPos = (Screen.width - totalBlockWidth) / 2f;
                        yPos = 10f;
                        break;
                    case "TopRight":
                        xPos = Screen.width - totalBlockWidth - 10f;
                        yPos = 10f;
                        break;
                    case "MiddleLeft":
                        xPos = 10f;
                        yPos = (Screen.height - iconSize) / 2f;
                        break;
                    case "MiddleCenter":
                        xPos = (Screen.width - totalBlockWidth) / 2f;
                        yPos = (Screen.height - iconSize) / 2f;
                        break;
                    case "MiddleRight":
                        xPos = Screen.width - totalBlockWidth - 10f;
                        yPos = (Screen.height - iconSize) / 2f;
                        break;
                    case "BottomLeft":
                        xPos = 10f;
                        yPos = Screen.height - iconSize - 10f;
                        break;
                    case "BottomCenter":
                        xPos = (Screen.width - totalBlockWidth) / 2f;
                        yPos = Screen.height - iconSize - 10f;
                        break;
                    case "BottomRight":
                        xPos = Screen.width - totalBlockWidth - 10f;
                        yPos = Screen.height - iconSize - 10f;
                        break;
                }

                foreach (StatusEffect missingBuffSE in currentlyMissingBuffs)
                {
                    Sprite sprite = missingBuffSE.m_icon;
                    if (sprite != null && sprite.texture != null)
                    {
                        Texture2D spriteSheet = sprite.texture;
                        Rect spritePixelRect = sprite.textureRect;

                        Rect uvCoords = new Rect(
                            spritePixelRect.x / spriteSheet.width,
                            spritePixelRect.y / spriteSheet.height,
                            spritePixelRect.width / spriteSheet.width,
                            spritePixelRect.height / spriteSheet.height
                        );

                        Rect iconRect = new Rect(xPos, yPos, iconSize, iconSize);
                        GUI.DrawTextureWithTexCoords(iconRect, spriteSheet, uvCoords, true);

                        xPos += iconSize + iconSpacing;
                    }
                }
                GUI.color = originalColor;
            }
        }

        // Future methods for buff detection and alerts will go here.
        // For example:
        // private void Update() { CheckBuffsAndAlert(); }
        // private void CheckBuffsAndAlert() { /* ... */ }
        // private void DisplayAlert(string buffName) { /* ... */ }
        // private void PlaySoundAlert() { /* ... */ }

        private void OnDestroy()
        {
            // If we had patches, we would unpatch them here.
            // harmony?.UnpatchSelf();
            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) is unloading.");
        }
    }

    public static class ZSFXHelper
    {
        public static void PlayOneShotZSFX(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            GameObject go = new GameObject("TempZSFX_" + clip.name);
            go.transform.position = position;

            AudioSource source = go.AddComponent<AudioSource>();
            ZSFX zsfx = go.AddComponent<ZSFX>();

            source.spatialBlend = 1f; // 3D sound
            source.minDistance = 1f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.loop = false;
            source.playOnAwake = false;

            // Basic ZSFX setup
            zsfx.m_audioClips = new[] { clip };
            zsfx.m_minVol = zsfx.m_maxVol = volume;
            zsfx.m_minPitch = zsfx.m_maxPitch = pitch;
            zsfx.m_playOnAwake = false;

            // Add randomness or captions if desired here
            zsfx.Awake(); // simulate Unity lifecycle
            zsfx.Play();

            // Cleanup after the clip finishes
            UnityEngine.Object.Destroy(go, clip.length + 0.5f);
        }
    }

}