using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core.Assets;
using Rocket.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.Core.Permissions
{
    public sealed class RocketPermissionsManager : MonoBehaviour, IRocketPermissionsProvider
    {
        private RocketPermissionsHelper helper;

        private void Awake()
        {
            try
            {
                if (R.Settings.Instance.WebPermissions.Enabled)
                {
                    lastWebPermissionsUpdate = DateTime.Now;
                    helper = new RocketPermissionsHelper(new WebXMLFileAsset<RocketPermissions>(new Uri(R.Settings.Instance.WebPermissions.Url + "?instance=" + R.Implementation.InstanceId)));
                    updateWebPermissions = true;
                }
                else
                {
                    helper = new RocketPermissionsHelper(new XMLFileAsset<RocketPermissions>(Environment.PermissionFile));
                }

            }
            catch (Exception ex)
            {
                Logging.Logger.LogException(ex);
            }
        }
        
        private bool updateWebPermissions = false;
        private DateTime lastWebPermissionsUpdate;

        /*private void FixedUpdate()
        {
            try
            {
                if (updateWebPermissions && R.Settings.Instance.WebPermissions.Interval > 0 && (DateTime.Now - lastWebPermissionsUpdate) > TimeSpan.FromSeconds(R.Settings.Instance.WebPermissions.Interval))
                {
                    lastWebPermissionsUpdate = DateTime.Now;
                    updateWebPermissions = false;
                    helper.permissions.Load((IAsset<RocketPermissions> asset) => {
                        updateWebPermissions = true;
                    });
                }
            }

            catch (Exception ex)
            {
                Logging.Logger.LogException(ex);
            }
        }*/

        public void Reload()
        {
            helper.permissions.Load();
            helper.permissions.Instance.GroupsDict.Clear();
            foreach (RocketPermissionsGroup _Group in helper.permissions.Instance.Groups)
            {
                if (_Group._Members.Count == 0)
                {
                    _Group._Members = new HashSet<string>(_Group.Members);
                }
                helper.permissions.Instance.GroupsDict[_Group.Id] = _Group;
            }
        }
        
        //public void ManualLoad() { Awake(); }
        public System.Collections.IEnumerator ManualUpdate() {
            while (R.Settings.Instance.WebPermissions.Enabled)
            {
                if (updateWebPermissions)
                {
                    updateWebPermissions = false;
                    try
                    {
                        helper.permissions.Load((IAsset<RocketPermissions> asset) =>
                        {
                            updateWebPermissions = true;
                        });
                    }
                    catch (Exception ex)
                    {
                        updateWebPermissions = true;
                        Logging.Logger.LogException(ex);
                    }
                }
                yield return new WaitForSeconds(R.Settings.Instance.WebPermissions.Interval);
            }
            yield break;
        }

        public bool HasPermission(IRocketPlayer player, List<string> permissions)
        {
            return helper.HasPermission(player, permissions);
        }
        public bool HasPermissionFast(IRocketPlayer player, string permission)
        {
            var playerPermissions = helper.GetPermissionDict(player.Id);

            if (playerPermissions.ContainsKey("*"))
                return true;

            if (playerPermissions.ContainsKey(permission))
                return true;

            foreach (var kv in playerPermissions)
            {
                if (!kv.Key.EndsWith(".*", StringComparison.Ordinal))
                    continue;

                var basePerm = kv.Key.Substring(0, kv.Key.Length - 2);

                if (permission.StartsWith(basePerm, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool HasPermission(string playerId, List<string> permissions)
        {
            return helper.HasPermission(playerId, permissions);
        }
        public bool HasPermission(IRocketPlayer player, HashSet<string> requestedPermissions)
        {
            return helper.HasPermission(player, requestedPermissions);
        } // does adding this break any plugin?

        public List<RocketPermissionsGroup> GetGroups(IRocketPlayer player, bool includeParentGroups)
        {
            return helper.GetGroups(player, includeParentGroups);
        }

        public List<RocketPermissionsGroup> GetGroups(string playerId, bool includeParentGroups)
        {
            return helper.GetGroups(playerId, includeParentGroups);
        }

        public List<Permission> GetPermissions(IRocketPlayer player)
        {
            return helper.GetPermissions(player);
        }

        public List<Permission> GetPermissions(string playerId)
        {
            return helper.GetPermissions(playerId);
        }

        public List<Permission> GetPermissions(IRocketPlayer player,List<string> requestedPermissions)
        {
            return helper.GetPermissions(player, requestedPermissions);
        }


        public List<Permission> GetPermissions(string playerId, List<string> requestedPermissions)
        {
            return helper.GetPermissions(playerId, requestedPermissions);
        }

        public RocketPermissionsProviderResult AddPlayerToGroup(string groupId, IRocketPlayer player)
        {
            return helper.AddPlayerToGroup(groupId,player);
        }

        public RocketPermissionsProviderResult AddPlayerToGroup(string groupId, string playerId)
        {
            return helper.AddPlayerToGroup(groupId, playerId);
        }

        public RocketPermissionsProviderResult RemovePlayerFromGroup(string groupId, IRocketPlayer player)
        {
            return helper.RemovePlayerFromGroup(groupId, player);
        }

        public RocketPermissionsProviderResult RemovePlayerFromGroup(string groupId, string playerId)
        {
            return helper.RemovePlayerFromGroup(groupId, playerId);
        }

        public RocketPermissionsGroup GetGroup(string groupId)
        {
            return helper.GetGroup(groupId);
        }

        public RocketPermissionsProviderResult SaveGroup(RocketPermissionsGroup group)
        {
            return helper.SaveGroup(group);
        }

        public RocketPermissionsProviderResult AddGroup(RocketPermissionsGroup group)
        {
            return helper.AddGroup(group);
        }

        public RocketPermissionsProviderResult DeleteGroup(RocketPermissionsGroup group)
        {
            return helper.DeleteGroup(group.Id);
        }

        public RocketPermissionsProviderResult DeleteGroup(string groupId)
        {
            return helper.DeleteGroup(groupId);
        }
    }
}