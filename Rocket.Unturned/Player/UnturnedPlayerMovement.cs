using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned
{
    public class UnturnedPlayerMovement : UnturnedPlayerComponent
    {
        public bool VanishMode = false;

        // Unity time float – no DateTime struct allocation, no conversions.
        private float _nextCheckTime;

        // Sentinel: y == -1f means "no previous sample yet".
        private Vector3 _lastPos = new Vector3(0f, -1f, 0f);

        // Cached once in Load() to avoid GetComponent<> every FixedUpdate tick.
        private PlayerMovement _movement;

        protected override void Load()
        {
            _movement = Player.Player.GetComponent<PlayerMovement>();
        }

        private void FixedUpdate()
        {
            // Vanish-mode players are intentionally exempt from movement logging.
            if (VanishMode)
                return;

            // Setting may be toggled at runtime; check it first so we bail out
            // before even touching Time.time when logging is disabled.
            if (!U.Settings.Instance.LogSuspiciousPlayerMovement)
                return;

            float now = Time.time;
            if (now < _nextCheckTime)
                return;

            _nextCheckTime = now + 1f;

            // Guard against the component being destroyed or not yet ready.
            if (_movement == null)
            {
                _movement = Player.Player.GetComponent<PlayerMovement>();
                if (_movement == null) return;
            }

            Vector3 pos = _movement.real;

            // Skip the very first sample (sentinel y == -1).
            if (_lastPos.y != -1f)
            {
                float dy = pos.y - _lastPos.y;

                if (dy > 15f)
                {
                    // Only raycast when the suspicious threshold is exceeded.
                    float floorDist = 0f;
                    if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit))
                        floorDist = Mathf.Abs(hit.point.y - pos.y);

                    Logger.Log(string.Format(
                        "{0} moved x:{1:F1} y:{2:F1} (+{3:F1}) z:{4:F1} in the last second (floor dist: {5:F1})",
                        Player.DisplayName, pos.x, pos.y, dy, pos.z, floorDist));
                }
            }

            _lastPos = pos;
        }
    }
}