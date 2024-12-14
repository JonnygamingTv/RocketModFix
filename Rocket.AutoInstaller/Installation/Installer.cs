using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using SDG.Unturned;
using UnityEngine.Networking;

namespace Rocket.AutoInstaller.Installation
{
    public class Installer
    {
        public static IEnumerator Install(Config config)
        {
            var request = UnityWebRequest.Get(
                "https://api.github.com/repos/RocketModFix/RocketModFix/releases");
            request.SetRequestHeader("User-Agent", "RocketModFix");
            request.redirectLimit = 5;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                CommandWindow.LogError($"Installation Failed: An error occured while installing RocketModFix, response code: {request.responseCode}, error: {request.error}");
                yield break;
            }

            var responseContent = request.downloadHandler.text;
            var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(responseContent);
            var latestRelease = releases!.FirstOrDefault();
            if (latestRelease == null)
            {
                CommandWindow.LogError("Installation Failed: No Release Found.");
                yield break;
            }
            if (string.IsNullOrWhiteSpace(latestRelease.TagName))
            {
                CommandWindow.LogError("Installation Failed: Release Tag Name doesn't seems to be valid.");
                yield break;
            }
            var moduleAsset = latestRelease.Assets.FirstOrDefault(IsRocketModFixModule);
            if (moduleAsset == null)
            {
                CommandWindow.LogError("Installation Failed: Module not found.");
                yield break;
            }

            CommandWindow.LogWarning($"Preparing to install: " +
                                     $"RocketModFix {latestRelease.TagName} Released: {latestRelease.PublishedAt}");

            request = UnityWebRequest.Get(moduleAsset.BrowserDownloadUrl);
            request.SetRequestHeader("User-Agent", "RocketModFix");
            request.redirectLimit = 5;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                CommandWindow.LogError($"Installation Failed: An error occured while getting raw data of module {moduleAsset.Name}, response code: {request.responseCode}, error: {request.error}");
                yield break;
            }

            var rawData = request.downloadHandler.data;
            var releaseEntries = GetReleaseEntries(rawData);
            byte[]? rocketModuleData = null;
            List<byte[]> rocketLibraries = [];
            const string rocketEntryPointFileName = "Rocket.Unturned.dll";
            foreach (var releaseEntry in releaseEntries)
            {
                if (releaseEntry.Name == rocketEntryPointFileName)
                {
                    rocketModuleData = releaseEntry.Content;
                    continue;
                }
                if (releaseEntry.Name != rocketEntryPointFileName && releaseEntry.FileExtension == ".dll")
                {
                    rocketLibraries.Add(releaseEntry.Content);
                    continue;
                }
            }

            if (rocketModuleData == null)
            {
                CommandWindow.LogError($"Installation Failed: {rocketEntryPointFileName} Rocket Entry Point was not found.");
                yield break;
            }

            foreach (var rocketLibrary in rocketLibraries)
            {
                Assembly.Load(rocketLibrary);
            }

            var rocketModule = Assembly.Load(rocketModuleData);

            var types = GetLoadableTypes(rocketModule);
            var moduleType = types.FirstOrDefault(
                x => x.IsAbstract == false && typeof(IModuleNexus).IsAssignableFrom(x));
            if (moduleType == null)
            {
                CommandWindow.LogError("Installation Failed: Rocket Module Type cannot be found!");
                yield break;
            }

            try
            {
                if (Activator.CreateInstance(moduleType) is not IModuleNexus plugin)
                {
                    CommandWindow.LogError("Unable to create UnturnedGuard Module!");
                    yield break;
                }

                plugin.initialize();
            }
            catch (Exception ex)
            {
                CommandWindow.LogError("An error occured while creating UnturnedGuard Module! \n" + ex);
                yield break;
            }

            CommandWindow.LogWarning(
                $"Successfully installed: RocketModFix v{latestRelease.TagName}");
        }

        private static bool IsRocketModFixModule(GitHubAsset asset)
        {
            return asset.Name.Equals("Rocket.Unturned.Module.zip", StringComparison.Ordinal);
        }
        private static List<ReleaseEntry> GetReleaseEntries(byte[] assetData)
        {
            var entries = new List<ReleaseEntry>();
            using var memoryStream = new MemoryStream(assetData);
            using var zipArchive = new ZipArchive(memoryStream);
            foreach (var entry in zipArchive.Entries)
            {
                try
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entry.FullName);
                    var fileExtension = Path.GetExtension(entry.FullName);
                    var entryDirectoryName = Path.GetDirectoryName(entry.FullName)!;

                    using var stream = entry.Open();
                    var entryData = stream.CopyToArray();

                    entries.Add(new ReleaseEntry(entry.Name, entry.FullName, fileNameWithoutExtension, fileExtension,
                        entryDirectoryName, entryData));
                }
                catch (Exception ex)
                {
                    CommandWindow.LogError($"Error while collecting info about release entry: {entry.FullName} \n{ex}");
                }
            }
            return entries;
        }
        /// <summary>
        /// Safely returns the set of loadable types from an assembly.
        /// Algorithm from StackOverflow answer here:
        /// https://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> from which to load types.</param>
        /// <returns>
        /// The set of types from the <paramref name="assembly" />, or the subset
        /// of types that could be loaded if there was any error.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="assembly" /> is <see langword="null" />.
        /// </exception>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            try
            {
                return assembly.DefinedTypes.Select(x => x.AsType());
            }
            catch (ReflectionTypeLoadException ex)
            {
                CommandWindow.LogError($"An error occured while getting types for assembly: {assembly} \n" + ex);
                return ex.Types.Where(x => x is not null);
            }
        }
    }
}