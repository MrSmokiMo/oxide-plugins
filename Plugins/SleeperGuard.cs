﻿using System;

namespace Oxide.Plugins
{
    [Info("SleeperGuard", "Wulf/lukespragg", "0.2.0", ResourceId = 1454)]
    [Description("Protects sleeping players from being killed and looted")]

    class SleeperGuard : CovalencePlugin
    {
        // Do NOT edit this file, instead edit SleeperGuard.json in oxide/config

        #region Initialization

        const string permAnimals = "sleeperguard.animals";
        const string permPlayers = "sleeperguard.players";
        const string permLoot = "sleeperguard.loot";

        void Init()
        {
            #if !RUST
            throw new NotSupportedException("This plugin does not support this game");
            #endif

            LoadDefaultConfig();
            permission.RegisterPermission(permAnimals, this);
            permission.RegisterPermission(permPlayers, this);
            permission.RegisterPermission(permLoot, this);
        }

        #endregion

        #region Configuration

        bool animalProtection;
        bool playerProtection;

        protected override void LoadDefaultConfig()
        {
            Config["Animals"] = animalProtection = GetConfig("Animals", true);
            Config["Players"] = playerProtection = GetConfig("Players", true);
            SaveConfig();
        }

        #endregion

        #region Damage Blocking

        #if RUST
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var victim = entity.ToPlayer();
            if (victim == null || !victim.IsSleeping()) return null;

            if (!(info.Initiator is BasePlayer) && animalProtection || HasPermission(victim.UserIDString, permAnimals)) return true;
            if ((info.Initiator is BasePlayer) && playerProtection || HasPermission(victim.UserIDString, permPlayers)) return true;

            return null;
        }
        #endif

        #endregion

        #region Loot Blocking

        #if RUST
        bool CanLootPlayer(BasePlayer target)
        {
            return !target.IsSleeping() || (!playerProtection && !HasPermission(target.UserIDString, permLoot));
        }
        #endif

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T) Convert.ChangeType(Config[name], typeof(T));
        }

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion
    }
}
