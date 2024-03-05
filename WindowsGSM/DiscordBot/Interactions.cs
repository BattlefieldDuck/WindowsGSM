using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace WindowsGSM.DiscordBot
{
    public class Interactions
    {
        private readonly DiscordSocketClient _client;
        private InteractionService _interactionService;
        private readonly IServiceProvider _serviceProvider;

        public Interactions(DiscordSocketClient client, IServiceProvider serviceProvider)
        {
            _client = client;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeInteractions()
        {
            _interactionService = _serviceProvider.GetRequiredService<InteractionService>();
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
            await _interactionService.RegisterCommandsGloballyAsync();

            _client.InteractionCreated += HandleInteraction;
            _interactionService.SlashCommandExecuted += SlashCommandExecuted;
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            try
            {
                await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Error executing command: {ex.Message}");
                await interaction.RespondAsync("Failed to execute command");
                if(interaction.Type == InteractionType.ApplicationCommand)
                {
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
                }
            }
        }

        private static Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
        {
            if (!arg3.IsSuccess)
            {
                arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true);
            }
            return Task.CompletedTask;
        }
    }
}