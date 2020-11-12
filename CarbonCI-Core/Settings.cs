using Config.Net;
using Octokit;

namespace CarbonCI
{
    public static class Settings
    {
        public static string LogHolder = "";
        //Global persistent settings that are saved to a json file. Limited Types
        public static IPersistentSettings PSettings = new ConfigurationBuilder<IPersistentSettings>().UseJsonFile("settings.json").Build();
    }


    public interface IPersistentSettings
    {
        //Types allowed: bool, double, int, long, string, TimeSpan, DateTime, Uri, Guid
        //DateTime is always converted to UTC
        [Option(Alias = "Github.Username", DefaultValue = "")]
        string GithubUsername { get; }
        
        [Option(Alias = "Github.Password", DefaultValue = "")]
        string GithubPassword { get; }
        
        [Option(Alias = "DiscordToken", DefaultValue = "")]
        string DiscordToken { get; }
        
        
        [Option(Alias = "Polly.Voice", DefaultValue = "justin")]
        string PollyVoice { get; set; }


        [Option(Alias = "Polly.AccessKey", DefaultValue = "")]
        string PollyAccessKey { get; set; }
        
        [Option(Alias = "Polly.SecretKey", DefaultValue = "")]
        string PollySecretKey { get; set; }
        
        [Option(Alias = "LavaLink", DefaultValue = "")]
        string lavalinkConnectionString { get; }
        
        [Option(Alias = "LavalinkPassword", DefaultValue = "")]
        string lavalinkPassword { get; }
    }
}