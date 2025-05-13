using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

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
}