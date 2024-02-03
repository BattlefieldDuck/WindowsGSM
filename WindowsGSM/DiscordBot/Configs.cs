using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using WindowsGSM.Functions;

namespace WindowsGSM.DiscordBot
{
    static class Configs
    {
		private static readonly string _botPath = ServerPath.Get(ServerPath.FolderName.Configs, "discordbot");

		public static void CreateConfigs()
		{
			Directory.CreateDirectory(_botPath);
		}

		public static string GetCommandsList()
		{
			string prefix = GetBotPrefix();
			return $"{prefix}wgsm check\n{prefix}wgsm list\n{prefix}wgsm start <SERVERID>\n{prefix}wgsm stop <SERVERID>\n{prefix}wgsm restart <SERVERID>\n{prefix}wgsm update <SERVERID>\n{prefix}wgsm send <SERVERID> <COMMAND>\n{prefix}wgsm backup <SERVERID>\n{prefix}wgsm stats";
		}

		public static string GetBotPrefix()
		{
			try
			{
				return File.ReadAllText(Path.Combine(_botPath, "prefix.txt")).Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void SetBotPrefix(string prefix)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "prefix.txt"), prefix);
		}

		public static string GetBotToken()
		{
			try
			{
				return File.ReadAllText(Path.Combine(_botPath, "token.txt")).Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void SetBotToken(string token)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "token.txt"), token.Trim());
		}

		public static string GetDashboardChannel()
		{
			try
			{
				return File.ReadAllText(Path.Combine(_botPath, "channel.txt")).Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		public static void SetDashboardChannel(string channel)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "channel.txt"), channel.Trim());
		}

		public static int GetDashboardRefreshRate()
		{
			try
			{
				return int.Parse(File.ReadAllText(Path.Combine(_botPath, "refreshrate.txt")).Trim());
			}
			catch
			{
				return 5;
			}
		}

		public static void SetDashboardRefreshRate(int rate)
		{
			Directory.CreateDirectory(_botPath);
			File.WriteAllText(Path.Combine(_botPath, "refreshrate.txt"), rate.ToString());
		}

		public static List<string> GetBotAdminIds()
		{
			try
			{
				var adminIds = new List<string>();
				var lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));
				foreach (var line in lines)
				{
					string[] items = line.Split(new char[] { ' ' });
					adminIds.Add(items[0]);
				}
				return adminIds;
			}
			catch
			{
				return new List<string>();
			}
		}

		public static List<string> GetServerIdsByAdminId(string adminId)
		{
			try
			{
				var lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));

                foreach (var line in lines)
				{
					string[] items = line.Split(new[] { ' ' });

                    if (items[0] == adminId)
					{
						return items[2].Trim().Split(',').Select(s => s.Trim()).ToList();
					}
				}

				return new List<string>();
			}
			catch
			{
				return new List<string>();
			}
		}

		public static List<(string, string, string)> GetBotAdminList()
		{
			try
			{
				var adminList = new List<(string, string, string)>();
				var lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));
				foreach (var line in lines)
				{
					string[] items = line.Split(new[] { ' ' });
                    adminList.Add((items[0], items.Length == 1 ? string.Empty : items[1], items.Length == 2 ? string.Empty : items[2]));
                }

                return adminList;
			}
			catch
			{
				return new List<(string, string, string)>();
			}
		}

		public static void SetBotAdminList(List<(string, string, string)> adminList)
		{
			Directory.CreateDirectory(_botPath);

			List<string> lines = new List<string>();
			foreach ((string adminID, string adminName, string serverIDs) in adminList)
			{
				lines.Add($"{adminID} {adminName} {serverIDs}");
			}
			File.WriteAllText(Path.Combine(_botPath, "adminIDs.txt"), string.Join("\n", lines.ToArray()));
		}

		public static async void FixOldDiscordAdminList()
		{
			if (File.Exists(Path.Combine(_botPath, "adminIDs.txt")))
			{
                var lines = File.ReadAllLines(Path.Combine(_botPath, "adminIDs.txt"));
                var adminList = new List<(string, string, string)>();

                foreach (var line in lines)
                {
                    string[] items = line.Split(new[] { ' ' });
                    string discordName = string.Empty;
                    if (GetBotToken() != string.Empty)
                    {
                        discordName = await GetDiscordUserName(items[0]);
                    }

                    if (items.Length == 2)
                    {
                        adminList.Add((items[0], discordName, items.Length == 1 ? string.Empty : items[1]));
                    }
					else
					{
                        if (items[1] != string.Empty)
						{
                            discordName = items[1];
                        }

                        adminList.Add((items[0], discordName, items[2]));
					}
                }

                SetBotAdminList(adminList);
            }
        }

        public static async Task<string> GetDiscordUserName(string discordID)
        {
            try
            {
                string token = DiscordBot.Configs.GetBotToken();

                if (token == string.Empty) return string.Empty;

                string apiUrl = $"https://discord.com/api/v10/users/{discordID}";

                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bot {token}");
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "WGSMDiscordBot");

                    HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        Trace.WriteLine("RESULT: " + jsonResult);
                        int startIndex = jsonResult.IndexOf("\"global_name\":\"") + 15;
                        int endIndex = jsonResult.IndexOf("\"", startIndex);
                        string globalName = jsonResult.Substring(startIndex, endIndex - startIndex);
                        globalName = Regex.Replace(globalName, @"\\u[0-9A-Fa-f]{4}", string.Empty);
                        return globalName;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to retrieve Discord user information. Status code: {response.StatusCode}", "Discord Bot Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error: {ex.Message}");
            }

            return string.Empty;
        }
    }
}
