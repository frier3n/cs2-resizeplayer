using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace cssResizePlayers
{
    public class cssResizePlayers : BasePlugin
    {
        public override string ModuleName => "Resize Players + No Block";
        public override string ModuleAuthor => "Yeezy, Manifest @Road To Glory, WD-, Cruze, r991";
        public override string ModuleDescription => "Allow resize players and remove box collisions with persistence support.";
        public override string ModuleVersion => "1.0.1";

        private const string Prefix = "[Resize]";

        private static PluginConfig Config = new();

        private string ConfigDir => Path.Combine(ModuleDirectory, "config");
        private string ConfigFile => Path.Combine(ConfigDir, "config.json");

        public FakeConVar<bool> cEnable = new("css_nocol_enable", "Toggle between noblock", true);
        public FakeConVar<bool> cGrenadeEnable = new("css_nocol_grenade_enable", "Toggle between noblock for grenades", true);

        private Dictionary<ulong, float> playerScales = new();

        public override void Load(bool hotReload)
        {
            Directory.CreateDirectory(ConfigDir);
            LoadConfig();

            AddCommand("css_sz", "Resize a player, you or a team", SetPlayerSizeCommand);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
        }

        public override void Unload(bool hotReload)
        {
            RemoveListener<Listeners.OnEntityCreated>(OnEntityCreated);
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    Config = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(ConfigFile)) ?? new PluginConfig();
                }
                else
                {
                    File.WriteAllText(ConfigFile, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch
            {
                Logger.LogError("[Resize] Error loading config.json — using default.");
                Config = new PluginConfig();
            }
        }

        [RequiresPermissions("@css/root")]
        [ConsoleCommand("css_sz", "Resize a player, you or a team")]
        [CommandHelper(minArgs: 2, usage: "<@all | @CT | @T | @me | player_name | SteamID> <size>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        private void SetPlayerSizeCommand(CCSPlayerController? admin, CommandInfo command)
        {
            if (admin == null)
            {
                Console.WriteLine("[Resize] Command must be executed by a player.");
                return;
            }

            string targetName = command.GetArg(1);
            if (!float.TryParse(command.GetArg(2), out float newScale))
            {
                admin.PrintToChat($" {ChatColors.Gold}{Prefix} {ChatColors.Red}Invalid scale value! Use a number between 0.1 and 10.0.");
                return;
            }

            newScale = Math.Clamp(newScale, 0.1f, 10.0f);
            List<CCSPlayerController> targetPlayers = GetTargetPlayers(targetName, admin);

            if (targetPlayers.Count == 0)
            {
                admin.PrintToChat($" {ChatColors.Gold}{Prefix} {ChatColors.Red}Player or team not found!");
                return;
            }

            foreach (var player in targetPlayers)
            {
                SetPlayerScale(player, newScale);
                player.PrintToChat($" {ChatColors.Gold}{Prefix} {ChatColors.Blue}Your size has been set to {newScale}.");

                if (Config.PersistentSize)
                    playerScales[player.SteamID] = newScale;
            }

            admin.PrintToChat($" {ChatColors.Gold}{Prefix} {ChatColors.Green}Set size to {newScale} for {targetPlayers.Count} player(s).");
        }

        private List<CCSPlayerController> GetTargetPlayers(string targetName, CCSPlayerController admin)
        {
            List<CCSPlayerController> players = new();

            if (targetName.Equals("@all", StringComparison.OrdinalIgnoreCase))
                players.AddRange(Utilities.GetPlayers());
            else if (targetName.Equals("@CT", StringComparison.OrdinalIgnoreCase))
                players.AddRange(Utilities.GetPlayers().Where(p => p.Team == CsTeam.CounterTerrorist));
            else if (targetName.Equals("@T", StringComparison.OrdinalIgnoreCase))
                players.AddRange(Utilities.GetPlayers().Where(p => p.Team == CsTeam.Terrorist));
            else if (targetName.Equals("@me", StringComparison.OrdinalIgnoreCase))
                players.Add(admin);
            else
            {
                ulong targetSteamID;
                bool isSteamID = ulong.TryParse(targetName, out targetSteamID);
                var player = Utilities.GetPlayers().FirstOrDefault(p =>
                    p.PlayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                    (isSteamID && p.SteamID == targetSteamID));

                if (player != null)
                    players.Add(player);
            }

            return players;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.PlayerPawn?.Value == null)
                return HookResult.Continue;

            if (Config.PersistentSize && playerScales.TryGetValue(player.SteamID, out float savedScale))
                SetPlayerScale(player, savedScale);
            else
                SetPlayerScale(player, 1.0f);

            CHandle<CCSPlayerPawn> pawn = player.PlayerPawn;
            Server.NextFrame(() => PlayerSpawnNextFrame(player, pawn));

            return HookResult.Continue;
        }

        private void PlayerSpawnNextFrame(CCSPlayerController player, CHandle<CCSPlayerPawn> pawn)
        {
            if (player == null || !player.IsValid || pawn == null || !pawn.IsValid || pawn.Value == null || !pawn.Value.IsValid)
                return;

            CollisionGroup collision = cEnable.Value
                ? CollisionGroup.COLLISION_GROUP_DEBRIS
                : CollisionGroup.COLLISION_GROUP_PLAYER;

            if (pawn.Value.Collision.CollisionGroup != (byte)collision)
                pawn.Value.SetCollisionGroup(collision);
        }

        private void SetPlayerScale(CCSPlayerController player, float scale)
        {
            var skeletonInstance = player.PlayerPawn.Value!.CBodyComponent?.SceneNode?.GetSkeletonInstance();
            if (skeletonInstance != null)
                skeletonInstance.Scale = scale;

            player.PlayerPawn.Value.AcceptInput("SetScale", null, null, scale.ToString());

            Server.NextFrame(() =>
            {
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_CBodyComponent");
            });
        }

        private void OnEntityCreated(CEntityInstance entity)
        {
            if (!cGrenadeEnable.Value) return;

            var className = entity.Entity?.DesignerName ?? "";
            if (string.IsNullOrWhiteSpace(className) || !className.Contains("_projectile")) return;

            entity.As<CBaseEntity>().SetCollisionGroup(CollisionGroup.COLLISION_GROUP_DEBRIS);
        }
    }

    public class PluginConfig
    {
        public bool PersistentSize { get; set; } = true;
    }

    public static class Extensions
    {
        private static readonly int OnCollisionRulesChangedOffset = GameData.GetOffset("OnCollisionRulesChangedOffset");
        public static void SetCollisionGroup(this CBaseEntity entity, CollisionGroup collision)
        {
            if (entity == null || !entity.IsValid || entity.Handle == IntPtr.Zero ||
                entity.Collision == null || entity.Collision.Handle == IntPtr.Zero)
                return;

            entity.Collision.CollisionGroup = (byte)collision;
            Utilities.SetStateChanged(entity, "CCollisionProperty", "m_collisionAttribute");

            VirtualFunctionVoid<nint> collisionRulesChanged =
                new VirtualFunctionVoid<nint>(entity.Handle, OnCollisionRulesChangedOffset);
            collisionRulesChanged.Invoke(entity.Handle);
        }
    }
}
