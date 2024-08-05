using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DeathTeleport", "RustFlash", "1.0.0")]
    [Description("grants a number of teleports to your own body")]
    class DeathTeleport : RustPlugin
    {
        private Configuration config;
        private Dictionary<ulong, int> playerTeleports = new Dictionary<ulong, int>();
        private Dictionary<ulong, Vector3> lastDeathPositions = new Dictionary<ulong, Vector3>();

        private const string PermDefault = "deathteleport.default";
        private const string PermTierOne = "deathteleport.tierone";
        private const string PermTierTwo = "deathteleport.tiertwo";
        private const string PermTierThree = "deathteleport.tierthree";
        private const string PermVIP = "deathteleport.vip";

        void Init()
        {
            permission.RegisterPermission(PermDefault, this);
            permission.RegisterPermission(PermTierOne, this);
            permission.RegisterPermission(PermTierTwo, this);
            permission.RegisterPermission(PermTierThree, this);
            permission.RegisterPermission(PermVIP, this);

            cmd.AddChatCommand("teledeath", this, "CmdTeleDeath");
        }

        void OnServerInitialized()
        {
            RemoveAllTeleportButtons();
            LoadConfig();
        }

        void Unload()
        {
            RemoveAllTeleportButtons();
        }

        void RemoveAllTeleportButtons()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyTeleportButton(player);
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            lastDeathPositions[player.userID] = player.transform.position;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;
            if (GetPlayerMaxTeleports(player) > 0 || permission.UserHasPermission(player.UserIDString, PermVIP))
            {
                if (config.ShowUI)
                {
                    CreateTeleportButton(player);
                    timer.Once(120f, () => DestroyTeleportButton(player));
                }
            }
        }

        void CreateTeleportButton(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4 0.115", AnchorMax = "0.6 0.165" },
                Button = { Color = "0.545 0 0 1", Command = "chat.say /teledeath" },
                Text = { Text = "Teleport to Death", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" }
            }, "Hud", "DeathTeleportButton");
            CuiHelper.AddUi(player, container);
        }

        void DestroyTeleportButton(BasePlayer player)
        {
            if (player != null && player.IsConnected)
            {
                CuiHelper.DestroyUi(player, "DeathTeleportButton");
            }
        }

        [ChatCommand("teledeath")]
        void CmdTeleDeath(BasePlayer player, string command, string[] args)
        {
            Puts($"CmdTeleDeath wurde aufgerufen von {player.displayName}");

            if (!lastDeathPositions.ContainsKey(player.userID))
            {
                SendMessage(player, "You have no recorded death position.");
                return;
            }

            int maxTeleports = GetPlayerMaxTeleports(player);
            if (maxTeleports == 0)
            {
                SendMessage(player, "You don't have permission to use this command.");
                return;
            }

            if (!playerTeleports.ContainsKey(player.userID))
            {
                playerTeleports[player.userID] = 0;
            }

            if (playerTeleports[player.userID] >= maxTeleports && maxTeleports != -1)
            {
                SendMessage(player, "You have used all your teleports for today.");
                return;
            }

            Vector3 deathPosition = lastDeathPositions[player.userID];
            DestroyTeleportButton(player);
            timer.Once(10f, () => TeleportPlayer(player, deathPosition));
            SendMessage(player, "Teleporting to your death location in 10 seconds...");
        }

        [ConsoleCommand("teledeath")]
        private void ConsoleCmdTeleDeath(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            Puts($"ConsoleCmdTeleDeath wurde aufgerufen von {player.displayName}");
            CmdTeleDeath(player, "teledeath", new string[0]);
        }

        void TeleportPlayer(BasePlayer player, Vector3 position)
        {
            if (player == null || !player.IsConnected) return;

            player.Teleport(position);
            SendMessage(player, "You have been teleported to your death location.");

            int maxTeleports = GetPlayerMaxTeleports(player);
            if (maxTeleports != -1)
            {
                playerTeleports[player.userID]++;
                SendMessage(player, $"You have {maxTeleports - playerTeleports[player.userID]} teleports remaining today.");
            }
        }

        int GetPlayerMaxTeleports(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, PermVIP)) return -1;
            if (permission.UserHasPermission(player.UserIDString, PermTierThree)) return config.TierThreeTeleports;
            if (permission.UserHasPermission(player.UserIDString, PermTierTwo)) return config.TierTwoTeleports;
            if (permission.UserHasPermission(player.UserIDString, PermTierOne)) return config.TierOneTeleports;
            if (permission.UserHasPermission(player.UserIDString, PermDefault)) return config.DefaultTeleports;
            return 0;
        }

        void SendMessage(BasePlayer player, string message)
        {
            player.ChatMessage($"<color=#8B0000>DeathTeleport:</color> {message}");
        }

        void LoadConfig()
        {
            config = Config.ReadObject<Configuration>();
            if (config == null)
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                DefaultTeleports = 1,
                TierOneTeleports = 2,
                TierTwoTeleports = 3,
                TierThreeTeleports = 4,
                ShowUI = true
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            public int DefaultTeleports { get; set; }
            public int TierOneTeleports { get; set; }
            public int TierTwoTeleports { get; set; }
            public int TierThreeTeleports { get; set; }
            public bool ShowUI { get; set; }
        }

        void OnNewDay()
        {
            playerTeleports.Clear();
        }
    }
}