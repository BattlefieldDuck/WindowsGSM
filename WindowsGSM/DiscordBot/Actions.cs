using Discord;
using System;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using WindowsGSM.Functions;

namespace WindowsGSM.DiscordBot
{
    public class Actions
    {
        public async Task<Embed> GetServerList(string userId)
        {
            var embed = new EmbedBuilder { Color = Color.Teal };
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;

                var list = WindowsGSM.GetServerListByUserId(userId);

                var ids = string.Empty;
                var status = string.Empty;
                var servers = string.Empty;

                foreach (var(id, state, server) in list)
                {
                    ids += $"`{id}`\n";
                    status += $"`{state}`\n";
                    servers += $"`{server}`\n";
                }

                embed.AddField("ID", ids, inline: true);
                embed.AddField("Status", status, inline: true);
                embed.AddField("Server Name", servers, inline: true);
            });

            return embed.Build();
        }

        private async Task<string> GetServerName(string serverId)
        {
            return await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                return WindowsGSM.GetServerName(serverId);
            });
        }

        public async Task<string> GetServerPermissions(string userId)
        {
            var serverIds = Configs.GetServerIdsByAdminId(userId);
            return serverIds.Contains("0")
                ? "You have full permission.\nCommands: `check`, `list`, `start`, `stop`, `restart`, `send`, `backup`, `update`, `stats`"
                : $"You have permission on servers (`{string.Join(",", serverIds.ToArray())}`)\nCommands: `check`, `start`, `stop`, `restart`, `send`, `backup`, `update`, `stats`";

        }

        public async Task StartServer(SocketInteraction interaction, string serverId)
        {
            var serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                var message = string.Empty;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    var serverStatus = WindowsGSM.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Stopped)
                    {
                        var started = await WindowsGSM.StartServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        message = $"Server {serverName}(ID: {serverId}) {(started ? "Started" : "Fail to Start")}.";
                    }
                    else if (serverStatus == MainWindow.ServerStatus.Started)
                    {
                        message = $"Server {serverName}(ID: {serverId}) already Started.";
                    }
                    else
                    {
                        message = $"Server {serverName}(ID: {serverId}) currently in {serverStatus.ToString()} state, not able to start.";
                    }

                    await SendServerEmbed(interaction, message, Color.Green, serverId,
                        WindowsGSM.GetServerStatus(serverId).ToString(), WindowsGSM.GetServerName(serverId));
                }
                else
                {
                    await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) does not exists.");
                }
            });
        }

        public async Task StopServer(SocketInteraction interaction, string serverId)
        {
            var serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                var message = string.Empty;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    var serverStatus = WindowsGSM.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Started)
                    {
                        var started = await WindowsGSM.StopServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        message = $"Server {serverName}(ID: {serverId}) {(started ? "Stopped" : "Fail to Stop")}.";
                    }
                    else if (serverStatus == MainWindow.ServerStatus.Stopped)
                    {
                        message = $"Server {serverName}(ID: {serverId}) already Stopped.";
                    }
                    else
                    {
                        message = $"Server {serverName}(ID: {serverId}) currently in {serverStatus.ToString()} state, not able to stop.";
                    }

                    await SendServerEmbed(interaction, message, Color.Orange, serverId,
                        WindowsGSM.GetServerStatus(serverId).ToString(), WindowsGSM.GetServerName(serverId));
                }
                else
                {
                    await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) does not exists.");
                }
            });
        }

        public async Task RestartServer(SocketInteraction interaction, string serverId)
        {
            var serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                var message = string.Empty;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    var serverStatus = WindowsGSM.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Started)
                    {
                        var started = await WindowsGSM.RestartServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        message = $"Server {serverName}(ID: {serverId}) {(started ? "Restarted" : "Fail to Restart")}.";
                    }
                    else
                    {
                        message = $"Server {serverName}(ID: {serverId}) currently in {serverStatus.ToString()} state, not able to restart.";
                    }

                    await SendServerEmbed(interaction, message, Color.Blue, serverId,
                        WindowsGSM.GetServerStatus(serverId).ToString(), WindowsGSM.GetServerName(serverId));
                }
                else
                {
                    await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) does not exists.");
                }
            });
        }

        public async Task SendServerCommand(SocketInteraction interaction, string serverId, string command)
        {
            var serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    var serverStatus = WindowsGSM.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Started)
                    {
                        var sent = await WindowsGSM.SendCommandById(serverId, command,
                            interaction.User.Id.ToString(), interaction.User.Username);
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) {(sent ? "Command sent" : "Fail to send command")}. | `{command}`");
                    }
                    else
                    {
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) currently in {serverStatus.ToString()} state, not able to send command.");
                    }
                }
                else
                {
                    await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) does not exists.");
                }
            });
        }

        public async Task BackupServer(SocketInteraction interaction, string serverId)
        {
            var serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    var serverStatus = WindowsGSM.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Stopped)
                    {
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) Backup started - this may take some time.");
                        var backuped = await WindowsGSM.BackupServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) {(backuped ? "Backup Complete" : "Failed to Backup")}.");
                    }
                    else if (serverStatus == MainWindow.ServerStatus.Backuping)
                    {
                        await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) already backing up.");
                    }
                    else
                    {
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) currently in {serverStatus.ToString()} state, not able to backup.");
                    }
                }
                else
                {
                    await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) does not exists.");
                }
            });
        }

        public async Task UpdateServer(SocketInteraction interaction, string serverId)
        {
            var serverName = await GetServerName(serverId);
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(serverId))
                {
                    var serverStatus = WindowsGSM.GetServerStatus(serverId);
                    if (serverStatus == MainWindow.ServerStatus.Stopped)
                    {
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) Update started - this may take some time.");
                        var updated = await WindowsGSM.UpdateServerById(serverId, interaction.User.Id.ToString(),
                            interaction.User.Username);
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) {(updated ? "Updated" : "Fail to Update")}.");
                    }
                    else if (serverStatus == MainWindow.ServerStatus.Updating)
                    {
                        await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) already Updating.");
                    }
                    else
                    {
                        await interaction.FollowupAsync(
                            $"Server {serverName}(ID: {serverId}) currently in {serverStatus} state, not able to update.");
                    }
                }
                else
                {
                    await interaction.FollowupAsync($"Server {serverName}(ID: {serverId}) does not exists.");
                }
            });
        }

        public async Task GetServerStats(SocketInteraction interaction)
        {
            var system = new SystemMetrics();
            await Task.Run(() => system.GetCPUStaticInfo());
            await Task.Run(() => system.GetRAMStaticInfo());
            await Task.Run(() => system.GetDiskStaticInfo());

            await interaction.RespondAsync(embed: (await GetMessageEmbed(system)).Build());
        }

        private async Task SendServerEmbed(SocketInteraction interaction, string message, Color color, string serverId, string serverStatus,
            string serverName)
        {
            var embed = new EmbedBuilder { Color = color };
            embed.AddField("ID", serverId, inline: true);
            embed.AddField("Status", serverStatus, inline: true);
            embed.AddField("Server Name", serverName, inline: true);

            await interaction.FollowupAsync(text:message, embed: embed.Build());
        }

        private static string GetProgressBar(double progress)
        {
            const int MAX_BLOCK = 23;
            var display = $" {(int)progress}% ";

            var startIndex = MAX_BLOCK / 2 - display.Length / 2;
            var progressBar = string.Concat(Enumerable.Repeat("█", (int)(progress / 100 * MAX_BLOCK)))
                .PadRight(MAX_BLOCK).Remove(startIndex, display.Length).Insert(startIndex, display);

            return $"**`{progressBar}`**";
        }

        private string GetActivePlayersString(int activePlayers)
        {
            const int MAX_BLOCK = 23;
            var display = $" {activePlayers} ";

            var startIndex = MAX_BLOCK / 2 - display.Length / 2;
            var activePlayersString = string.Concat(Enumerable.Repeat(" ", MAX_BLOCK))
                .Remove(startIndex, display.Length).Insert(startIndex, display);

            return $"**`{activePlayersString}`**";
        }

        private async Task<(int, int, int)> GetGameServerDashBoardDetails()
        {
            if (Application.Current != null)
            {
                return await Application.Current.Dispatcher.Invoke(async () =>
                {
                    var WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    return (WindowsGSM.GetServerCount(), WindowsGSM.GetStartedServerCount(),
                        WindowsGSM.GetActivePlayers());
                });
            }

            return (0, 0, 0);
        }

        private async Task<EmbedBuilder> GetMessageEmbed(SystemMetrics system)
        {
            var embed = new EmbedBuilder
            {
                Title = ":small_orange_diamond: System Metrics",
                Description = $"Server name: {Environment.MachineName}",
                Color = Color.Blue
            };

            embed.AddField("CPU", GetProgressBar(await Task.Run(() => system.GetCPUUsage())), true);
            var ramUsage = await Task.Run(() => system.GetRAMUsage());
            embed.AddField("Memory: " + SystemMetrics.GetMemoryRatioString(ramUsage, system.RAMTotalSize),
                GetProgressBar(ramUsage), true);
            var diskUsage = await Task.Run(() => system.GetDiskUsage());
            embed.AddField("Disk: " + SystemMetrics.GetDiskRatioString(diskUsage, system.DiskTotalSize),
                GetProgressBar(diskUsage), true);

            var (serverCount, startedCount, activePlayers) = await GetGameServerDashBoardDetails();
            embed.AddField($"Servers: {serverCount}/{MainWindow.MAX_SERVER}",
                GetProgressBar(serverCount * 100 / MainWindow.MAX_SERVER), true);
            embed.AddField($"Online: {startedCount}/{serverCount}",
                GetProgressBar((serverCount == 0) ? 0 : startedCount * 100 / serverCount), true);
            embed.AddField("Active Players", GetActivePlayersString(activePlayers), true);

            embed.WithFooter(new EmbedFooterBuilder()
                .WithIconUrl("https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png")
                .WithText($"WindowsGSM {MainWindow.WGSM_VERSION} | System Metrics"));
            embed.WithCurrentTimestamp();

            return embed;
        }
    }
}