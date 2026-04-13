using Rocket.Core.Logging;
using System;
using UnityEngine;

namespace Rocket.Core.Utils
{
    internal class AutomaticShutdownWatchdog : MonoBehaviour
    {
        public static AutomaticShutdownWatchdog Instance;

        private bool started;
        private bool shutdownScheduled;

        private void Start()
        {
            Instance = this;

            if (!R.Settings.Instance.AutomaticShutdown.Enabled)
                return;

            int seconds = R.Settings.Instance.AutomaticShutdown.Interval;

            var shutdownTime = DateTime.UtcNow.AddSeconds(seconds);

            Logging.Logger.Log(
                $"This server will automatically shutdown in {seconds} seconds ({shutdownTime} UTC)");

            shutdownScheduled = true;

            TaskDispatcher.QueueOnMainThread(() =>
            {
                try
                {
                    R.Implementation.Shutdown();
                }
                catch (Exception ex)
                {
                    Logging.Logger.LogException(ex);
                }
            }, seconds);
        }
    }
}