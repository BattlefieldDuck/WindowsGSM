﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using WindowsGSM.Extensions;
using WindowsGSM.GameServers;
using WindowsGSM.GameServers.Components;
using WindowsGSM.GameServers.Configs;
using WindowsGSM.GameServers.Mods;
using WindowsGSM.Utilities;

namespace WindowsGSM.Services
{
    public class GameServerService : IHostedService, IDisposable
    {
        // Path
        public static readonly string BasePath = Path.GetDirectoryName(Environment.ProcessPath)!;
        public static readonly string ConfigsPath = Path.Combine(BasePath, "configs");
        public static readonly string ServersPath = Path.Combine(BasePath, "servers");
        public static readonly string StoragePath = Path.Combine(BasePath, "storage");

        public static event Action? GameServersHasChanged;
        public static void InvokeGameServersHasChanged() => GameServersHasChanged?.Invoke();

        public List<IGameServer> GameServers { get; private set; } = new();

        public Dictionary<Type, (string?, DateTime)> LatestVersions = new();

        private readonly ILogger<GameServerService> _logger;
        private Timer? _timer;

        public GameServerService(ILogger<GameServerService> logger)
        {
            _logger = logger;

            Directory.CreateDirectory(ConfigsPath);
            Directory.CreateDirectory(ServersPath);
            Directory.CreateDirectory(StoragePath);

            InitializeGameServers();
        }

        private void InitializeGameServers()
        {
            // Load serverGuids.json
            string path = Path.Combine(StoragePath, "serverGuids.json");

            if (File.Exists(path))
            {
                foreach (string guid in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new())
                {
                    string configPath = Path.Combine(ConfigsPath, $"{guid}.json");

                    if (File.Exists(configPath) && TryDeserialize(File.ReadAllText(configPath), out IGameServer? gameServer))
                    {
                        AddGameServer(gameServer);
                    }
                }
            }

            foreach (string configPath in Directory.GetFiles(ConfigsPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (TryDeserialize(File.ReadAllText(configPath), out IGameServer? gameServer) && !GameServers.Select(x => x.Config.Guid).Contains(gameServer.Config.Guid))
                {
                    AddGameServer(gameServer);
                }
            }

            UpdateServerGuidsJson();
        }

        private void UpdateServerGuidsJson()
        {
            string path = Path.Combine(StoragePath, "serverGuids.json");
            string contents = JsonSerializer.Serialize(GameServers.Select(x => x.Config.Guid.ToString()).Distinct(), new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(path, contents);
        }

        private void AddGameServer(IGameServer gameServer)
        {
            gameServer.Process.Exited += async (exitCode) => await OnGameServerExited(gameServer);
            GameServers.Add(gameServer);
            UpdateServerGuidsJson();
            GameServersHasChanged?.Invoke();
        }

        private async Task OnGameServerExited(IGameServer gameServer)
        {
            if (!gameServer.Status.IsRunning() && gameServer.Config.Advanced.AutoRestart)
            {
                gameServer.UpdateStatus(Status.Restarting);

                await Task.Delay(5000);
                await Start(gameServer);
            }
            else
            {
                gameServer.UpdateStatus(Status.Stopped);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public BasicConfig GetNewBasicConfig(Guid guid)
        { 
            string[] names = GameServers.Select(x => x.Config.Basic.Name).ToArray();
            int number = GameServers.Count;

            while (names.Contains($"WindowsGSM - Server #{++number}"));

            return new()
            {
                Name = $"WindowsGSM - Server #{number}",
                Directory = Path.Combine(ServersPath, guid.ToString()),
            };
        }

        public List<IGameServer> GetSupportedGameServers()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => x.GetInterfaces().Contains(typeof(IGameServer)) && !x.IsAbstract)
                .Select(x => (Activator.CreateInstance(x) as IGameServer)!).OrderBy(x => x.Name).ToList();
        }

        public async Task Create(IGameServer gameServer)
        {
            Directory.CreateDirectory(gameServer.Config.Basic.Directory);

            await gameServer.Config.Update();

            AddGameServer(gameServer);

            try
            {
                gameServer.UpdateStatus(Status.Creating);

                await gameServer.Create();

                gameServer.UpdateStatus(Status.Stopped);
            }
            catch
            {
                gameServer.UpdateStatus(Status.Stopped);
                throw;
            }
        }

        public async Task Update(IGameServer gameServer)
        {
            try
            {
                gameServer.UpdateStatus(Status.Updating);

                await gameServer.Update();

                gameServer.UpdateStatus(Status.Stopped);
            }
            catch
            {
                gameServer.UpdateStatus(Status.Stopped);
                throw;
            }
        }

        public async Task Delete(IGameServer gameServer)
        {
            try
            {
                gameServer.UpdateStatus(Status.Deleting);

                if (Directory.Exists(gameServer.Config.Basic.Directory))
                {
                    await DirectoryEx.DeleteAsync(gameServer.Config.Basic.Directory, true);
                }
                
                await gameServer.Config.Delete();

                GameServers.Remove(gameServer);
                GameServersHasChanged?.Invoke();
            }
            catch
            {
                gameServer.UpdateStatus(Status.Stopped);
                throw;
            }
        }

        public async Task Start(IGameServer gameServer)
        {
            try
            {
                gameServer.UpdateStatus(Status.Starting);

                await gameServer.Start();

                if (gameServer.Process.Process != null)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    gameServer.Process.Process.ProcessorAffinity = (IntPtr)gameServer.Config.Advanced.ProcessorAffinity;
#pragma warning restore CA1416 // Validate platform compatibility
                    gameServer.Process.Process.PriorityClass = ProcessPriorityClassExtensions.FromString(gameServer.Config.Advanced.ProcessPriority);
                }

                gameServer.UpdateStatus(Status.Started);
            }
            catch
            {
                gameServer.UpdateStatus(Status.Stopped);
                throw;
            }
        }

        public async Task Stop(IGameServer gameServer)
        {
            try
            {
                gameServer.UpdateStatus(Status.Stopping);

                await gameServer.Stop();

                gameServer.UpdateStatus(Status.Stopped);
            }
            catch
            {
                gameServer.UpdateStatus(Status.Started);
                throw;
            }
        }

        public async Task Restart(IGameServer gameServer)
        {
            await Stop(gameServer);
            await Start(gameServer);
        }

        public async Task Kill(IGameServer gameServer)
        {
            try
            {
                gameServer.UpdateStatus(Status.Killing);

                await TaskEx.Run(() => gameServer.Process.Kill());

                if (!await gameServer.Process.WaitForExit(5000))
                {
                    throw new Exception($"Process ID: {gameServer.Process.Id}");
                }

                gameServer.UpdateStatus(Status.Stopped);
            }
            catch
            {
                gameServer.UpdateStatus(Status.Stopped);
                throw;
            }
        }

        public async Task InstallMod(IGameServer gameServer, IMod mod, string version)
        {
            try
            {
                gameServer.UpdateStatus(Status.InstallingMod);

                await mod.Create(gameServer, version);

                gameServer.UpdateStatus(Status.Stopped);
            }
            catch
            {
                gameServer.UpdateStatus(Status.Stopped);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameServer"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool TryGetLatestVersion(IGameServer gameServer, out string? version, [NotNullWhen(true)] out DateTime? lastUpdate)
        {
            Type type = gameServer.GetType();

            if (LatestVersions.ContainsKey(type))
            {
                (version, lastUpdate) = LatestVersions[gameServer.GetType()];
            }
            else
            {
                (version, lastUpdate) = (null, null);
            }

            return version != null;
        }

        public bool TryCreateInstance(Type type, [NotNullWhen(true)] out IGameServer? gameServer)
        {
            gameServer = Activator.CreateInstance(type) as IGameServer;

            return gameServer != null;
        }

        public List<IGameServer> LoadAll()
        {
            List<IGameServer> gameServers = new();

            foreach (string filePath in Directory.GetFiles(ConfigsPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (TryDeserialize(File.ReadAllText(filePath), out IGameServer? server))
                {
                    gameServers.Add(server);
                }
            }

            return gameServers;
        }

        public IGameServer Load(Guid guid)
        {
            return Deserialize(File.ReadAllText(Path.Combine(ConfigsPath, $"{guid}.json")));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        /// <param name="server"></param>
        /// <returns></returns>
        private bool TryDeserialize(string json, [NotNullWhen(true)] out IGameServer? server)
        {
            try
            {
                server = Deserialize(json);
                return true;
            }
            catch
            {
                server = null;
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private IGameServer Deserialize(string json)
        {
            Dictionary<string, object>? config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            return config?["ClassName"].ToString() switch
            {
                nameof(MCBE) => new MCBE { Config = JsonSerializer.Deserialize<MCBE.Configuration>(json)! },
                nameof(TF2) => new TF2 { Config = JsonSerializer.Deserialize<TF2.Configuration>(json)! },
                _ => throw new Exception("Invalid JSON config file"),
            };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(60 * 5));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }
        
        private async void DoWork(object? state)
        {
            IEnumerable<IGameServer> gameServers = GameServers.DistinctBy(x => x.GetType());

            foreach (IGameServer gameServer in gameServers)
            {
                try
                {
                    LatestVersions[gameServer.GetType()] = (await gameServer.GetLatestVersion(), DateTime.Now);
                    _logger.LogInformation($"GetLatestVersion {LatestVersions[gameServer.GetType()]} ({gameServer.GetType().Name})");
                }
                catch
                {
                    _logger.LogError($"Fail to GetLatestVersion ({gameServer.GetType().Name})");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
