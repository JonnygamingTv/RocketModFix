using Rocket.Core.Logging;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Items;
using System.Linq;

namespace Rocket.Unturned.Commands
{
    public class CommandI : IRocketCommand
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
            get { return "i"; }
        }

        public string Help
        {
            get { return "Gives yourself an item";}
        }

        public string Syntax
        {
            get { return "<id> [amount]"; }
        }

        public List<string> Aliases
        {
            get { return new List<string>() { "item" }; }
        }

        public List<string> Permissions
        {
            get { return new List<string>() { "rocket.item" , "rocket.i" }; }
        }
        private static readonly List<ItemAsset> sortedAssets = new List<ItemAsset>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            if (command.Length == 0 || command.Length > 2)
            {
                UnturnedChat.Say(player, U.Translate("command_generic_invalid_parameter"));
                throw new WrongUsageOfCommandException(caller, this);
            }

            ushort id = 0;
            byte amount = 1;

            string itemString = command[0];

            if(sortedAssets.Count == 0)
            {
                Assets.find(sortedAssets); // only populate once
                sortedAssets.RemoveAll(a => a.itemName == null); // clear all nullNames
                sortedAssets.Sort((a, b) => a.itemName.Length - b.itemName.Length); // order by length of names
            }
            Asset? a = null;
            if (ushort.TryParse(itemString, out id))
            {
                a = SDG.Unturned.Assets.find(EAssetType.ITEM, id);
            }
            else
            {
                string search = itemString; // no ToLower()

                for (int i = 0; i < sortedAssets.Count; i++)
                {
                    var asset = sortedAssets[i];
                    var name = asset.itemName;

                    if (name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        a = asset;
                        id = asset.id;
                        break; // first match = shortest name match due to sort
                    }
                }

                if (string.IsNullOrWhiteSpace(itemString) || id == 0)
                {
                    UnturnedChat.Say(player, U.Translate("command_generic_invalid_parameter"));
                    throw new WrongUsageOfCommandException(caller, this);
                }
            }

            if ((command.Length == 2 && !byte.TryParse(command[1], out amount)) || a == null)
            {
                UnturnedChat.Say(player, U.Translate("command_generic_invalid_parameter"));
                throw new WrongUsageOfCommandException(caller, this);
            }

            string assetName = ((ItemAsset)a).itemName;

            if (U.Settings.Instance.EnableItemBlacklist && !player.HasPermission("itemblacklist.bypass"))
            {
                if (player.HasPermission("item." + id)) {
                    UnturnedChat.Say(player, U.Translate("command_i_blacklisted"));
                    return;
                }
            }

            if (U.Settings.Instance.EnableItemSpawnLimit && !player.HasPermission("itemspawnlimit.bypass"))
            {
                if (amount > U.Settings.Instance.MaxSpawnAmount)
                {
                    UnturnedChat.Say(player, U.Translate("command_i_too_much", U.Settings.Instance.MaxSpawnAmount));
                    return;
                }
            }

            if (player.GiveItem(id, amount))
            {
                Logger.Log(U.Translate("command_i_giving_console", player.DisplayName, id, amount));
                UnturnedChat.Say(player, U.Translate("command_i_giving_private", amount, assetName, id));
            }
            else
            {
                UnturnedChat.Say(player, U.Translate("command_i_giving_failed_private", amount, assetName, id));
            }
        }
    }
}
