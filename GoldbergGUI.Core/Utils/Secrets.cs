namespace GoldbergGUI.Core.Utils
{
    public class Secrets : ISecrets
    {
        public string SteamWebApiKey()
        {
            return System.Environment.GetEnvironmentVariable("GOLDBERG_STEAM_WEB_API_KEY") ?? string.Empty;
        }
    }
}
