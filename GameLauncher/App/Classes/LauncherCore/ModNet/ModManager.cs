﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using GameLauncher.App.Classes.LauncherCore.Global;
using GameLauncher.App.Classes.LauncherCore.Lists.JSON;
using GameLauncher.App.Classes.LauncherCore.Visuals;
using Newtonsoft.Json;

namespace GameLauncher.App.Classes.LauncherCore.ModNet
{
    /* http://localhost/Engine.svc/GetServerInformation */

    public class ModFile
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }
    }

    /* http://localhost/Engine.svc/Modding/GetModInfo */
    public class ModInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("created_at")]
        public long CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public long UpdatedAt { get; set; }

        [JsonProperty("files")]
        public List<ModFile> Files { get; set; }

        [JsonProperty("required_mods")]
        public List<string> RequiredMods { get; set; }
    }

    public static class ModManager
    {
        public static List<string> ModCache = new List<string>();

        static string ComputeSha256Hash(byte[] rawData)
        {
            /* Create a SHA256 */
            using (var sha256Hash = SHA256.Create())
            {
                /* ComputeHash - returns byte array */
                var bytes = sha256Hash.ComputeHash(rawData);

                /* Convert byte array to a string */
                var builder = new StringBuilder();
                foreach (var t in bytes)
                {
                    builder.Append(t.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static void LoadModCache()
        {
            if (File.Exists("modcache.json"))
            {
                ModCache = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("modcache.json"));
            }
            else
            {
                using (var fs = File.OpenWrite("modcache.json"))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write("[]");
                }
            }
        }

        public static void SaveModCache()
        {
            using (var fs = File.OpenWrite("modcache.json"))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(JsonConvert.SerializeObject(ModCache));
            }
        }

        public static List<ModInfo> GetMods(string server)
        {
            using (var wc = new WebClient())
            {
                var data = wc.DownloadString(URLs.Main + $"/servers/{server}/mods");

                return JsonConvert.DeserializeObject<List<ModInfo>>(data);
            }
        }

        public static bool Download(List<ModInfo> mods, string gameDir, string serverKey, System.Windows.Forms.Label PlayProgress, ProgressBarEx progress)
        {
            ServerList serverInfo;
            PlayProgress.Text = ("Downloading mods for " + serverKey).ToUpper();
            progress.Value = 0;

            int currentModCount = 0;

            using (var wc = new WebClient())
            {
                var data = wc.DownloadString(URLs.Main + $"/servers/{serverKey}");
                serverInfo = JsonConvert.DeserializeObject<ServerList>(data);
            }

            var modsDirectory = Path.Combine(gameDir, "MODS");
            var serverModsDirectory = Path.Combine(modsDirectory, serverKey);

            Directory.CreateDirectory(serverModsDirectory);

            foreach (var mod in mods)
            {
                int totalModsCount = mod.Files.Count;
                var url = new Uri(serverInfo.DistributionUrl + "/" + mod.Id + "/");

                int moddownloaded = 0;

                if (ModCache.Contains($"{serverKey}::{mod.Id}"))
                {
                    foreach (var file in mod.Files)
                    {
                        if (!File.Exists(Path.Combine(serverModsDirectory, file.Path)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(serverModsDirectory, file.Path)));
                            File.Create(Path.Combine(serverModsDirectory, file.Path)).Dispose();
                        }

                        var computedHash = ComputeSha256Hash(File.ReadAllBytes(Path.Combine(serverModsDirectory, file.Path)));
                        if (computedHash != file.Hash)
                        {
                            moddownloaded++;
                            var wc = new WebClient();
                            PlayProgress.Text = ("Downloading " + serverKey + " files: " + file.Path + " (" + moddownloaded + "/" + totalModsCount + ")").ToUpper();
                            var fileData = wc.DownloadData(url + file.Path);
                            using (var fs = File.OpenWrite(Path.Combine(serverModsDirectory, file.Path)))
                            using (var bw = new BinaryWriter(fs))
                            {
                                bw.Write(fileData);
                            }
                        }
                    }

                    continue;
                }

                using (var wc = new WebClient())
                {
                    foreach (var file in mod.Files)
                    {
                        try
                        {
                            currentModCount++;

                            PlayProgress.Text = ("Downloading " + serverKey + " files: " + file.Path + " (" + currentModCount + "/" + totalModsCount + ") ").ToUpper();
                            progress.Value = Convert.ToInt32(Decimal.Divide(currentModCount, totalModsCount) * 100);
                            progress.Width = Convert.ToInt32(Decimal.Divide(currentModCount, totalModsCount) * 519);

                            System.Windows.Forms.Application.DoEvents();

                            var fileData = wc.DownloadData(url + file.Path);

                            if (!File.Exists(Path.Combine(serverModsDirectory, file.Path))) {
                                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(serverModsDirectory, file.Path)));
                                File.Create(Path.Combine(serverModsDirectory, file.Path)).Dispose();
                            }

                            var computedHash = ComputeSha256Hash(fileData);
                            if (computedHash != file.Hash)
                            {
                                /* TODO: Redownload file! */
                            }

                            if (file.Path.Contains("/"))
                            {
                                var dirName = Path.GetDirectoryName(file.Path);
                                if (dirName != null)
                                {
                                    Directory.CreateDirectory(Path.Combine(serverModsDirectory, dirName));
                                }
                            }

                            using (var fs = File.OpenWrite(Path.Combine(serverModsDirectory, file.Path)))
                            using (var bw = new BinaryWriter(fs))
                            {
                                bw.Write(fileData);
                            }
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(e.Message, "ModNet Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                ModCache.Add($"{serverKey}::{mod.Id}");
            }

            var uniqueFiles = mods.SelectMany(m => m.Files).Distinct().ToList();

            SaveModCache();

            using (var fs = new FileStream(Path.Combine(gameDir, "ModManager.dat"), FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(uniqueFiles.Count);

                foreach (var file in uniqueFiles)
                {
                    var originalPath = Path.Combine(gameDir, file.Path).Replace("/", "\\").ToUpper();
                    var modPath = Path.Combine(serverModsDirectory, file.Path).Replace("/", "\\").ToUpper();

                    bw.Write(originalPath.Length);
                    bw.Write(originalPath.ToCharArray());
                    bw.Write(modPath.Length);
                    bw.Write(modPath.ToCharArray());
                }
            }

            return true;
        }

        public static void ResetModDat(string gameDir)
        {
            File.Delete(Path.Combine(gameDir, "ModManager.dat"));
        }
    }
}
