using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using WindowsGSM.Functions;

namespace WindowsGSM.Installer
{
    /// <summary>
    /// This script is very old, so it doesn't written in the best practice, but at least it works
    /// </summary>
    public class SteamCMD
    {
        private static readonly string _exeFile = "steamcmd.exe";
        private static readonly string _installPath = ServerPath.GetBin("steamcmd");
        private static readonly string _userDataPath = Path.Combine(_installPath, "userData.txt");
        private string _param;
        public string Error;

        public SteamCMD()
        {
            Directory.CreateDirectory(_installPath);
        }

        private static async Task<bool> Download()
        {
            Directory.CreateDirectory(_installPath);
            var exePath = Path.Combine(_installPath, _exeFile);
            if (File.Exists(exePath)) { return true; }

            try
            {
                var zipPath = Path.Combine(_installPath, "steamcmd.zip");
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip", zipPath);
                }

                //Extract steamcmd.zip and delete the zip
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, _installPath));
                await Task.Run(() => File.Delete(zipPath));

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Old parameter script
        public void SetParameter(string installDir, string modName, string appId, bool validate, bool loginAnonymous = true)
        {
            _param = $"+force_install_dir \"{installDir}\"";

            if (loginAnonymous)
            {
                _param += " +login anonymous";
            }
            else
            {
                string steamUser = null, steamPass = null;

                if (File.Exists(_userDataPath))
                {
                    string[] lines = File.ReadAllLines(_userDataPath);

                    foreach (string line in lines)
                    {
                        if (line[0] == '/' && line[1] == '/')
                        {
                            continue;
                        }

                        string[] keyvalue = line.Split(new char[] { '=' }, 2);
                        if (keyvalue[0] == "steamUser")
                        {
                            steamUser = keyvalue[1].Trim('\"');
                        }
                        else if (keyvalue[0] == "steamPass")
                        {
                            steamPass = keyvalue[1].Trim('\"');
                        }
                    }
                }
                else
                {
                    CreateUserDataTxtIfNotExist();
                }

                if (string.IsNullOrWhiteSpace(steamUser) || string.IsNullOrWhiteSpace(steamPass))
                {
                    _param = null;
                    return;
                }

                _param += $" +login \"{steamUser}\" \"{steamPass}\"";
            }

            _param += (string.IsNullOrWhiteSpace(modName) ? string.Empty : $" +app_set_config 90 mod {modName}") + $" +app_update {appId}" + (validate ? " validate" : "");

            if (appId == "90")
            {
                //Install 4 more times if hlds.exe
                for (int i = 0; i < 4; i++)
                {
                    _param += $" +app_update {appId}" + (validate ? " validate" : string.Empty);
                }
            }

            _param += " +quit";
        }

        // New parameter script
        public static string GetParameter(string forceInstallDir, string appId, bool validate = true, bool loginAnonymous = true, string modName = null, string custom = null)
        {
            var sb = new StringBuilder();

            // Set up force_install_dir parameter
            sb.Append($"+force_install_dir \"{forceInstallDir}\"");

            // Set up login parameter
            if (loginAnonymous)
            {
                sb.Append(" +login anonymous");
            }
            else
            {
                var (username, password) = GetSteamUsernamePassword();
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) { return null; }
                sb.Append($" +login \"{username}\" \"{password}\"");
            }

            // Set up app_set_config parameter
            sb.Append(!string.IsNullOrWhiteSpace(modName) ? $" +app_set_config {appId} mod \"{modName}\"" : string.Empty);

            // Install 4 more times if hlds.exe (appId = 90)
            for (var i = 0; i < 4; i++)
            {
                // Set up app_update parameter
                sb.Append($" +app_update {appId}");

                // Set up app_update extra parameter
                sb.Append(!string.IsNullOrWhiteSpace(custom) ? $" {custom}" : string.Empty); // custom parameter like -beta latest_experimental

                // Set up app_update validate parameter
                sb.Append(validate ? " validate" : string.Empty);

                if (appId != "90") { break; }
            }

            // Set up quit parameter
            sb.Append(" +quit");

            return sb.ToString();
        }

        // New parameter script
        private static (string, string) GetSteamUsernamePassword()
        {
            if (!File.Exists(_userDataPath))
            {
                return (null, null);
            }

            string username = null, password = null;
            foreach (var line in File.ReadAllLines(_userDataPath).ToList())
            {
                if (line[0] == '/' && line[1] == '/') { continue; } // Skip the line if it is a comment line
                var keyValue = line.Split(new[] { '=' }, 2);
                switch (keyValue[0])
                {
                    case "steamUser": username = keyValue[1].Substring(1, keyValue[1].Length - 2); break;
                    case "steamPass": password = keyValue[1].Substring(1, keyValue[1].Length - 2); break;
                }
            }

            return (username, password);
        }

        public async Task<Process> Run()
        {
            string exePath = Path.Combine(_installPath, _exeFile);
            if (!File.Exists(exePath))
            {
                //If steamcmd.exe not exists, download steamcmd.exe
                if (!await Download())
                {
                    Error = $"Fail to download {_exeFile}";
                    return null;
                }
            }

            if (_param == null)
            {
                Error = "Steam account is not set";
                return null;
            }

            //Console.WriteLine($"SteamCMD Param: {_param}");

            var firewall = new WindowsFirewall(_exeFile, exePath);
            if (!await firewall.IsRuleExist())
            {
                await firewall.AddRule();
            }

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = _installPath,
                    FileName = exePath,
                    Arguments = _param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            p.Start();

            return p;
        }

        public async Task<Process> Install(string serverId, string modName, string appId, bool validate = true, bool loginAnonymous = true)
        {
            // Fix the SteamCMD issue
            Directory.CreateDirectory(Path.Combine(ServerPath.GetServersServerFiles(serverId), "steamapps"));

            SetParameter(ServerPath.GetServersServerFiles(serverId), modName, appId, validate, loginAnonymous);
            Process p = await Run();
            SendEnterPreventFreeze(p);
            return p;
        }

        // New
        public static async Task<(Process, string)> UpdateEx(string serverId, string appId, bool validate = true, bool loginAnonymous = true, string modName = null, string custom = null, bool embedConsole = true)
        {
            string param = GetParameter(ServerPath.GetServersServerFiles(serverId), appId, validate, loginAnonymous, modName, custom);
            if (param == null)
            {
                return (null, "Steam account not set up");
            }

            string exePath = Path.Combine(_installPath, _exeFile);
            if (!File.Exists(exePath) && !await Download())
            {
                return (null, "Unable to download steamcmd");
            }

            // Fix the SteamCMD issue
            Directory.CreateDirectory(Path.Combine(ServerPath.GetServersServerFiles(serverId), "steamapps"));

            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = _installPath,
                    FileName = exePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            if (embedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(serverId);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return (p, null);
            }

            p.Start();
            return (p, null);
        }

        // Old
        public async Task<bool> Update(string serverId, string modName, string appId, bool validate, bool loginAnonymous = true)
        {
            SetParameter(Functions.ServerPath.GetServersServerFiles(serverId), modName, appId, validate, loginAnonymous);

            Process p = await Run();
            if (p == null)
            {
                return false;
            }

            SendEnterPreventFreeze(p);

            await Task.Run(() => p.WaitForExit());

            if (p.ExitCode != 0)
            {
                Error = $"Exit code: {p.ExitCode.ToString()}";
                return false;
            }

            return true;
        }

        private async void SendEnterPreventFreeze(Process p)
        {
            try
            {
                await Task.Delay(300000);

                // Send enter 3 times per 3 seconds
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(3000);

                    if (p == null || p.HasExited) { break; }
                    p.StandardInput.WriteLine(string.Empty);
                }

                // Wait 5 minutes
                await Task.Delay(300000);

                // Send enter 3 times per 3 seconds
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(3000);

                    if (p == null || p.HasExited) { break; }
                    p.StandardInput.WriteLine(string.Empty);
                }
            }
            catch
            {

            }
        }

        public string GetLocalBuild(string serverId, string appId)
        {
            string manifestFile = $"appmanifest_{appId}.acf";
            string manifestPath = Path.Combine(ServerPath.GetServersServerFiles(serverId), "steamapps", manifestFile);

            if (!File.Exists(manifestPath))
            {
                Error = $"{manifestFile} is missing.";
                return string.Empty;
            }

            string text;
            try
            {
                text = File.ReadAllText(manifestPath);
            }
            catch (Exception e)
            {
                Error = $"Fail to get local build {e.Message}";
                return string.Empty;
            }

            Regex regex = new Regex("\"buildid\".{1,}\"(.*?)\"");
            var matches = regex.Matches(text);

            if (matches.Count != 1 || matches[0].Groups.Count != 2)
            {
                Error = $"Fail to get local build";
                return string.Empty;
            }

            return matches[0].Groups[1].Value;
        }

        public async Task<string> GetRemoteBuild(string appId)
        {
            string exePath = Path.Combine(_installPath, "steamcmd.exe");
            if (!File.Exists(exePath))
            {
                //If steamcmd.exe not exists, download steamcmd.exe
                if (!await Download())
                {
                    Error = "Fail to download steamcmd.exe";
                    return string.Empty;
                }
            }

            WindowsFirewall firewall = new WindowsFirewall("steamcmd.exe", exePath);
            if (!await firewall.IsRuleExist())
            {
                await firewall.AddRule();
            }

            // Removes appinfo.vdf as a fix for not always getting up to date version info from SteamCMD.
            await Task.Run(() =>
            {
                string vdfPath = Path.Combine(_installPath, "appcache", "appinfo.vdf");
                try
                {
                    if (File.Exists(vdfPath))
                    {
                        File.Delete(vdfPath);
                        Debug.WriteLine($"Deleted appinfo.vdf ({vdfPath})");
                    }
                }
                catch
                {
                    Debug.WriteLine($"File to delete appinfo.vdf ({vdfPath})");
                }
            });

            Process p = new Process
            {
                StartInfo =
                {
                    FileName = exePath,
                    //Sometimes it fails to get if appID < 90
                    Arguments = $"+login anonymous +app_info_update 1 +app_info_print {appId} +app_info_print {appId} +app_info_print {appId} +app_info_print {appId} +logoff +quit",
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            p.Start();
            SendEnterPreventFreeze(p);

            string output = await p.StandardOutput.ReadToEndAsync();
            Regex regex = new Regex("\"public\"\r\n.{0,}{\r\n.{0,}\"buildid\".{1,}\"(.*?)\"");
            var matches = regex.Matches(output);

            if (matches.Count < 1 || matches[1].Groups.Count < 2)
            {
                Error = $"Fail to get remote build";
                return string.Empty;
            }

            return matches[0].Groups[1].Value;
        }

        public void CreateUserDataTxtIfNotExist()
        {
            if (!File.Exists(_userDataPath))
            {
                File.Create(_userDataPath).Dispose();

                using (TextWriter textwriter = new StreamWriter(_userDataPath))
                {
                    textwriter.WriteLine("// For security and compatibility reasons, WindowsGSM suggests you to create a new steam account.");
                    textwriter.WriteLine("// More info: (https://docs.windowsgsm.com/installer/steamcmd)");
                    textwriter.WriteLine("// ");
                    textwriter.WriteLine("// Username and password - No Steam Guard             (Supported + Auto update supported) (Recommended)");
                    textwriter.WriteLine("// Username and password - Steam Guard via Email      (Supported + Auto update supported)");
                    textwriter.WriteLine("// Username and password - Steam Guard via Smartphone (Supported + Auto update NOT supported)");
                    textwriter.WriteLine("// ");
                    textwriter.WriteLine("steamUser=\"\"");
                    textwriter.WriteLine("steamPass=\"\"");
                }
            }
        }
    }
}
