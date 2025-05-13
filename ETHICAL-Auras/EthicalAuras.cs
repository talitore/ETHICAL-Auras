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

        // Configuration Entries
        public static ConfigEntry<string> TrackedBuffsConfig;
        public static List<string> TrackedBuffNames = new List<string>();

        public static ConfigEntry<bool> EnableAudioAlertsConfig;
        public static ConfigEntry<string> AudioAlertSoundNameConfig;

        // List to store buffs that are currently missing
        private List<string> currentlyMissingBuffs = new List<string>();

        private AudioClip missingBuffAudioClip;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) is loading!");

            // Initialize Configuration
            TrackedBuffsConfig = Config.Bind(
                "General", // Section
                "TrackedBuffs", // Key
                "Rested,GP_Eikthyr,CampFire,Potion_poisonresist", // Default value (example buffs)
                "Comma-separated list of buff names to track (e.g., Rested,GP_Eikthyr,CookedLoxMeat). Use the internal buff names."); // Description

            EnableAudioAlertsConfig = Config.Bind(
                "AudioAlerts", // Section
                "EnableAudioAlerts", // Key
                true, // Default value
                "Enable or disable audio alerts for missing buffs."); // Description

            AudioAlertSoundNameConfig = Config.Bind(
                "AudioAlerts", // Section
                "AudioAlertSoundName", // Key
                "gui_ping", // Default value - a guess, user might need to change this
                "The name of the game's sound effect to play for missing buff alerts (e.g., 'gui_ping', 'sfx_lootray_pickup'). Leave empty to disable specific sound."); // Description

            ParseTrackedBuffs();
            // Listen for changes to the configuration and update if it's modified while the game is running.
            TrackedBuffsConfig.SettingChanged += (sender, args) => ParseTrackedBuffs();
            EnableAudioAlertsConfig.SettingChanged += (sender, args) => Log.LogInfo($"Audio alerts {(EnableAudioAlertsConfig.Value ? "enabled" : "disabled")}.");
            AudioAlertSoundNameConfig.SettingChanged += (sender, args) => Log.LogInfo($"Audio alert sound name changed to: {AudioAlertSoundNameConfig.Value}");

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

            AssetBundle bundle = AssetBundle.LoadFromStream(stream);
            stream.Close(); // It's good practice to close the stream after loading

            if (bundle == null)
            {
                Log.LogError($"Failed to load AssetBundle from stream: {resourceName}.");
                return; // Or handle error appropriately
            }

            // Load the audio clip
            AudioClip audioClip = bundle.LoadAsset<AudioClip>("scream");

            if (audioClip == null)
            {
                Log.LogError($"Failed to load AudioClip 'scream' from AssetBundle: {resourceName}.");
                // missingBuffAudioClip will remain null, and sound playing will be skipped or might error later if not handled.
            }
            else
            {
                missingBuffAudioClip = audioClip;
                Log.LogInfo("Successfully loaded 'scream' audio clip from asset bundle.");
            }

            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) has loaded. Currently tracking: {string.Join(", ", TrackedBuffNames)}");
            Log.LogInfo($"Audio alerts: {(EnableAudioAlertsConfig.Value ? "Enabled" : "Disabled")}, Sound: {AudioAlertSoundNameConfig.Value}");
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
            List<string> missingBuffsFoundThisFrame = new List<string>();

            foreach (string buffName in TrackedBuffNames)
            {
                // Assuming HaveStatusEffect takes an int (hash of the name)
                // For Valheim, buff names are typically checked by their hashed integer ID or directly by string name if the SEMan method supports it.
                // The original code uses GetStableHashCode(), which is a common approach for string-based buff checks if the game's API expects an int hash.
                // Let's assume StatusEffect.m_name is the string to compare against if GetStableHashCode() isn't directly finding it or if direct name comparison is preferred.
                // However, the existing code used GetStableHashCode(), so we'll stick to it unless issues arise.
                if (!statusEffectManager.HaveStatusEffect(buffName.GetStableHashCode()))
                {
                    missingBuffsFoundThisFrame.Add(buffName);
                }
            }

            if (EnableAudioAlertsConfig.Value)
            {
                foreach (string newMissingBuff in missingBuffsFoundThisFrame)
                {
                    if (!currentlyMissingBuffs.Contains(newMissingBuff)) // If it wasn't missing last frame but is now
                    {
                        PlayMissingBuffSoundLogic(newMissingBuff, localPlayer.transform);
                    }
                }
            }

            currentlyMissingBuffs = missingBuffsFoundThisFrame;
        }

        private void PlayMissingBuffSoundLogic(string buffName, Transform anchor)
        {
            if (string.IsNullOrEmpty(AudioAlertSoundNameConfig?.Value))
            {
                //Log.LogDebug($"Audio alert sound name is not configured or empty. Won\'t play sound for missing buff: {buffName}");
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
                    // Configure the AudioSource for 3D sound, if desired, similar to ZSFXHelper
                    audioSource.spatialBlend = 1f;
                    audioSource.minDistance = 1f;
                    audioSource.maxDistance = 30f;
                    audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                    // Apply other settings as needed
                }
                else
                {
                    audioSource = soundGo.GetComponent<AudioSource>();
                }

                soundGo.transform.position = anchor.position; // Update position to the anchor (player)

                if (missingBuffAudioClip != null)
                {
                    if (!audioSource.isPlaying || audioSource.clip != missingBuffAudioClip)
                    {
                        audioSource.clip = missingBuffAudioClip;
                        audioSource.loop = true; // Enable looping
                        audioSource.Play();
                        Log.LogInfo($"Started looping '{AudioAlertSoundNameConfig.Value}' sound for missing buff: {buffName}");
                    }
                }
                else
                {
                    Log.LogWarning($"missingBuffAudioClip is null. Cannot play sound for {buffName}.");
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
                // Basic styling for the label - black background, white text
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.UpperLeft;
                style.fontSize = 16; // Example font size
                Texture2D background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0, 0, 0, 0.7f)); // Semi-transparent black
                background.Apply();
                style.normal.background = background;
                style.normal.textColor = Color.white;
                style.padding = new RectOffset(5, 5, 5, 5);

                float yOffset = 10f; // Starting Y position for the first alert
                float xPos = 10f; // Starting X position for alerts
                float lineHeight = 25f; // Height of each alert line, including padding

                foreach (string missingBuff in currentlyMissingBuffs)
                {
                    string alertMessage = $"MISSING: {missingBuff}";
                    GUI.Label(new Rect(xPos, yOffset, 250, lineHeight), alertMessage, style); // Adjust width as needed
                    yOffset += lineHeight; // Move next alert down
                }
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
            if (GUI.skin.label.normal.background != null)
            {
                 Texture2D.Destroy(GUI.skin.label.normal.background as Texture2D); // Clean up texture created for OnGUI
            }
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