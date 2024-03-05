using Discord;
using Discord.Interactions;
using System;
using System.Threading.Tasks;

namespace WindowsGSM.DiscordBot.Preconditions
{
    public class RequireAdminAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            var adminIds = Configs.GetBotAdminIds();
            if (!adminIds.Contains(context.User.Id.ToString()))
            {
                return PreconditionResult.FromError("You don't have permission to use this command.");
            }

            return PreconditionResult.FromSuccess();
        }
    }
}