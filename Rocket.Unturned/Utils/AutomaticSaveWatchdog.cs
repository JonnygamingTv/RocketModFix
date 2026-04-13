using Rocket.Core.Logging;
using Rocket.Core.Utils;
using SDG.Unturned;
using System;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned.Utils
{
    internal class AutomaticSaveWatchdog : MonoBehaviour
    {
        public static AutomaticSaveWatchdog Instance;

        private int interval = 30;
        private bool enabled;

        private void Start()
        {
            Instance = this;

            enabled = U.Settings.Instance.AutomaticSave.Enabled;
            if (!enabled)
                return;

            if (U.Settings.Instance.AutomaticSave.Interval < interval)
            {
                Logger.LogError("AutomaticSave interval must be at least 30 seconds, using 30.");
            }
            else
            {
                interval = U.Settings.Instance.AutomaticSave.Interval;
            }

            Logger.Log($"Auto-save every {interval} seconds");

            ScheduleNext();
        }

        private void ScheduleNext()
        {
            TaskDispatcher.QueueOnMainThread(ExecuteSave, interval);
        }

        private void ExecuteSave()
        {
            if (!enabled)
                return;

            try
            {
                Logger.Log("Saving server");
                SaveManager.save();
            }
            finally
            {
                ScheduleNext(); // reschedule instead of polling
            }
        }
    }
}