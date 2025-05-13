using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // Added for GUI and other Unity functionalities

namespace ETHICAL_Auras
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class EthicalAurasPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.talitore.ethical-auras"; // MODIFIED - Replace with your actual GUID
        public const string PluginName = "ETHICAL Auras"; // MODIFIED
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource Log;
        private Harmony harmony;

        // Configuration Entries
        public static ConfigEntry<string> TrackedBuffsConfig;
        public static List<string> TrackedBuffNames = new List<string>();

        // List to store buffs that are currently missing
        private List<string> currentlyMissingBuffs = new List<string>();

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) is loading!");

            // Initialize Configuration
            TrackedBuffsConfig = Config.Bind(
                "General", // Section
                "TrackedBuffs", // Key
                "Rested,GP_Eikthyr", // Default value (example buffs)
                "Comma-separated list of buff names to track (e.g., Rested,GP_Eikthyr,CookedLoxMeat). Use the internal buff names."); // Description

            ParseTrackedBuffs();
            // Listen for changes to the configuration and update if it's modified while the game is running.
            TrackedBuffsConfig.SettingChanged += (sender, args) => ParseTrackedBuffs();

            // Initialize Harmony for patching
            harmony = new Harmony(PluginGUID);
            // In future steps, we will add harmony.PatchAll(); here once we have patches.
            // For US1, we are only setting up configuration.

            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) has loaded. Currently tracking: {string.Join(", ", TrackedBuffNames)}");
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
        }

        private void CheckActiveBuffs()
        {
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || localPlayer.GetSEMan() == null)
            {
                currentlyMissingBuffs.Clear(); // No player or SEMan, so no buffs to be missing
                return;
            }

            SEMan statusEffectManager = localPlayer.GetSEMan();
            List<string> missingBuffsThisFrame = new List<string>();

            foreach (string buffName in TrackedBuffNames)
            {
                if (!statusEffectManager.HaveStatusEffect(buffName.GetStableHashCode())) // Assuming HaveStatusEffect takes an int (hash of the name)
                {
                    missingBuffsThisFrame.Add(buffName);
                }
            }
            currentlyMissingBuffs = missingBuffsThisFrame;
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
            Texture2D.Destroy(GUI.skin.label.normal.background as Texture2D); // Clean up texture created for OnGUI
            Log.LogInfo($"Plugin {PluginGUID} ({PluginName}) is unloading.");
        }
    }
}