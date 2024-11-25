using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DeathTeleport", "RustFlash", "1.2.0")]
    class DeathTeleport : RustPlugin
    {
        private Configuration config;
        private Dictionary<ulong, int> playerTeleports = new Dictionary<ulong, int>();
        private Dictionary<ulong, Vector3> lastDeathPositions = new Dictionary<ulong, Vector3>();
        private Dictionary<ulong, DroppedItemContainer> playerDeathBags = new Dictionary<ulong, DroppedItemContainer>();
        private Dictionary<ulong, string> activeTeleportButtons = new Dictionary<ulong, string>();
        private Dictionary<ulong, Timer> protectionTimers = new Dictionary<ulong, Timer>();

        private const string PermDefault = "deathteleport.default";
        private const string PermTierOne = "deathteleport.tierone";
        private const string PermTierTwo = "deathteleport.tiertwo";
        private const string PermTierThree = "deathteleport.tierthree";
        private const string PermVIP = "deathteleport.vip";

        public static class UIHelper
        {
            public static CuiElementContainer NewCuiElement(string name, string color, string aMin, string aMax)
            {
                var element = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = false  // Changed from true to false
                        },
                        new CuiElement().Parent,
                        name
                    }
                };
                return element;
            }

            public static void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursorEnabled = false)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = color},
                    RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                    CursorEnabled = cursorEnabled
                }, panel);
            }

            public static void CreateItemIcon(ref CuiElementContainer container, string panel, string itemShortName, float alpha, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Url = $"assets/content/ui/ui.background.tile.psd", Color = $"1 1 1 {alpha}"},
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax}
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiImageComponent {ItemId = ItemManager.FindItemDefinition(itemShortName).itemid, SkinId = 0},
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax}
                    }
                });
            }

            public static void CreateButton(ref CuiElementContainer container, string panel, string text, string command, string aMin, string aMax)
            {
                container.Add(new CuiButton
                {
                    Button = {Command = command, Color = "0 0 0 0"},
                    RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                    Text = {Text = text, FontSize = 14, Align = TextAnchor.MiddleCenter}
                }, panel);
            }

            public static string HexToRGBA(string hex, float alpha)
            {
                if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";
                var str = hex.Trim('#');
                if (str.Length != 6) throw new Exception("Hex color must be six characters in length.");
                var r = byte.Parse(str.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha}";
            }
        }

        void Init()
        {
            permission.RegisterPermission(PermDefault, this);
            permission.RegisterPermission(PermTierOne, this);
            permission.RegisterPermission(PermTierTwo, this);
            permission.RegisterPermission(PermTierThree, this);
            permission.RegisterPermission(PermVIP, this);

            cmd.AddChatCommand("teledeath_tp", this, "CmdTeleDeath");
        }

        void OnServerInitialized()
        {
            RemoveAllTeleportButtons();
            LoadConfig();
        }

        void Unload()
        {
            cmd.RemoveChatCommand("teledeath_tp", this);
            RemoveAllTeleportButtons();
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;

            lastDeathPositions[player.userID] = player.transform.position;

            timer.Once(0.1f, () =>
            {
                var containers = UnityEngine.Object.FindObjectsOfType<DroppedItemContainer>();
                foreach (var container in containers)
                {
                    if (container.OwnerID == player.userID)
                    {
                        playerDeathBags[player.userID] = container;
                        break;
                    }
                }
            });
        }
        
        object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;

            if (protectionTimers.ContainsKey(player.userID))
            {
                return true; // Verhindert den Schaden
            }

            return null;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;

            if (GetPlayerMaxTeleports(player) > 0 || permission.UserHasPermission(player.UserIDString, PermVIP))
            {
                if (config.ShowUI)
                {
                    DestroyTeleportButton(player);
                    CreateTeleportButton(player);

                    timer.Once(300f, () => 
                    {
                        if (!lastDeathPositions.ContainsKey(player.userID)) return;

                        if (!DeathBagExistsAtPosition(lastDeathPositions[player.userID]))
                        {
                            DestroyTeleportButton(player);
                        }
                    });
                }
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is DroppedItemContainer container)
            {
                ulong playerId = container.playerSteamID;

                if (activeTeleportButtons.ContainsKey(playerId))
                {
                    BasePlayer player = BasePlayer.FindByID(playerId);
                    if (player != null)
                    {
                        CuiHelper.DestroyUi(player, activeTeleportButtons[playerId]);
                        activeTeleportButtons.Remove(playerId);
                        Puts($"Removed teleport button for {player.displayName} (Loot sack despawned or removed).");
                    }
                }
            }
        }

        void OnNewDay()
        {
            playerTeleports.Clear();
        }

        [ChatCommand("teledeath")]
        void CmdTeleDeath(BasePlayer player, string command, string[] args)
        {
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

            CmdTeleDeath(player, "teledeath", new string[0]);
        }

        void TeleportPlayer(BasePlayer player, Vector3 position)
        {
            if (player == null || !player.IsConnected) return;

            player.Teleport(position);

            if (protectionTimers.ContainsKey(player.userID))
            {
                protectionTimers[player.userID].Destroy();
            }

            protectionTimers[player.userID] = timer.Once(30f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    protectionTimers.Remove(player.userID);
                    SendMessage(player, "Protection disabled.");
                }
            });

            SendMessage(player, "You have been teleported to your death location. Protected for 30 seconds.");

            int maxTeleports = GetPlayerMaxTeleports(player);
            if (maxTeleports != -1)
            {
                playerTeleports[player.userID]++;
                SendMessage(player, $"You have {maxTeleports - playerTeleports[player.userID]} teleports remaining today.");
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (protectionTimers.ContainsKey(player.userID))
            {
                protectionTimers[player.userID].Destroy();
                protectionTimers.Remove(player.userID);
            }
        }

        void CreateTeleportButton(BasePlayer player)
        {
            var element = UIHelper.NewCuiElement("DEATHTELEPORT_UI", UIHelper.HexToRGBA("#1c1c1c", 0.28f), "0.695 0.025", "0.74 0.105");

            UIHelper.CreatePanel(ref element, "DEATHTELEPORT_UI", UIHelper.HexToRGBA("#ffffff", 0.26f), "0.0 0.0", "1.0 1.0", false);
            UIHelper.CreateItemIcon(ref element, "DEATHTELEPORT_UI", "skull.human", 0f, "0.15 0.2", "0.85 0.8");
            UIHelper.CreateButton(ref element, "DEATHTELEPORT_UI", "", "chat.say /teledeath_tp", "0 0", "1 1");

            CuiHelper.AddUi(player, element);
            activeTeleportButtons[player.userID] = "DEATHTELEPORT_UI";
        }

        void DestroyTeleportButton(BasePlayer player)
        {
            if (player != null && player.IsConnected)
            {
                CuiHelper.DestroyUi(player, "DEATHTELEPORT_UI");
            }
            if (activeTeleportButtons.ContainsKey(player.userID))
            {
                activeTeleportButtons.Remove(player.userID);
            }
        }

        bool DeathBagExistsAtPosition(Vector3 position)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is DroppedItemContainer bag && Vector3.Distance(bag.transform.position, position) < 1f)
                {
                    return true;
                }
            }
            return false;
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

        void RemoveAllTeleportButtons()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyTeleportButton(player);
            }
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
    }
}