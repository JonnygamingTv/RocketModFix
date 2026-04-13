using Rocket.Core.Logging;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using System;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned
{
    public class UnturnedPlayerMovement : UnturnedPlayerComponent
    {
        public bool VanishMode = false;

        // HOT STATE (avoid DateTime overhead in FixedUpdate)
        private float _nextCheckTime;
        private Vector3 _lastVector = new Vector3(0f, -1f, 0f);

        // Cache component reference (avoid GetComponent every tick)
        private PlayerMovement _movement;

        protected override void Load()
        {
            _movement = Player.GetComponent<PlayerMovement>();
        }

        private void FixedUpdate()
        {
            if (VanishMode)
                return;

            // faster than DateTime.Now comparisons (no struct overhead / no conversions)
            float time = Time.time;
            if (time < _nextCheckTime)
                return;

            _nextCheckTime = time + 1f;

            var movement = _movement;
            if (!movement) // recently added. Somehow fatal crash on QueueOnMainThread (Unity-level crash) // have not tested with this patch yet
            {
                movement = Player.GetComponent<PlayerMovement>();
                _movement = movement;
                if (!movement) return;
            }

            Vector3 pos = movement.real;

            // sentinel check (keep behavior identical)
            if (_lastVector.y != -1f)
            {
                float dx = Mathf.Abs(_lastVector.x - pos.x);
                float dy = pos.y - _lastVector.y;
                float dz = Mathf.Abs(_lastVector.z - pos.z);

                // early exit: avoid raycast unless needed
                if (dy > 15f)
                {
                    // Raycast optimization: reuse static direction
                    RaycastHit hit;

                    if (Physics.Raycast(pos, Vector3.down, out hit))
                    {
                        float floorDist = Mathf.Abs(hit.point.y - pos.y);

                        Logger.Log(
                            Player.DisplayName +
                            " moved x:" + pos.x +
                            " y:" + pos.y + "(+" + dy + ")" +
                            " z:" + pos.z +
                            " dist:" + floorDist
                        );
                    }
                }
            }

            _lastVector = pos;
        }
    }
}