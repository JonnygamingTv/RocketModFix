using Rocket.API;
using Rocket.API.Extensions;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rocket.Unturned.Commands
{
    public class CommandV : IRocketCommand
    {
        public AllowedCaller AllowedCaller
        {
            get
            {
                return AllowedCaller.Player;
            }
        }

        public string Name
        {
            get { return "v"; }
        }

        public string Help
        {
            get { return "Gives yourself an vehicle";}
        }

        public string Syntax
        {
            get { return "<id>"; }
        }

        public List<string> Aliases
        {
            get { return new List<string>(); }
        }

        public List<string> Permissions
        {
            get { return new List<string>() { "rocket.v", "rocket.vehicle" }; }
        }

        private static readonly List<VehicleAsset> assets = new List<VehicleAsset>();
        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            if (command.Length != 1)
            {
                UnturnedChat.Say(caller, U.Translate("command_generic_invalid_parameter"));
                throw new WrongUsageOfCommandException(caller, this);
            }
            Asset? a = null;
            ushort id;
            if (ushort.TryParse(command[0], out id))
            {
                a = Assets.find(EAssetType.VEHICLE, id);
            }
            else
            {
                string itemString = command[0];

                if (string.IsNullOrWhiteSpace(itemString))
                {
                    UnturnedChat.Say(caller, U.Translate("command_generic_invalid_parameter"));
                    throw new WrongUsageOfCommandException(caller, this);
                }

                if (assets.Count == 0)
                {
                    Assets.find(assets);
                    assets.RemoveAll(v => v.vehicleName == null);
                    assets.Sort((a, b) => a.vehicleName.Length - b.vehicleName.Length);
                }

                for (int i = 0; i < assets.Count; i++)
                {
                    VehicleAsset ia = assets[i];
                    var name = ia.vehicleName;
                    if (name.IndexOf(itemString, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        a = ia;
                        id = ia.id;
                        break;
                    }
                }
            }

            if (a == null)
            {
                UnturnedChat.Say(caller, U.Translate("command_generic_invalid_parameter"));
                throw new WrongUsageOfCommandException(caller, this);
            }

            string assetName = ((VehicleAsset)a).vehicleName;

            if (U.Settings.Instance.EnableVehicleBlacklist && !player.HasPermission("vehicleblacklist.bypass"))
            {
                if (a is VehicleRedirectorAsset ra)
                {
                    id = ra.TargetVehicle.Find().id; // legacy IDs
                }
                if (player.HasPermission("vehicle." + id))
                {
                    UnturnedChat.Say(caller, U.Translate("command_v_blacklisted"));
                    return;
                }
            }

            if (VehicleTool.SpawnVehicleForPlayer(player.Player, a))
            {
                Logger.Log(U.Translate("command_v_giving_console", player.CharacterName, id));
                UnturnedChat.Say(caller, U.Translate("command_v_giving_private", assetName, id));
            }
            else
            {
                UnturnedChat.Say(caller, U.Translate("command_v_giving_failed_private", assetName, id));
            }
        }
    }
}