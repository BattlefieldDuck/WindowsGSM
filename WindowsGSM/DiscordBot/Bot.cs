using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace WindowsGSM.DiscordBot
{
	class Bot
	{
		private DiscordSocketClient _client;
		private string _donorType;
		private SocketTextChannel _dashboardTextChannel;
		private RestUserMessage _dashboardMessage;
		private CancellationTokenSource _cancellationTokenSource;
		private readonly IServiceProvider _serviceProvider = CreateServices();
		private Interactions _interactions;

		public Bot()
		{
			Configs.CreateConfigs();
		}

		public async Task<bool> Start()
		{
			_client = _serviceProvider.GetRequiredService<DiscordSocketClient>();
			_client.Ready += On_Bot_Ready;

			try
			{
				await _client.LoginAsync(TokenType.Bot, Configs.GetBotToken());
				await _client.StartAsync();
			}
			catch
			{
				return false;
			}

			// Listen Commands
			new Commands(_client);
			_interactions = new Interactions(_client, _serviceProvider);

			return true;
		}

		private async Task On_Bot_Ready()
		{
			try
			{
				Stream stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Images/WindowsGSM{(string.IsNullOrWhiteSpace(_donorType) ? string.Empty : $"-{_donorType}")}.png")).Stream;
				await _client.CurrentUser.ModifyAsync(x =>
				{
					x.Username = "WindowsGSM";
					x.Avatar = new Image(stream);
				});
			}
			catch
			{
				// ignore
			}

			await _interactions.InitializeInteractions();
			
			List<Task> tasks = new List<Task>
			{
				StartDiscordPresenceUpdate(),
			};

			_cancellationTokenSource = new CancellationTokenSource();

			await Task.Run(() =>
			{
				try
				{
					Task.WaitAny(tasks.ToArray(), _cancellationTokenSource.Token);
				}
				catch (AggregateException e)
				{
					System.Diagnostics.Debug.WriteLine($"{e.Message}");
				}
			});
		}

		private async Task StartDiscordPresenceUpdate()
		{
			while (_client != null && _client.CurrentUser != null)
			{
				if (Application.Current != null)
				{
					await Application.Current.Dispatcher.Invoke(async () =>
					{
						MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
						int serverCount = WindowsGSM.ServerGrid.Items.Count;
						await _client.SetGameAsync($"{serverCount} game server{(serverCount > 1 ? "s" : string.Empty)}");
					});
				}

				await Task.Delay(900000);
			}
		}


		public void SetDonorType(string donorType)
		{
			_donorType = donorType;
		}

		public async Task Stop()
		{
			if (_client != null)
			{
				try
				{
					_cancellationTokenSource?.Cancel();
				}
				catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"{e.Message}");
                }

				await _client.StopAsync();

				// Delete the message after the bot stop
				try
				{
					if (_dashboardTextChannel != null)
					{
                        await _dashboardTextChannel.DeleteMessageAsync(_dashboardMessage);
                        _dashboardMessage = null;
                    }
				}
				catch
				{
					// ignore
				}
			}
		}

		public string GetInviteLink()
		{
			return (_client == null || _client.CurrentUser == null) ? string.Empty : $"https://discordapp.com/api/oauth2/authorize?client_id={_client.CurrentUser.Id}&permissions=67497024&scope=bot%20applications.commands";
		}
		
		static IServiceProvider CreateServices()
		{
			var config = new DiscordSocketConfig()
			{
				GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages |
				                 GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessageTyping |
				                 GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions |
				                 GatewayIntents.DirectMessageTyping | GatewayIntents.MessageContent,
				// Prevent snowflake date from being used in the interaction service
				UseInteractionSnowflakeDate = false
			};

			return new ServiceCollection()
				.AddSingleton(new DiscordSocketClient(config))
				.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
				.BuildServiceProvider();
		}
	}
}
