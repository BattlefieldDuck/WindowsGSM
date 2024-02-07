using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace WindowsGSM.GameServer
{
    /// <summary>
    ///
    /// Notes:
    /// Both RedirectStandardInput or RedirectStandardOutput cannot use on WindowsGSM.
    /// RedirectStandardOutput is possible but it will break the input, if used both, the server can run successfully but the input become useless again.
    ///
    /// The solution for this is don't use neither RedirectStandardInput nor RedirectStandardOutput.
    /// Just use the traditional method to handle the server.
    ///
    /// </summary>
    class RUST
    {
        private readonly Functions.ServerConfig _serverData;

        public string Error;
        public string Notice;

        public const string FullName = "Rust Dedicated Server";
        public string StartPath = "RustDedicated.exe";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 1;
        public dynamic QueryMethod = new Query.A2S();

        public string Port = "28015";
        public string QueryPort = "28016";
        public string Defaultmap = "Procedural Map";
        public string Maxplayers = "50";
        public string Additional = string.Empty;

        public string AppId = "258550";

        public RUST(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            //Download server.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{rcon_password}}", _serverData.GetRCONPassword());
                configText = configText.Replace("{{port}}", _serverData.ServerPort);
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server.cfg");
            if (!File.Exists(configPath))
            {
                Notice = $"server.cfg not found ({configPath})";
            }

            string workingDir = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
            string srcdsPath = Path.Combine(workingDir, "RustDedicated.exe");

            string param = "-nographics -batchmode -silent-crashes";
            param += string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" +server.hostname \"{_serverData.ServerName}\"";
            param += string.IsNullOrWhiteSpace(_serverData.ServerIP) ? string.Empty : $" +server.ip {_serverData.ServerIP}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" +server.port {_serverData.ServerPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" +server.queryport {_serverData.ServerQueryPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $" +server.level \"{_serverData.ServerMap}\"";
            param += string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" +server.maxplayers {_serverData.ServerMaxPlayer}";

            foreach (string line in File.ReadLines(configPath))
            {
                param += $" {line}";
            }

            param += $" {_serverData.ServerParam}";

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = workingDir,
                    FileName = srcdsPath,
                    Arguments = param.TrimEnd()
                },
                EnableRaisingEvents = true
            };
            p.Start();

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "quit");
            });
        }

        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom);
            Error = error;
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            Error = $"Invalid Path! Fail to find {StartPath}";
            return File.Exists(Path.Combine(path, StartPath));
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
