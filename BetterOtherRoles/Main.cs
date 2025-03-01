﻿global using Il2CppInterop.Runtime;
global using Il2CppInterop.Runtime.Attributes;
global using Il2CppInterop.Runtime.InteropTypes;
global using Il2CppInterop.Runtime.InteropTypes.Arrays;
global using Il2CppInterop.Runtime.Injection;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using UnityEngine;
using BetterOtherRoles.Modules;
using BetterOtherRoles.Players;
using BetterOtherRoles.Utilities;
using Il2CppSystem.Security.Cryptography;
using Il2CppSystem.Text;
using AmongUs.Data;
using BetterOtherRoles.Modules.CustomHats;
using BetterOtherRoles.UI;
using BetterOtherRoles.Utilities.Attributes;
using UnityEngine.IO;
using File = System.IO.File;

namespace BetterOtherRoles
{
    [BepInPlugin(Id, Name, VersionString)]
    [BepInDependency(SubmergedCompatibility.SUBMERGED_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("Among Us.exe")]
    public class BetterOtherRolesPlugin : BasePlugin
    {
        public const string Name = "Better Other Roles";
        public const string Id = "betterohterroles.eno.pm";
        public const string VersionString = "1.6.5";
        public const int betaNum = 0;
        
        public static Version Version = Version.Parse(VersionString);
        internal static BepInEx.Logging.ManualLogSource Logger;
         
        public Harmony Harmony { get; } = new Harmony(Id);
        public static BetterOtherRolesPlugin Instance;

        public static int optionsPage = 2;

        public static ConfigEntry<string> DebugMode { get; private set; }
        public static ConfigEntry<bool> GhostsSeeInformation { get; set; }
        public static ConfigEntry<bool> GhostsSeeRoles { get; set; }
        public static ConfigEntry<bool> GhostsSeeModifier { get; set; }
        public static ConfigEntry<bool> GhostsSeeVotes{ get; set; }
        public static ConfigEntry<bool> ShowRoleSummary { get; set; }
        public static ConfigEntry<bool> ShowLighterDarker { get; set; }
        public static ConfigEntry<bool> EnableSoundEffects { get; set; }
        public static ConfigEntry<bool> EnableHorseMode { get; set; }
        public static ConfigEntry<bool> ShowVentsOnMap { get; set; }
        public static ConfigEntry<bool> GetBetaReleases { get; set; }
        public static ConfigEntry<string> ShowPopUpVersion { get; set; }

        public override void Load() {
            Logger = Log;
            Instance = this;

            DebugMode = Config.Bind("Custom", "Enable Debug Mode", "false");
            GhostsSeeInformation = Config.Bind("Custom", "Ghosts See Remaining Tasks", true);
            GhostsSeeRoles = Config.Bind("Custom", "Ghosts See Roles", true);
            GhostsSeeModifier = Config.Bind("Custom", "Ghosts See Modifier", true);
            GhostsSeeVotes = Config.Bind("Custom", "Ghosts See Votes", true);
            ShowRoleSummary = Config.Bind("Custom", "Show Role Summary", true);
            ShowLighterDarker = Config.Bind("Custom", "Show Lighter / Darker", true);
            EnableSoundEffects = Config.Bind("Custom", "Enable Sound Effects", true);
            EnableHorseMode = Config.Bind("Custom", "Enable Horse Mode", false);
            ShowPopUpVersion = Config.Bind("Custom", "Show PopUp", "0");
            ShowVentsOnMap = Config.Bind("Custom", "Show vent positions on minimap", true);
            GetBetaReleases = Config.Bind("Custom", "Get Beta Releases", false);

            DebugMode = Config.Bind("Custom", "Enable Debug Mode", "false");
            
            if(Directory.GetFiles(Paths.PluginPath, "UniverseLib*", SearchOption.AllDirectories).FirstOrDefault() == null)
            {
                string libPath = Path.Combine(Helpers.getAssemblyPath(), "UniverseLib.IL2CPP.dll");
                Helpers.writeResourceToFile("BetterOtherRoles.Resources.Files.UniverseLib.IL2CPP.dll", libPath);
            }
            
            Harmony.PatchAll();
            
            CustomOptionHolder.Load();
            CustomColors.Load();
            CustomHatManager.LoadHats();
            if (BepInExUpdater.UpdateRequired)
            {
                AddComponent<BepInExUpdater>();
                return;
            }

            EventUtility.Load();
            SubmergedCompatibility.Initialize();
            
            MainMenuPatch.addSceneChangeCallbacks();
            AddToKillDistanceSetting.addKillDistance();
            
            AutoloadAttribute.Initialize();
            UIManager.Init();
            AddComponent<BetterUIBehaviour>();
        }
    }

    // Deactivate bans, since I always leave my local testing game and ban myself
    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.AmBanned), MethodType.Getter)]
    public static class AmBannedPatch
    {
        public static void Postfix(out bool __result)
        {
            __result = false;
        }
    }
    
    // Debugging tools
    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    public static class DebugManager
    {
        private static readonly string passwordHash = "b0338c64994729ef6ea8f812a816a5000012b15a94d3bf18e059c0b8e6a974c4";
        private static readonly System.Random random = new System.Random((int)DateTime.Now.Ticks);
        private static List<PlayerControl> bots = new List<PlayerControl>();

        public static void Postfix(KeyboardJoystick __instance)
        {
            // Check if debug mode is active.
            StringBuilder builder = new StringBuilder();
            SHA256 sha = SHA256Managed.Create();
            Byte[] hashed = sha.ComputeHash(Encoding.UTF8.GetBytes(BetterOtherRolesPlugin.DebugMode.Value));
            foreach (var b in hashed) {
                builder.Append(b.ToString("x2"));
            }
            string enteredHash = builder.ToString();
            if (enteredHash != passwordHash) return;


            // Spawn dummys
            if (Input.GetKeyDown(KeyCode.F)) {
                var playerControl = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
                var i = playerControl.PlayerId = (byte) GameData.Instance.GetAvailableId();

                bots.Add(playerControl);
                GameData.Instance.AddPlayer(playerControl);
                AmongUsClient.Instance.Spawn(playerControl, -2, InnerNet.SpawnFlags.None);
                
                playerControl.transform.position = CachedPlayer.LocalPlayer.transform.position;
                playerControl.GetComponent<DummyBehaviour>().enabled = true;
                playerControl.NetTransform.enabled = false;
                playerControl.SetName(RandomString(10));
                playerControl.SetColor((byte) random.Next(Palette.PlayerColors.Length));
                GameData.Instance.RpcSetTasks(playerControl.PlayerId, Array.Empty<byte>());
            }

            // Terminate round
            if(Input.GetKeyDown(KeyCode.L)) {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(CachedPlayer.LocalPlayer.PlayerControl.NetId, (byte)CustomRPC.ForceEnd, Hazel.SendOption.Reliable, -1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.forceEnd();
            }
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
