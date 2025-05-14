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

            ParseTrackedBuffs();
            // Listen for changes to the configuration and update if it's modified while the game is running.
            TrackedBuffsConfig.SettingChanged += (sender, args) => ParseTrackedBuffs();
            EnableAudioAlertsConfig.SettingChanged += (sender, args) => Log.LogInfo($"Audio alerts {(EnableAudioAlertsConfig.Value ? "enabled" : "disabled")}.");
            LoopAudioAlertConfig.SettingChanged += (sender, args) => Log.LogInfo($"Audio alert looping {(LoopAudioAlertConfig.Value ? "enabled" : "disabled")}.");
            SelectedAudioClipNameConfig.SettingChanged += (sender, args) =>
            {
                Log.LogInfo($"Selected audio clip name changed to: {SelectedAudioClipNameConfig.Value}. Reloading clip.");
                // Reload the audio clip when the setting changes
                LoadSelectedAudioClip(ethicalAurasBundle); // Use the class field 'ethicalAurasBundle'
            };

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

        private void Update()
        {
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

                // --- START DEBUG: Make icons larger and centered for testing ---
                float debugIconSize = 200f;
                float iconPaddingToUse = ICON_PADDING; // Use existing class constant

                // Calculate the total width of the icon block
                float totalBlockWidth = 0f;
                // This if condition is technically redundant due to currentlyMissingBuffs.Any() check earlier,
                // but kept for clarity if the outer check structure changes.
                if (currentlyMissingBuffs.Count > 0) {
                     totalBlockWidth = (currentlyMissingBuffs.Count * (debugIconSize + iconPaddingToUse)) - iconPaddingToUse;
                }

                float xPos = (Screen.width - totalBlockWidth) / 2f; // Centered horizontally
                float yPos = (Screen.height - debugIconSize) / 2f; // Centered vertically
                // --- END DEBUG ---

                // Commenting out original positioning logic for the debug session
                // float xPos = (Screen.width / 2f) - (currentlyMissingBuffs.Count * (ICON_SIZE + ICON_PADDING) - ICON_PADDING) / 2f; // Centered
                // float yPos = 10f; // Top of the screen

                foreach (StatusEffect missingBuffSE in currentlyMissingBuffs)
                {
                    Sprite sprite = missingBuffSE.m_icon; // Get the Sprite object
                    if (sprite != null && sprite.texture != null) // Ensure sprite and its texture exist
                    {
                        Texture2D spriteSheet = sprite.texture; // This is the full sprite sheet
                        Rect spritePixelRect = sprite.textureRect; // The specific sprite's rectangle in pixels on the sheet

                        // Calculate the UV coordinates for the specific sprite
                        // (normalized coordinates within the sprite sheet)
                        Rect uvCoords = new Rect(
                            spritePixelRect.x / spriteSheet.width,
                            spritePixelRect.y / spriteSheet.height,
                            spritePixelRect.width / spriteSheet.width,
                            spritePixelRect.height / spriteSheet.height
                        );

                        // Use debugIconSize for drawing from the previous debugging step
                        Rect iconRect = new Rect(xPos, yPos, debugIconSize, debugIconSize);

                        // Draw the specific part of the texture (the sprite)
                        GUI.DrawTextureWithTexCoords(iconRect, spriteSheet, uvCoords, true);

                        // Advance xPos for the next icon, using debugIconSize and its associated padding
                        xPos += debugIconSize + iconPaddingToUse;
                    }
                }
                GUI.color = originalColor; // Reset GUI color
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