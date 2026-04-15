using Rocket.Core.Logging;
using Rocket.Unturned.Enumerations;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using Rocket.Core.Extensions;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned.Events
{
    public sealed class UnturnedPlayerEvents : UnturnedPlayerComponent
    {
        // =========================================================
        // INSTANCE CACHE
        // Avoids GetComponent<> (Unity hash-map walk) on every event.
        // Populated in Load(), removed in OnDestroy() – no leaks.
        // =========================================================
        private static readonly Dictionary<SDG.Unturned.Player, UnturnedPlayerEvents>
            _instances = new Dictionary<SDG.Unturned.Player, UnturnedPlayerEvents>(64);

        private static bool TryGet(SDG.Unturned.Player player,
                                   out UnturnedPlayerEvents inst,
                                   out UnturnedPlayer rp)
        {
            if (_instances.TryGetValue(player, out inst))
            {
                rp = UnturnedPlayer.FromPlayer(player);
                return true;
            }
            inst = null;
            rp = null;
            return false;
        }

        // =========================================================
        // LIFECYCLE
        // =========================================================
        protected override void Load()
        {
            _instances[Player.Player] = this;

            Player.Player.life.onStaminaUpdated += onUpdateStamina;

            var inv = Player.Player.inventory;
            inv.onInventoryAdded += onInventoryAdded;
            inv.onInventoryRemoved += onInventoryRemoved;
            inv.onInventoryResized += onInventoryResized;
            inv.onInventoryUpdated += onInventoryUpdated;
        }

        private void OnDestroy()
        {
            if (Player?.Player != null)
                _instances.Remove(Player.Player);
        }

        private void Start()
        {
            UnturnedEvents.triggerOnPlayerConnected(Player);
        }

        // =========================================================
        // STATIC ENGINE HOOKS
        // =========================================================
        internal static void TriggerReceive(SteamChannel instance, CSteamID d,
                                            byte[] a, int b, int size)
        {
            // Intentionally empty. Debug block removed; see git history.
        }

        internal static void InternalOnPlayerStatIncremented(SDG.Unturned.Player player, EPlayerStat stat)
        {
            if (!TryGet(player, out var inst, out var rp)) return;
            OnPlayerUpdateStat.TryInvoke(rp, stat);
            inst.OnUpdateStat.TryInvoke(rp, stat);
        }

        internal static void InternalOnGestureChanged(PlayerAnimator animator, EPlayerGesture g)
        {
            PlayerGesture rg;
            switch (g)
            {
                case EPlayerGesture.NONE: rg = PlayerGesture.None; break;
                case EPlayerGesture.INVENTORY_START: rg = PlayerGesture.InventoryOpen; break;
                case EPlayerGesture.INVENTORY_STOP: rg = PlayerGesture.InventoryClose; break;
                case EPlayerGesture.PICKUP: rg = PlayerGesture.Pickup; break;
                case EPlayerGesture.PUNCH_LEFT: rg = PlayerGesture.PunchLeft; break;
                case EPlayerGesture.PUNCH_RIGHT: rg = PlayerGesture.PunchRight; break;
                case EPlayerGesture.SURRENDER_START: rg = PlayerGesture.SurrenderStart; break;
                case EPlayerGesture.SURRENDER_STOP: rg = PlayerGesture.SurrenderStop; break;
                case EPlayerGesture.POINT: rg = PlayerGesture.Point; break;
                case EPlayerGesture.WAVE: rg = PlayerGesture.Wave; break;
                case EPlayerGesture.SALUTE: rg = PlayerGesture.Salute; break;
                case EPlayerGesture.ARREST_START: rg = PlayerGesture.Arrest_Start; break;
                case EPlayerGesture.ARREST_STOP: rg = PlayerGesture.Arrest_Stop; break;
                case EPlayerGesture.REST_START: rg = PlayerGesture.Rest_Start; break;
                case EPlayerGesture.REST_STOP: rg = PlayerGesture.Rest_Stop; break;
                case EPlayerGesture.FACEPALM: rg = PlayerGesture.Facepalm; break;
                default: return;
            }
            if (!TryGet(animator.player, out var inst, out var rp)) return;
            OnPlayerUpdateGesture.TryInvoke(rp, rg);
            inst.OnUpdateGesture.TryInvoke(rp, rg);
        }

        internal static void InternalOnStanceChanged(PlayerStance stance)
        {
            if (!TryGet(stance.player, out var inst, out var rp)) return;
            byte s = (byte)stance.stance;
            OnPlayerUpdateStance.TryInvoke(rp, s);
            inst.OnUpdateStance.TryInvoke(rp, s);
        }

        // =========================================================
        // LIFE STATS
        // =========================================================
        internal static void InternalOnTellHealth(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateHealth.TryInvoke(rp, life.health);
            inst.OnUpdateHealth.TryInvoke(rp, life.health);
        }

        internal static void InternalOnTellFood(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateFood.TryInvoke(rp, life.food);
            inst.OnUpdateFood.TryInvoke(rp, life.food);
        }

        internal static void InternalOnTellWater(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateWater.TryInvoke(rp, life.water);
            inst.OnUpdateWater.TryInvoke(rp, life.water);
        }

        internal static void InternalOnTellVirus(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateVirus.TryInvoke(rp, life.virus);
            inst.OnUpdateVirus.TryInvoke(rp, life.virus);
        }

        internal static void InternalOnTellBleeding(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateBleeding.TryInvoke(rp, life.isBleeding);
            inst.OnUpdateBleeding.TryInvoke(rp, life.isBleeding);
        }

        internal static void InternalOnTellBroken(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateBroken.TryInvoke(rp, life.isBroken);
            inst.OnUpdateBroken.TryInvoke(rp, life.isBroken);
        }

        internal static void InternalOnExperienceChanged(PlayerSkills skills, uint oldExp)
        {
            if (!TryGet(skills.player, out var inst, out var rp)) return;
            OnPlayerUpdateExperience.TryInvoke(rp, skills.experience);
            inst.OnUpdateExperience.TryInvoke(rp, skills.experience);
        }

        // =========================================================
        // DEATH / REVIVE
        // =========================================================
        internal static void InternalOnRevived(PlayerLife life)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerUpdateLife.TryInvoke(rp, life.health);
            inst.OnUpdateLife.TryInvoke(rp, life.health);

            Vector3 pos = life.transform.position;
            byte ang = MeasurementTool.angleToByte(life.transform.rotation.eulerAngles.y);
            OnPlayerRevive.TryInvoke(rp, pos, ang);
            inst.OnRevive.TryInvoke(rp, pos, ang);
        }

        internal static void InternalOnPlayerDeath(PlayerLife life, EDeathCause cause,
                                                    ELimb limb, CSteamID instigator)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerDeath.TryInvoke(rp, cause, limb, instigator);
            inst.OnDeath.TryInvoke(rp, cause, limb, instigator);
        }

        internal static void InternalOnPlayerDied(PlayerLife life, EDeathCause cause,
                                                   ELimb limb, CSteamID instigator)
        {
            if (!TryGet(life.player, out var inst, out var rp)) return;
            OnPlayerDead.TryInvoke(rp, Vector3.zero);
            inst.OnDead.TryInvoke(rp, Vector3.zero);
        }

        // =========================================================
        // CLOTHING
        // =========================================================
        internal static void InternalOnShirtChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Shirt, c.shirt, c.shirtQuality);

        internal static void InternalOnPantsChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Pants, c.pants, c.pantsQuality);

        internal static void InternalOnHatChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Hat, c.hat, c.hatQuality);

        internal static void InternalOnBackpackChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Backpack, c.backpack, c.backpackQuality);

        internal static void InternalOnVestChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Vest, c.vest, c.vestQuality);

        internal static void InternalOnMaskChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Mask, c.mask, c.maskQuality);

        internal static void InternalOnGlassesChanged(PlayerClothing c)
            => OnPlayerWear.TryInvoke(UnturnedPlayer.FromPlayer(c.player), Wearables.Glasses, c.glasses, c.glassesQuality);

        // =========================================================
        // POSITION
        // =========================================================
        public delegate void PlayerUpdatePosition(UnturnedPlayer player, Vector3 position);
        public static event PlayerUpdatePosition OnPlayerUpdatePosition;

        internal static void fireOnPlayerUpdatePosition(UnturnedPlayer player)
            => OnPlayerUpdatePosition.TryInvoke(player, player.Position);

        // =========================================================
        // CHAT
        // ref parameters force per-handler dispatch; GetInvocationList()
        // allocates once but is unavoidable here.
        // Early-exit on cancel avoids invoking remaining handlers needlessly.
        // =========================================================
        public delegate void PlayerChatted(UnturnedPlayer player, ref Color color,
                                           string message, EChatMode chatMode, ref bool cancel);
        public static event PlayerChatted OnPlayerChatted;

        internal static Color firePlayerChatted(UnturnedPlayer player, EChatMode chatMode,
                                                 Color color, string msg, ref bool cancel)
        {
            var ev = OnPlayerChatted;
            if (ev == null) return color;

            var list = ev.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    ((PlayerChatted)list[i])(player, ref color, msg, chatMode, ref cancel);
                    if (cancel) break; // no point invoking further handlers
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }
            return color;
        }

        // =========================================================
        // INSTANCE STAMINA HANDLER
        // =========================================================
        private void onUpdateStamina(byte stamina)
        {
            OnPlayerUpdateStamina.TryInvoke(Player, stamina);
            OnUpdateStamina.TryInvoke(Player, stamina);
        }

        // =========================================================
        // INSTANCE INVENTORY HANDLERS
        // Enum.Parse+ToString replaced with direct cast – zero allocation,
        // zero reflection, identical result since the SDK byte IS the enum value.
        // =========================================================
        private void onInventoryUpdated(byte page, byte index, ItemJar item)
        {
            var group = (InventoryGroup)page;
            OnPlayerInventoryUpdated.TryInvoke(Player, group, index, item);
            OnInventoryUpdated.TryInvoke(Player, group, index, item);
        }

        private void onInventoryResized(byte page, byte width, byte height)
        {
            var group = (InventoryGroup)page;
            OnPlayerInventoryResized.TryInvoke(Player, group, width, height);
            OnInventoryResized.TryInvoke(Player, group, width, height);
        }

        private void onInventoryRemoved(byte page, byte index, ItemJar item)
        {
            var group = (InventoryGroup)page;
            OnPlayerInventoryRemoved.TryInvoke(Player, group, index, item);
            OnInventoryRemoved.TryInvoke(Player, group, index, item);
        }

        private void onInventoryAdded(byte page, byte index, ItemJar item)
        {
            var group = (InventoryGroup)page;
            OnPlayerInventoryAdded.TryInvoke(Player, group, index, item);
            OnInventoryAdded.TryInvoke(Player, group, index, item);
        }

        // =========================================================
        // PUBLIC API – DELEGATES & EVENTS
        // =========================================================
        public delegate void PlayerUpdateBleeding(UnturnedPlayer player, bool bleeding);
        public static event PlayerUpdateBleeding OnPlayerUpdateBleeding;
        public event PlayerUpdateBleeding OnUpdateBleeding;

        public delegate void PlayerUpdateBroken(UnturnedPlayer player, bool broken);
        public static event PlayerUpdateBroken OnPlayerUpdateBroken;
        public event PlayerUpdateBroken OnUpdateBroken;

        public delegate void PlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer);
        public static event PlayerDeath OnPlayerDeath;
        public event PlayerDeath OnDeath;

        public delegate void PlayerDead(UnturnedPlayer player, Vector3 position);
        public static event PlayerDead OnPlayerDead;
        public event PlayerDead OnDead;

        public delegate void PlayerUpdateLife(UnturnedPlayer player, byte life);
        public static event PlayerUpdateLife OnPlayerUpdateLife;
        public event PlayerUpdateLife OnUpdateLife;

        public delegate void PlayerUpdateFood(UnturnedPlayer player, byte food);
        public static event PlayerUpdateFood OnPlayerUpdateFood;
        public event PlayerUpdateFood OnUpdateFood;

        public delegate void PlayerUpdateHealth(UnturnedPlayer player, byte health);
        public static event PlayerUpdateHealth OnPlayerUpdateHealth;
        public event PlayerUpdateHealth OnUpdateHealth;

        public delegate void PlayerUpdateVirus(UnturnedPlayer player, byte virus);
        public static event PlayerUpdateVirus OnPlayerUpdateVirus;
        public event PlayerUpdateVirus OnUpdateVirus;

        public delegate void PlayerUpdateWater(UnturnedPlayer player, byte water);
        public static event PlayerUpdateWater OnPlayerUpdateWater;
        public event PlayerUpdateWater OnUpdateWater;

        public enum PlayerGesture
        {
            None = 0, InventoryOpen = 1, InventoryClose = 2, Pickup = 3,
            PunchLeft = 4, PunchRight = 5, SurrenderStart = 6, SurrenderStop = 7,
            Point = 8, Wave = 9, Salute = 10, Arrest_Start = 11, Arrest_Stop = 12,
            Rest_Start = 13, Rest_Stop = 14, Facepalm = 15
        }
        public delegate void PlayerUpdateGesture(UnturnedPlayer player, PlayerGesture gesture);
        public static event PlayerUpdateGesture OnPlayerUpdateGesture;
        public event PlayerUpdateGesture OnUpdateGesture;

        public delegate void PlayerUpdateStance(UnturnedPlayer player, byte stance);
        public static event PlayerUpdateStance OnPlayerUpdateStance;
        public event PlayerUpdateStance OnUpdateStance;

        public delegate void PlayerRevive(UnturnedPlayer player, Vector3 position, byte angle);
        public static event PlayerRevive OnPlayerRevive;
        public event PlayerRevive OnRevive;

        public delegate void PlayerUpdateStat(UnturnedPlayer player, EPlayerStat stat);
        public static event PlayerUpdateStat OnPlayerUpdateStat;
        public event PlayerUpdateStat OnUpdateStat;

        public delegate void PlayerUpdateExperience(UnturnedPlayer player, uint experience);
        public static event PlayerUpdateExperience OnPlayerUpdateExperience;
        public event PlayerUpdateExperience OnUpdateExperience;

        public delegate void PlayerUpdateStamina(UnturnedPlayer player, byte stamina);
        public static event PlayerUpdateStamina OnPlayerUpdateStamina;
        public event PlayerUpdateStamina OnUpdateStamina;

        public delegate void PlayerInventoryUpdated(UnturnedPlayer player, InventoryGroup group, byte index, ItemJar item);
        public static event PlayerInventoryUpdated OnPlayerInventoryUpdated;
        public event PlayerInventoryUpdated OnInventoryUpdated;

        public delegate void PlayerInventoryResized(UnturnedPlayer player, InventoryGroup group, byte oldSize, byte newSize);
        public static event PlayerInventoryResized OnPlayerInventoryResized;
        public event PlayerInventoryResized OnInventoryResized;

        public delegate void PlayerInventoryRemoved(UnturnedPlayer player, InventoryGroup group, byte index, ItemJar item);
        public static event PlayerInventoryRemoved OnPlayerInventoryRemoved;
        public event PlayerInventoryRemoved OnInventoryRemoved;

        public delegate void PlayerInventoryAdded(UnturnedPlayer player, InventoryGroup group, byte index, ItemJar item);
        public static event PlayerInventoryAdded OnPlayerInventoryAdded;
        public event PlayerInventoryAdded OnInventoryAdded;

        public enum Wearables { Hat = 0, Mask = 1, Vest = 2, Pants = 3, Shirt = 4, Glasses = 5, Backpack = 6 }
        public delegate void PlayerWear(UnturnedPlayer player, Wearables wear, ushort id, byte? quality);
        public static event PlayerWear OnPlayerWear;
    }
}
