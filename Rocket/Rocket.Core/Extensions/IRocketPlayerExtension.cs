using Rocket.API.Serialisation;
using Rocket.Core;
using System.Collections.Generic;

namespace Rocket.API
{
    public static class IRocketPlayerExtension
    {
        public static bool HasPermission(this IRocketPlayer player, string permission)
        {
            var provider = R.Permissions;

            if (player.IsAdmin)
                return true;

            // FAST PATH (no List)
            var rpm = provider as Core.Permissions.RocketPermissionsManager;
            if (rpm != null)
                return rpm.HasPermissionFast(player, permission);

            // FALLBACK PATH (legacy compatibility)
            return provider.HasPermission(player, new List<string>(1) { permission });
        }

        public static bool HasPermissions(this IRocketPlayer player, List<string> permissions)
        {
            return R.Permissions.HasPermission(player, permissions) || player.IsAdmin;
        }

        public static List<Permission> GetPermissions(this IRocketPlayer player)
        {
            return R.Permissions.GetPermissions(player);
        }
    }
}
