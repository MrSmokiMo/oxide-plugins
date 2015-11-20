﻿/*
TODO:
- Add option to hide as a deployed entity of choice
- Add command cooldown option
- Add daily limit option
- Add max vanish time option
- Add AppearWhileRunning option (player.IsRunning())
- Add AppearWhenDamaged option (player.IsWounded())
- Add restoring after reconnection (datafile/static dictionary)
- Fix 'CanUseWeapon' only visually hiding item; find a better way
- Fix CUI overlay overlapping HUD elements/inventory
*/

using System;
using System.Collections.Generic;
using Network;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Vanish", "Wulf/lukespragg", "0.2.3", ResourceId = 1420)]
    [Description("Allows players with permission to become truly invisible.")]

    class Vanish : RustPlugin
    {
        // Do NOT edit this file, instead edit Vanish.json in server/<identity>/oxide/config

        #region Configuration

        // Messages
        string ChatCommand => GetConfig("ChatCommand", "vanish");
        string CantBeHurt => GetConfig("CantBeHurt", "You can't be hurt while vanished");
        string CantDamageBuilds => GetConfig("CantDamageBuilds", "You can't damage buildings while vanished");
        string CantHurtAnimals => GetConfig("CantHurtAnimals", "You can't hurt animals while vanished");
        string CantHurtPlayers => GetConfig("CantHurtPlayers", "You can't hurt players while vanished");
        string CantUseTeleport => GetConfig("CantUseTeleport", "You can't teleport while vanished");
        string NoPermission => GetConfig("NoPermission", "Sorry, you can't use 'vanish' right now");
        string VanishDisabled => GetConfig("VanishDisabled", "You are no longer invisible!");
        string VanishEnabled => GetConfig("VanishEnabled", "You have vanished from sight...");

        // Settings
        bool CanBeHurt => GetConfig("CanBeHurt", false);
        bool CanDamageBuilds => GetConfig("CanDamageBuilds", true);
        bool CanHurtAnimals => GetConfig("CanHurtAnimals", true);
        bool CanHurtPlayers => GetConfig("CanHurtPlayers", true);
        bool CanUseTeleport => GetConfig("CanUseTeleport", true);
        //bool CanUseWeapons => GetConfig("CanUseWeapons", true);
        bool ShowIndicator => GetConfig("ShowIndicator", true);
        bool ShowOverlay => GetConfig("ShowOverlay", false);
        bool VisibleToAdmin => GetConfig("VisibleToAdmin", true);
        bool VisualEffect => GetConfig("VisualEffect", true);

        protected override void LoadDefaultConfig()
        {
            // Messages
            Config["ChatCommand"] = ChatCommand;
            Config["CantBeHurt"] = CantBeHurt;
            Config["CantDamageBuilds"] = CantDamageBuilds;
            Config["CantHurtAnimals"] = CantHurtAnimals;
            Config["CantHurtPlayers"] = CantHurtPlayers;
            Config["CantUseTeleport"] = CantUseTeleport;
            Config["NoPermission"] = NoPermission;
            Config["VanishDisabled"] = VanishDisabled;
            Config["VanishEnabled"] = VanishEnabled;

            // Settings
            Config["CanBeHurt"] = CanBeHurt;
            Config["CanDamageBuilds"] = CanDamageBuilds;
            Config["CanHurtAnimals"] = CanHurtAnimals;
            Config["CanHurtPlayers"] = CanHurtPlayers;
            Config["CanUseTeleport"] = CanUseTeleport;
            //Config["CanUseWeapons"] = CanUseWeapons;
            Config["ShowIndicator"] = ShowIndicator;
            Config["ShowOverlay"] = ShowOverlay;
            Config["VisibleToAdmin"] = VisibleToAdmin;
            Config["VisualEffect"] = VisualEffect;

            SaveConfig();
        }

        #endregion

        #region General Setup

        void Loaded()
        {
            LoadDefaultConfig();

            permission.RegisterPermission("vanish.allowed", this);
            cmd.AddChatCommand(ChatCommand, this, "VanishChatCmd");
        }

        #endregion

        #region Data Storage

        class OnlinePlayer
        {
            public BasePlayer Player;
            public bool IsInvisible;
        }

        [OnlinePlayers] Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer>();

        #endregion

        #region Chat Command

        void VanishChatCmd(BasePlayer player)
        {
            if (!HasPermission(player, "vanish.allowed"))
            {
                SendReply(player, NoPermission);
                return;
            }

            // Vanishing visual effect
            if (VisualEffect) Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", player.transform.position);

            // Remove player/held item from view
            if (IsInvisible(player))
            {
                Reappear(player);
                return;
            }

            Disappear(player);
        }

        #endregion

        #region Vanishing Act

        void Disappear(BasePlayer player)
        {
            // Destroy player/item entities
            var connections = new List<Connection>();
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                Puts($"{player.displayName} ({player.UserIDString})");
                if (player == basePlayer && player.displayName != null) continue;
                if (VisibleToAdmin && IsAdmin(basePlayer)) continue;
                connections.Add(basePlayer.net.connection);
            }
            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Message.Type.EntityDestroy);
                Net.sv.write.EntityID(player.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }
            var item = player.GetActiveItem();
            if (item?.GetHeldEntity() != null && Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Message.Type.EntityDestroy);
                Net.sv.write.EntityID(item.GetHeldEntity().net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            // Add overlay effect
            if (ShowOverlay || ShowIndicator) VanishGui(player);

            // Save and notify
            PrintToChat(player, VanishEnabled);
            onlinePlayers[player].IsInvisible = true;
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            var player = entity as BasePlayer ?? (entity as HeldEntity)?.ownerPlayer;
            if (player == null || target == null || player == target) return null;
            if (VisibleToAdmin && IsAdmin(target)) return null;

            // Hide from other players
            if (IsInvisible(player)) return false;

            return null;
        }

        object CanBeTargeted(BaseCombatEntity entity)
        {
            // Hide from helis/turrets
            var player = entity as BasePlayer;
            if (player != null && IsInvisible(player)) return false;

            return null;
        }

        /*void OnPlayerSleepEnded(BasePlayer player)
        {
            // Notify if still vanished
            if (IsInvisible(player)) Disappear(player);
        }*/

        #endregion

        #region Reappearing Act

        void Reappear(BasePlayer player)
        {
            // Make player visible
            onlinePlayers[player].IsInvisible = false;
            player.SendNetworkUpdate();

            // Restore held item visibility
            player.GetActiveItem()?.GetHeldEntity()?.SendNetworkUpdate();

            // Destroy existing UI
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);

            PrintToChat(player, VanishDisabled);
        }

        #endregion

        #region GUI Indicator/Overlay

        Dictionary<ulong, string> GuiInfo = new Dictionary<ulong, string>();

        void VanishGui(BasePlayer player)
        {
            // Destroy existing UI
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);

            var elements = new CuiElementContainer();
            GuiInfo[player.userID] = CuiHelper.GetGuid();

            if (ShowIndicator)
            {
                elements.Add(new CuiElement
                {
                    Name = GuiInfo[player.userID],
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 0.3", Url = "http://i.imgur.com/Gr5G3YI.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.025 0.04",  AnchorMax = "0.075 0.12" }
                    }
                });
            }

            if (ShowOverlay)
            {
                elements.Add(new CuiElement
                {
                    Name = GuiInfo[player.userID],
                    Components =
                    {
                        new CuiRawImageComponent { Sprite = "assets/content/ui/overlay_freezing.png" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }

            // Create the UI elements
            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Damage Blocking

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = (info?.Initiator as BasePlayer) ?? entity as BasePlayer;
            if (player == null || !player.IsConnected() || !onlinePlayers[player].IsInvisible) return null;

            // Block damage to animals
            if (entity is BaseNPC)
            {
                if (CanHurtAnimals) return null;
                PrintToChat(player, CantHurtAnimals);
                return true;
            }

            // Block damage to builds
            if (!(entity is BasePlayer))
            {
                if (CanDamageBuilds) return null;
                PrintToChat(player, CantDamageBuilds);
                return true;
            }

            // Block damage to players
            if (info?.Initiator is BasePlayer)
            {
                if (CanHurtPlayers) return null;
                PrintToChat(player, CantHurtPlayers);
                return true;
            }

            // Block damage to self
            if (!CanBeHurt)
            {
                PrintToChat(player, CantBeHurt);
                return true;
            }

            return null;
        }

        #endregion

        #region Weapon Blocking

        /*void OnPlayerTick(BasePlayer player)
        {
            // Hide active item
            if (onlinePlayers[player].IsInvisible && !CanUseWeapons && player.GetActiveItem() != null)
            {
                var heldEntity = player.GetActiveItem().GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(false);
            }
        }*/

        #endregion

        #region Teleport Blocking

        object CanTeleport(BasePlayer player)
        {
            if (onlinePlayers[player].IsInvisible && !CanUseTeleport) return CantUseTeleport;
            return null;
        }

        #endregion

        #region General Cleanup

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                // Destroy existing UI
                string guiInfo;
                if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }
        }

        #endregion

        #region Helper Methods

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        bool IsAdmin(BasePlayer player) => permission.UserHasGroup(player.UserIDString, "admin") || player.net?.connection?.authLevel > 0;

        bool IsInvisible(BasePlayer player) => onlinePlayers[player]?.IsInvisible ?? false;

        #endregion
    }
}
