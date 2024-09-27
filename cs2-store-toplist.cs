using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Dapper;
using MySqlConnector;
using StoreApi;
using System.Text.Json.Serialization;
using Menu;
using Menu.Enums;

namespace Store_TopList
{
    public class Store_TopListConfig : BasePluginConfig
    {
        [JsonPropertyName("top_players_limit")]
        public int TopPlayersLimit { get; set; } = 10;

        [JsonPropertyName("TopMenuType")]
        public int TopMenuType { get; set; } = 0;

        [JsonPropertyName("KitsuneMenuDeveloperDisplay")]
        public bool KitsuneMenuDeveloperDisplay { get; set; } = true;

        [JsonPropertyName("commands")]
        public List<string> Commands { get; set; } = [];
    }

    public class Store_TopList : BasePlugin, IPluginConfig<Store_TopListConfig>
    {
        public override string ModuleName { get; } = "Store Module [TopList]";
        public override string ModuleVersion { get; } = "0.0.3";
        public override string ModuleAuthor => "Nathy";

        private IStoreApi? storeApi;

        public Store_TopListConfig Config { get; set; } = null!;
        
        public KitsuneMenu Menu { get; private set; } = null!;
    
        private void Menu_OnLoad()
        {
            Menu = new KitsuneMenu(this);
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            storeApi = IStoreApi.Capability.Get();

            if (storeApi == null)
            {
                return;
            }

            CreateCommands();
            Menu_OnLoad();
        }

        public void OnConfigParsed(Store_TopListConfig config)
        {
            Config = config;
        }

        private void CreateCommands()
        {
            foreach (string cmd in Config.Commands)
            {
                AddCommand($"css_{cmd}", "Shows top list by credits", OnCommand);
            }
        }

        public void OnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) return;

            if (storeApi == null) throw new Exception("StoreApi could not be located.");

            if (Config.TopMenuType == 0)
            {
                ShowTopCreditsChatMenu(player);
            }
            else if (Config.TopMenuType == 1)
            {
                ShowTopCreditsKitsuneMenu(player);
            }
        }

        private List<(string playerName, int credits)> FetchTopCredits(int limit)
        {
            var referrals = new List<(string playerName, int credits)>();

            using (var connection = new MySqlConnection(GetDatabaseString()))
            {
                connection.Open();

                string query = $@"
                    SELECT PlayerName, Credits
                    FROM store_players
                    ORDER BY Credits DESC
                    LIMIT {Config.TopPlayersLimit};";

                using (var command = new MySqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string playerName = reader.GetString("PlayerName");
                        int credits = reader.GetInt32("Credits");
                        referrals.Add((playerName, credits));
                    }
                }
            }

            return referrals;
        }

        private void ShowTopCreditsChatMenu(CCSPlayerController player)
        {
            var topPlayers = FetchTopCredits(Config.TopPlayersLimit);

            if (topPlayers.Count > 0)
            {
                player.PrintToChat(Localizer["topcredits.title", Config.TopPlayersLimit]);
                int rank = 1;

                foreach (var (playerName, credits) in topPlayers)
                {
                    string message = Localizer["topcredits.players", rank, playerName, credits];
                    player.PrintToChat(message);
                    rank++;
                }
            }
            else
            {
                player.PrintToChat(Localizer["Prefix"] + Localizer["No data available"]);
            }
        }

        private void ShowTopCreditsKitsuneMenu(CCSPlayerController player)
        {
            if (Menu == null)
            {
                return;
            }

            string title = Localizer["topcredits.title", Config.TopPlayersLimit];
            List<MenuItem> items = new List<MenuItem>();
            var topDictionary = new Dictionary<int, (string playerName, int credits)>();

            var topPlayers = FetchTopCredits(Config.TopPlayersLimit);

            int rank = 1;
            foreach (var (playerName, credits) in topPlayers)
            {
                string message = Localizer["topcredits.players", rank, playerName, credits];
                items.Add(new MenuItem(MenuItemType.Text, new MenuValue(message)));
                topDictionary[rank] = (playerName, credits);
                rank++;
            }

            if (items.Count == 0)
            {
                player.PrintToChat(Localizer["Prefix"] + Localizer["No data available"]);
                return;
            }

            Menu?.ShowScrollableMenu(player, title, items, (buttons, menu, selected) =>
            {
            }, false, freezePlayer: true, disableDeveloper: !Config.KitsuneMenuDeveloperDisplay);
        }

        public string GetDatabaseString()
        {
            if (storeApi == null)
            {
                throw new InvalidOperationException("Store API is not available.");
            }

            return storeApi.GetDatabaseString();
        }
    }

    public class TopPlayer
    {
        public string PlayerName { get; set; } = string.Empty;
        public int Credits { get; set; }
    }
}