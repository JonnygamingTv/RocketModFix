using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rocket.AutoInstaller.Installation;
using SDG.Framework.Modules;
using SDG.Unturned;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Rocket.AutoInstaller
{
    public class Config
    {
        public bool? EnableCustomInstall { get; set; }
        public string? CustomInstallPath { get; set; }
        public bool BlockIfRocketInstalled { get; set; }
    }

    /// <summary>
    /// This is needed to run Coroutines.
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour;

    [UsedImplicitly]
    public class Module : IModuleNexus
    {
        private const string DiscordSupportURL = "https://discord.gg/z6VM7taWeG";

        public void initialize()
        {
            var assembly = typeof(Module).Assembly;
            var assemblyName = assembly.GetName();

            CommandWindow.Log($"Loading Rocket.AutoInstaller... {assemblyName.Name} {assemblyName.Version} this could take for a while!");
            CommandWindow.Log($"Discord Support: {DiscordSupportURL}");

            var modulesDirectory = Path.Combine(ReadWrite.PATH, "Modules");
            const string autoInstallerDll = "Rocket.AutoInstaller.dll";
            var workingDirectory = Path.GetDirectoryName(Directory
                .GetFiles(modulesDirectory, autoInstallerDll, SearchOption.AllDirectories)
                .FirstOrDefault() ?? throw new Exception($"Failed to find Rocket.AutoInstaller file: \"{autoInstallerDll}\", in: \"{modulesDirectory}\""))!;

            var configPath = Path.Combine(workingDirectory, "config.json");
            Config config;
            if (File.Exists(configPath))
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath))!;
            }
            else
            {
                config = new Config
                {
                    EnableCustomInstall = false,
                    CustomInstallPath = null,
                    BlockIfRocketInstalled = true,
                };
                File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
            }

            if (config.BlockIfRocketInstalled)
            {
                const string rocketUnturnedDll = "Rocket.Unturned.dll";
                // TODO: Once we do a caching make sure to ignore the own directory in Module code, cuz it will block the load process (see `BlockIfRocketInstalled`)!
                var rocketPath = Path.GetDirectoryName(Directory
                    .GetFiles(modulesDirectory, rocketUnturnedDll, SearchOption.AllDirectories)
                    .FirstOrDefault());
                if (rocketPath != null)
                {
                    CommandWindow.Log($"Rocket.Unturned is already installed in the Modules directory: \"{rocketPath}\". Please remove it to prevent potential issues.");
                    if (config.BlockIfRocketInstalled)
                    {
                        CommandWindow.Log(
                            "Installation via Rocket.AutoInstaller has been stopped because Rocket is already installed. " +
                            "To proceed, either delete Rocket.Unturned from the Modules directory or set `BlockIfRocketInstalled` to `false` in the configuration."
                        );
                        return;
                    }
                }
            }

            var instance = new GameObject();
            UnityObject.DontDestroyOnLoad(instance);
            var coroutineRunner = instance.AddComponent<CoroutineRunner>();
            coroutineRunner.StartCoroutine(Installer.Install(config));
        }
        public void shutdown()
        {
        }
    }
}