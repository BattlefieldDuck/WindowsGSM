﻿using WindowsGSM.GameServers.Configs;
using WindowsGSM.GameServers.Engines;

namespace WindowsGSM.GameServers
{
    public class CSGO : SourceEngine
    {
        public override string Name => "Counter-Strike: Global Offensive";

        public override string ImageSource => $"/images/games/{nameof(CSGO).ToLower()}.jpg";

        public override IConfig Config { get; set; } = new Configuration()
        {
            ClassName = nameof(CSGO),
            Start =
            {
                StartParameter = "-console -game csgo -ip 0.0.0.0 -port 27015 -maxplayers 24 -usercon +game_type 0 +game_mode 0 +mapgroup mg_active +map de_dust2 -nohltv",
            },
            SteamCMD =
            {
                Game = "csgo",
                AppId = "730",
                ServerAppId = "740",
                Username = "anonymous",
                CreateParameter = "+app_update 740 validate",
                UpdateParameter = "+app_update 740",
            },
        };
    }
}
