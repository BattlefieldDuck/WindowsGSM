using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using WindowsGSM.DiscordBot.Preconditions;
using System.Windows;

namespace WindowsGSM.DiscordBot.Modules
{
    [RequireAdmin]
    public class AutocompleteCommandsModule : InteractionModuleBase<SocketInteractionContext>
    {
        [AutocompleteCommand("server", "start")]
        public async Task StartAutocomplete()
        {
            var userInput = (Context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
            await GetAutocompleteServerList(userInput);
        }

        [AutocompleteCommand("server", "stop")]
        public async Task StopAutocomplete()
        {
            var userInput = (Context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
            await GetAutocompleteServerList(userInput);
        }

        [AutocompleteCommand("server", "restart")]
        public async Task RestartAutocomplete()
        {
            var userInput = (Context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
            await GetAutocompleteServerList(userInput);
        }

        [AutocompleteCommand("server", "send")]
        public async Task SendAutocomplete()
        {
            var userInput = (Context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
            await GetAutocompleteServerList(userInput);
        }

        [AutocompleteCommand("server", "update")]
        public async Task UpdateAutocomplete()
        {
            var userInput = (Context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
            await GetAutocompleteServerList(userInput);
        }

        [AutocompleteCommand("server", "backup")]
        public async Task BackupAutocomplete()
        {
            var userInput = (Context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
            await GetAutocompleteServerList(userInput);
        }

        // Retrieve user's server list for autocomplete
        private async Task GetAutocompleteServerList(string input)
        {
            var results = new List<AutocompleteResult>();
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                var WindowsGSM = (MainWindow)Application.Current.MainWindow;

                var servers = WindowsGSM.GetServerList(Context.User.Id.ToString());

                foreach (var server in servers)
                {
                    results.Add(new AutocompleteResult($@"{server.Item3} [{server.Item2}]", server.Item1));
                }

                // Only send suggestions that starts with user's input; use case insensitive matching
                results.Where(x => x.Name.StartsWith(input, StringComparison.InvariantCultureIgnoreCase));

                // Max - 25 suggestions at a time
                await (Context.Interaction as SocketAutocompleteInteraction)?.RespondAsync(results.Take(25));
            });
        }
    }
}