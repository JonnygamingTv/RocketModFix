using Rocket.API.Extensions;
using Rocket.Unturned.Events;
using SDG.Unturned;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Rocket.Unturned.Player
{
    public sealed class UnturnedPlayerFeatures : UnturnedPlayerComponent
    {
        public DateTime Joined = DateTime.Now;

        private Color? color;
        internal Color? Color
        {
            get => color;
            set => color = value;
        }

        private bool vanishMode;

        public bool VanishMode
        {
            get => vanishMode;
            set
            {
                if (vanishMode == value) return; // early exit (huge in spam cases)

                vanishMode = value;

                var movement = Player.GetComponent<UnturnedPlayerMovement>();
                var pMovement = Player.GetComponent<PlayerMovement>();

                movement.VanishMode = value;
                pMovement.canAddSimulationResultsToUpdates = !value;

                if (!value)
                {
                    pMovement.updates.Add(
                        new PlayerStateUpdate(
                            pMovement.real,
                            Player.Player.look.angle,
                            Player.Player.look.rot));

                    pMovement.isUpdated = true;
                    PlayerManager.updates++;
                }
            }
        }

        private bool godMode;

        public bool GodMode
        {
            get => godMode;
            set
            {
                if (godMode == value) return;

                godMode = value;

                // base reset always
                ResetStats();

                if (value)
                {
                    SubscribeGodMode();
                }
                else
                {
                    UnsubscribeGodMode();
                }
            }
        }

        private void ResetStats()
        {
            Player.Bleeding = false;
            Player.Broken = false;
            Player.Infection = 0;
            Player.Hunger = 0;
            Player.Thirst = 0;
            Player.Stamina = 100;
            Player.Heal(100);
        }

        private void SubscribeGodMode()
        {
            var ev = Player.Events;

            ev.OnUpdateHealth += e_OnPlayerUpdateHealth;
            ev.OnUpdateBleeding += e_OnPlayerUpdateBleeding;
            ev.OnUpdateBroken += e_OnPlayerUpdateBroken;
            ev.OnUpdateWater += e_OnPlayerUpdateWater;
            ev.OnUpdateFood += e_OnPlayerUpdateFood;
            ev.OnUpdateVirus += e_OnPlayerUpdateVirus;
            ev.OnUpdateStamina += e_OnPlayerUpdateStamina;
        }

        private void UnsubscribeGodMode()
        {
            var ev = Player.Events;

            ev.OnUpdateHealth -= e_OnPlayerUpdateHealth;
            ev.OnUpdateBleeding -= e_OnPlayerUpdateBleeding;
            ev.OnUpdateBroken -= e_OnPlayerUpdateBroken;
            ev.OnUpdateWater -= e_OnPlayerUpdateWater;
            ev.OnUpdateFood -= e_OnPlayerUpdateFood;
            ev.OnUpdateVirus -= e_OnPlayerUpdateVirus;
            ev.OnUpdateStamina -= e_OnPlayerUpdateStamina;
        }

        // ─────────────────────────────────────────────────────────────
        // Position tracking (HOT PATH FIX)
        // ─────────────────────────────────────────────────────────────

        private Vector3 oldPosition;
        private const float PositionEpsilonSqr = 0.0001f;

        private void FixedUpdate()
        {
            Vector3 pos = Player.Position;

            // squared distance avoids Vector3 operator overload + allocation risk
            if ((pos - oldPosition).sqrMagnitude > PositionEpsilonSqr)
            {
                oldPosition = pos;
                UnturnedPlayerEvents.fireOnPlayerUpdatePosition(Player);
            }

            if (!initialCheck && (DateTime.Now - Joined).TotalSeconds > 3)
            {
                Check();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Name validation (CACHE REGEX)
        // ─────────────────────────────────────────────────────────────

        private bool initialCheck;

        private static Regex cachedRegex;

        private void Check()
        {
            initialCheck = true;

            if (!U.Settings.Instance.CharacterNameValidation)
                return;

            cachedRegex ??= new Regex(
                U.Settings.Instance.CharacterNameValidationRule,
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            string username = Player.CharacterName;

            if (!cachedRegex.IsMatch(username))
            {
                Provider.kick(Player.CSteamID, U.Translate("invalid_character_name"));
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Event handlers (already optimal, just no-op safe)
        // ─────────────────────────────────────────────────────────────

        private void e_OnPlayerUpdateVirus(UnturnedPlayer player, byte virus)
        {
            if (virus < 100) Player.Infection = 0;
        }

        private void e_OnPlayerUpdateFood(UnturnedPlayer player, byte food)
        {
            if (food < 100) Player.Hunger = 0;
        }

        private void e_OnPlayerUpdateWater(UnturnedPlayer player, byte water)
        {
            if (water < 100) Player.Thirst = 0;
        }

        private void e_OnPlayerUpdateBroken(UnturnedPlayer player, bool broken)
        {
            if (broken) Player.Broken = false;
        }

        private void e_OnPlayerUpdateBleeding(UnturnedPlayer player, bool bleeding)
        {
            if (bleeding) Player.Bleeding = false;
        }

        private void e_OnPlayerUpdateHealth(UnturnedPlayer player, byte health)
        {
            if (health < 100) Player.Heal(100);
        }

        private void e_OnPlayerUpdateStamina(UnturnedPlayer player, byte stamina)
        {
            if (stamina < 100) Player.Stamina = 100;
        }
    }
}