﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Amazon.Polly;
using Amazon.Polly.Model;
using Castle.Core.Internal;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
 using DSharpPlus.CommandsNext.Entities;
 using DSharpPlus.Entities;
using LiteDB;
using RestSharp;
using Semver;

namespace CarbonCI
{
    public class DiscordCommands : BaseCommandModule
    {
        [Command("voice")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task getVoice(CommandContext ctx)
        {
            var ActualVoice = VoiceId.FindValue(Settings.PSettings.PollyVoice);
            await ctx.RespondAsync("Current voice: " + ActualVoice);
            await ctx.Message.DeleteAsync();
        }

        [Command("voice")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task setVoice(CommandContext ctx, string VoiceName)
        {
            var Voices = await Program.Polly.DescribeVoicesAsync(new DescribeVoicesRequest
            {
                Engine = Engine.Neural,
                IncludeAdditionalLanguageCodes = false,
                LanguageCode = LanguageCode.EnUS
            });
            
            try
            {
                var newVoice = Voices.Voices.First(x=>String.Equals(x.Name, VoiceName, StringComparison.CurrentCultureIgnoreCase)).Name;
                Console.WriteLine(newVoice);
                Settings.PSettings.PollyVoice = VoiceName;
                await ctx.RespondAsync("Voice changed to: " + newVoice);
                await ctx.Message.DeleteAsync();
            }
            catch (Exception e)
            {
                var s = string.Join(", \n",Voices.Voices.Select(x => $"Name: {x.Name}, Gender: {x.Gender}"));
                await ctx.Message.DeleteAsync();
                await ctx.RespondAsync($"Error, could not find name. Choices: ```{s}```");
            }
        }
        [Command("Joke")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task Joke(CommandContext ctx)
        {
            var client = new RestClient("https://icanhazdadjoke.com");
            var request = new RestRequest();
            request.Method = Method.GET;
            request.AddHeader("Accept", "text/plain");
            var response = client.Execute(request);
            var player = Program.audioService.GetPlayer<QueuedLavalinkPlayerV2>(ctx.Guild.Id) 
                         ?? await Program.audioService.JoinAsync<QueuedLavalinkPlayerV2>(ctx.Guild.Id,ctx.Member.VoiceState.Channel.Id);

            var ActualVoice = VoiceId.FindValue(Settings.PSettings.PollyVoice);
            // resolve a track from youtube
            //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
            var SpeechResponse = await Program.Polly.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
            {
                Engine = Engine.Neural,
                LanguageCode = LanguageCode.EnUS,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = "24000",
                TextType = TextType.Text,
                Text = response.Content,
                VoiceId = ActualVoice
            });
                
            var g = Guid.NewGuid();
            string path = $@"C:\temp\{g}.Mp3";
            FileStream f = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
            await SpeechResponse.AudioStream.CopyToAsync(f);
            f.Flush();
            f.Close();
            var track = await Program.audioService.GetTrackAsync(HttpUtility.UrlEncode(path));
            // play track
            await player.PlayAsync(track);
            await ctx.Message.DeleteAsync();
            //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }
        
        [Command("speakdump")]
        [Aliases("s2f")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task speakdump(CommandContext ctx, [RemainingText] string textToSpeak)
        {
            try
            {
                var ActualVoice = VoiceId.FindValue(Settings.PSettings.PollyVoice);
                // resolve a track from youtube
                //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
                foreach (var user in ctx.Message.MentionedUsers)
                {
                    Console.WriteLine(user.Mention.ToString());
                    var DisMem = await ctx.Guild.GetMemberAsync(user.Id);
                    var callout = DisMem.Nickname.IsNullOrEmpty() ? DisMem.DisplayName : DisMem.Nickname;
                    textToSpeak = textToSpeak.Replace(user.Mention.ToString(), callout);
                }
                var SpeechResponse = await Program.Polly.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
                {
                    Engine = Engine.Neural,
                    LanguageCode = LanguageCode.EnUS,
                    OutputFormat = OutputFormat.Mp3,
                    SampleRate = "24000",
                    TextType = TextType.Text,
                    Text = textToSpeak,
                    VoiceId = ActualVoice
                });
                
                var g = Guid.NewGuid();
                string path = $@"{g}.Mp3";
                //FileStream f = new IsolatedStorageFileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
                //await SpeechResponse.AudioStream.CopyToAsync(f);
                await ctx.RespondWithFileAsync(DateTime.UtcNow.ToString("F")+".Mp3", SpeechResponse.AudioStream);
                await ctx.Message.DeleteAsync();
                //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
        [Command("speakSSML")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        
        public async Task speakssml(CommandContext ctx, [RemainingText] string textToSpeak)
        {
            try
            {
                var player = Program.audioService.GetPlayer<QueuedLavalinkPlayerV2>(ctx.Guild.Id)
                             ?? await Program.audioService.JoinAsync<QueuedLavalinkPlayerV2>(ctx.Guild.Id,
                                 ctx.Member.VoiceState.Channel.Id);

                var ActualVoice = VoiceId.FindValue(Settings.PSettings.PollyVoice);
                // resolve a track from youtube
                //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
                foreach (var user in ctx.Message.MentionedUsers)
                {
                    Console.WriteLine(user.Mention.ToString());
                    var DisMem = await ctx.Guild.GetMemberAsync(user.Id);
                    var callout = DisMem.Nickname.IsNullOrEmpty() ? DisMem.DisplayName : DisMem.Nickname;
                    textToSpeak = textToSpeak.Replace(user.Mention.ToString(), callout);
                }

                var SpeechResponse = await Program.Polly.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
                {
                    Engine = Engine.Standard,
                    LanguageCode = LanguageCode.EnUS,
                    OutputFormat = OutputFormat.Mp3,
                    SampleRate = "24000",
                    TextType = TextType.Ssml,
                    Text = textToSpeak,
                    VoiceId = ActualVoice
                });

                var g = Guid.NewGuid();
                string path = $@"C:\temp\{g}.Mp3";
                FileStream f = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
                await SpeechResponse.AudioStream.CopyToAsync(f);
                f.Flush();
                f.Close();
                var track = await Program.audioService.GetTrackAsync(HttpUtility.UrlEncode(path));
                // play track
                await player.PlayAsync(track);
                await ctx.Message.DeleteAsync();
                //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
            }
            catch (Exception e)
            {
                Console.WriteLine(textToSpeak);
            }
            
        }
        
        [Command("speak")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        
        public async Task speak(CommandContext ctx, [RemainingText] string textToSpeak)
        {
            var player = Program.audioService.GetPlayer<QueuedLavalinkPlayerV2>(ctx.Guild.Id) 
                         ?? await Program.audioService.JoinAsync<QueuedLavalinkPlayerV2>(ctx.Guild.Id,ctx.Member.VoiceState.Channel.Id);

            var ActualVoice = VoiceId.FindValue(Settings.PSettings.PollyVoice);
            // resolve a track from youtube
            //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
            foreach (var user in ctx.Message.MentionedUsers)
            {
                Console.WriteLine(user.Mention.ToString());
                var DisMem = await ctx.Guild.GetMemberAsync(user.Id);
                var callout = DisMem.Nickname.IsNullOrEmpty() ? DisMem.DisplayName : DisMem.Nickname;
                textToSpeak = textToSpeak.Replace(user.Mention.ToString(), callout);
            }
            var SpeechResponse = await Program.Polly.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
            {
                Engine = Engine.Neural,
                LanguageCode = LanguageCode.EnUS,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = "24000",
                TextType = TextType.Text,
                Text = textToSpeak,
                VoiceId = ActualVoice
            });
                
            var g = Guid.NewGuid();
            string path = $@"C:\temp\{g}.Mp3";
            FileStream f = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
            await SpeechResponse.AudioStream.CopyToAsync(f);
            f.Flush();
            f.Close();
            var track = await Program.audioService.GetTrackAsync(HttpUtility.UrlEncode(path));
            // play track
            await player.PlayAsync(track);
            await ctx.Message.DeleteAsync();
            //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }
        
        [Command("JokeDump")]
        [Aliases("jd", "j2f")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task JokeDump(CommandContext ctx)
        {
            var client = new RestClient("https://icanhazdadjoke.com");
            var request = new RestRequest();
            request.Method = Method.GET;
            request.AddHeader("Accept", "text/plain");
            var response = client.Execute(request);
            var ActualVoice = VoiceId.FindValue(Settings.PSettings.PollyVoice);
            // resolve a track from youtube
            //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
            var SpeechResponse = await Program.Polly.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
            {
                Engine = Engine.Neural,
                LanguageCode = LanguageCode.EnUS,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = "24000",
                TextType = TextType.Text,
                Text = response.Content,
                VoiceId = ActualVoice
            });
                
            var g = Guid.NewGuid();
            string path = $@"{g}.Mp3";
            //FileStream f = new IsolatedStorageFileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
            //await SpeechResponse.AudioStream.CopyToAsync(f);
            await ctx.RespondWithFileAsync(DateTime.UtcNow.ToString("F")+".Mp3", SpeechResponse.AudioStream);
            await ctx.Message.DeleteAsync();
            //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }
        
        [Command("pog")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task pog(CommandContext ctx, float volume = 0.5f)
        {
            var player = Program.audioService.GetPlayer<QueuedLavalinkPlayerV2>(ctx.Guild.Id) 
                         ?? await Program.audioService.JoinAsync<QueuedLavalinkPlayerV2>(ctx.Guild.Id,ctx.Member.VoiceState.Channel.Id);
            // resolve a track from youtube
            //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
            var track = await Program.audioService.GetTrackAsync(HttpUtility.UrlEncode($@"C:\Discord Memes\PogAudio.mp3"));
            // play track
            await player.SetVolumeAsync(volume);
            await player.PlayAsync(track);
            await ctx.Message.DeleteAsync();
            //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }
        [Command("notpog")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev","CI Access"})]
        public async Task notpog(CommandContext ctx, float volume = 0.5f)
        {
            var player = Program.audioService.GetPlayer<QueuedLavalinkPlayerV2>(ctx.Guild.Id) 
                         ?? await Program.audioService.JoinAsync<QueuedLavalinkPlayerV2>(ctx.Guild.Id,ctx.Member.VoiceState.Channel.Id);
            // resolve a track from youtube
            //var myTrack = await audioService.GetTrackAsync("The noob song", SearchMode.YouTube);
            var track = await Program.audioService.GetTrackAsync(HttpUtility.UrlEncode($@"C:\Discord Memes\!Poggers.mp3"));
            // play track
            await player.SetVolumeAsync(volume);
            await player.PlayAsync(track);
            await ctx.Message.DeleteAsync();
            //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }
        [Command("volume")]
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev"})]
        public async Task volume(CommandContext ctx, float volume)
        {
            var player = Program.audioService.GetPlayer<QueuedLavalinkPlayerV2>(ctx.Guild.Id) 
                         ?? await Program.audioService.JoinAsync<QueuedLavalinkPlayerV2>(ctx.Guild.Id,ctx.Member.VoiceState.Channel.Id);
            await player.SetVolumeAsync(volume / 100, true);
            await ctx.Message.DeleteAsync();
            //await ctx.Message.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":white_check_mark:"));
        }
        
        [GroupAttribute("locks")] // let's mark this class as a command group
        [Description("Meme commands")] // give it a description for help purposes
        [RequireRolesAttribute(RoleCheckMode.Any, new []{"Admin+Dev"})] // and restrict this to users who have appropriate permissions
        public class LockGroup : BaseCommandModule
        {
            [GroupCommand]
            public async Task ExecuteGroupAsync(CommandContext ctx, [Description("Who's locks to lookup")]DiscordMember member)
            {
                await ctx.TriggerTypingAsync();
                var embed = new DiscordEmbedBuilder();
                embed.WithFooter("Powered by CarbonCI®",
                    "https://cdn.discordapp.com/avatars/759076557750140930/26713c78e70e69f063bda9e918b855bb.png?size=128").WithTimestamp(DateTimeOffset.UtcNow)
                    .WithTitle($"Username locks for **{member.DisplayName}**");
                var col = Program.db.GetCollection<LockedDiscordUser>("LockedDiscordUsers");
                
                if(col.Exists(x => x.guildID == ctx.Guild.Id && x.UserId == member.Id))
                {
                    var thing = col.FindOne(x => x.guildID == ctx.Guild.Id && x.UserId == member.Id);
                    embed.AddField("**Locked to**:", $"{thing.nickToLockTo}");
                    embed.Color = DiscordColor.SpringGreen;
                }
                else
                {
                    embed.Description = "No locks for the specified user!";
                    embed.Color = DiscordColor.Red;
                }
                await ctx.RespondAsync(null, false, embed.Build());

            }
            
            [Command("clear"), Description("clears a users locks")]
            public async Task UnlockAdd(CommandContext ctx, [Description("Whose nickname should I unlock")] DiscordMember member)
            {
                await ctx.TriggerTypingAsync();
                var embed = new DiscordEmbedBuilder();
                embed.WithFooter("Powered by CarbonCI®",
                    "https://cdn.discordapp.com/avatars/759076557750140930/26713c78e70e69f063bda9e918b855bb.png?size=128").WithTimestamp(DateTimeOffset.UtcNow);
                var col = Program.db.GetCollection<LockedDiscordUser>("LockedDiscordUsers");
                try
                {
                    var doc = col.FindOne(b => b.UserId == member.Id && b.guildID == ctx.Guild.Id);
                    if (doc is null)
                    {
                        embed.WithTitle($"**Error**");
                        embed.Color = DiscordColor.Red;
                        embed.WithDescription("Doc is null");
                    }
                    else
                    {
                        col.Delete(doc.Id);
                        embed.WithTitle($"**Unlocked {member.Username}!**");
                        embed.Color = DiscordColor.SpringGreen;
                        embed.WithDescription("Successfully unlocked.");
                    }
                    await member.ModifyAsync(x=>x.Nickname="");
                }
                catch (Exception b)
                {
                    embed.WithTitle($"**Error**");
                    embed.Color = DiscordColor.Red;
                    embed.WithDescription(b.Message);
                }

                await ctx.RespondAsync(null, false, embed.Build());
            }

            [Command("add"), Description("adds a user to the locks list")]
            public async Task LockAdd(CommandContext ctx, [Description("Whose nickname should I lock?")] DiscordMember member, [RemainingText, Description("Nickname to set them too.")] string nickname)
            {
                await ctx.TriggerTypingAsync();
                var embed = new DiscordEmbedBuilder();
                embed.WithFooter("Powered by CarbonCI®",
                        "https://cdn.discordapp.com/avatars/759076557750140930/26713c78e70e69f063bda9e918b855bb.png?size=128").WithTimestamp(DateTimeOffset.UtcNow);
                try
                {
                    
                    var col = Program.db.GetCollection<LockedDiscordUser>("LockedDiscordUsers");
                    
                    var h = new LockedDiscordUser {UserId = member.Id, guildID = ctx.Guild.Id, nickToLockTo = nickname};
                    embed.WithColor(DiscordColor.SpringGreen);
                    if (col.Exists(x => x.UserId == member.Id && x.guildID == ctx.Guild.Id))
                    {
                        var a = col.FindOne(x => x.UserId == member.Id && x.guildID == ctx.Guild.Id);
                        a.nickToLockTo = nickname;
                        col.Update(a);
                        embed.WithTitle("**Modified lock**");
                    }
                    else
                    {
                        col.Insert(h);
                        embed.WithTitle("**Locked user nickname**");
                    }

                    embed.AddField("**Locked user:**", member.Username, true);
                    embed.AddField("**Locked to name:**", nickname, true);
                    await member.ModifyAsync(x=>x.Nickname=nickname);
                }
                catch (Exception e)
                {
                    embed.WithTitle("**Error!**");
                    embed.WithColor(DiscordColor.Red);
                    embed.Description = e.Message;
                }

                await ctx.RespondAsync(null, false, embed.Build());

            }
        }
        [Group("build")] // let's mark this class as a command group
        [Description("automated build and deployment commands.")] // give it a description for help purposes
        [RequireRolesAttribute(RoleCheckMode.Any,"Developer")] // and restrict this to users who have appropriate permissions
        public class ExampleGrouppedCommands: BaseCommandModule
        {
            [Command("patch"), Description("Builds a patch")]
            public async Task patch(CommandContext ctx)
            {
                string[] x86ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x32.exe"
                };
                string[] x64ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x64.exe"
                };
                // let's trigger a typing indicator to let
                // users know we're working
                await ctx.TriggerTypingAsync();
                var latest = await Program.getLatestVersion();
                var message = await ctx.RespondAsync($"Building patch {latest.incrementPatch()} from commits...");
                var tt = await Program.runBuildScript(latest.incrementPatch());
                await ctx.TriggerTypingAsync();
                if (tt)
                {
                    Dictionary<String, Stream> fileDict = new Dictionary<string, Stream>();
                    fileDict.Add("AmongUsCapture-x32.exe", File.OpenRead(Path.Combine(x86ApplicationOutput).Replace("file:\\", "")));
                    fileDict.Add("AmongUsCapture-x64.exe", File.OpenRead(Path.Combine(x64ApplicationOutput).Replace("file:\\", "")));
                    await message.DeleteAsync();
                    await ctx.RespondWithFilesAsync(fileDict, $"Build {latest.incrementPatch()} success!");
                }
                else
                {
                    MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(Settings.LogHolder));
                    await ctx.RespondWithFileAsync("log.txt",stream);
                }
            }
            
            [Command("minor"), Description("Builds a minor release")]
            public async Task minor(CommandContext ctx)
            {
                string[] x86ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x32.exe"
                };
                string[] x64ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x64.exe"
                };
                // let's trigger a typing indicator to let
                // users know we're working
                await ctx.TriggerTypingAsync();
                var latest = await Program.getLatestVersion();
                var message = await ctx.RespondAsync($"Building minor {latest.incrementMinor()} from commits...");
                var tt = await Program.runBuildScript(latest.incrementMinor());
                await ctx.TriggerTypingAsync();
                if (tt)
                {
                    Dictionary<String, Stream> fileDict = new Dictionary<string, Stream>();
                    fileDict.Add("AmongUsCapture-x32.exe", File.OpenRead(Path.Combine(x86ApplicationOutput).Replace("file:\\", "")));
                    fileDict.Add("AmongUsCapture-x64.exe", File.OpenRead(Path.Combine(x64ApplicationOutput).Replace("file:\\", "")));
                    await message.DeleteAsync();
                    await ctx.RespondWithFilesAsync(fileDict, $"Build {latest.incrementMinor()} success!");
                }
                else
                {
                    MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(Settings.LogHolder));
                    await message.ModifyAsync($"Build {latest.incrementMinor()} failed uh oh.!");
                    await ctx.RespondWithFileAsync("log.txt",stream);
                }
            }
            
            [Command("major"), Description("Builds a major release")]
            public async Task major(CommandContext ctx)
            {
                string[] x86ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x32.exe"
                };
                string[] x64ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x64.exe"
                };
                // let's trigger a typing indicator to let
                // users know we're working
                await ctx.TriggerTypingAsync();
                var latest = await Program.getLatestVersion();
                var message = await ctx.RespondAsync($"Building major {latest.incrementMajor()} from commits...");
                var tt = await Program.runBuildScript(latest.incrementMajor());
                await ctx.TriggerTypingAsync();
                if (tt)
                {
                    Dictionary<String, Stream> fileDict = new Dictionary<string, Stream>();
                    fileDict.Add("AmongUsCapture-x32.exe", File.OpenRead(Path.Combine(x86ApplicationOutput).Replace("file:\\", "")));
                    fileDict.Add("AmongUsCapture-x64.exe", File.OpenRead(Path.Combine(x64ApplicationOutput).Replace("file:\\", "")));
                    await message.DeleteAsync();
                    await ctx.RespondWithFilesAsync(fileDict, $"Build {latest.incrementMajor()} success!");
                }
                else
                {
                    MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(Settings.LogHolder));
                    await message.ModifyAsync($"Build {latest.incrementMajor()} failed uh oh.!");
                    await ctx.RespondWithFileAsync("log.txt",stream);
                }
            }
            [Command("alpha"), Description("Builds a prerelease")]
            public async Task alpha(CommandContext ctx)
            {
                string[] x86ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x32.exe"
                };
                string[] x64ApplicationOutput =
                {
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                    @"Script\", @"amonguscapture\", @"AmongUsCapture-x64.exe"
                };
                // let's trigger a typing indicator to let
                // users know we're working
                await ctx.TriggerTypingAsync();
                var latest = await Program.getLatestVersion();
                var message = await ctx.RespondAsync($"Building alpha {latest.incrementAlpha()} from commits...");
                var tt = await Program.runBuildScript(latest.incrementAlpha());
                await ctx.TriggerTypingAsync();
                if (tt)
                {
                    Dictionary<String, Stream> fileDict = new Dictionary<string, Stream>();
                    fileDict.Add("AmongUsCapture-x32.exe", File.OpenRead(Path.Combine(x86ApplicationOutput).Replace("file:\\", "")));
                    fileDict.Add("AmongUsCapture-x64.exe", File.OpenRead(Path.Combine(x64ApplicationOutput).Replace("file:\\", "")));
                    await message.DeleteAsync();
                    await ctx.RespondWithFilesAsync(fileDict, $"Build {latest.incrementAlpha()} success!");
                }
                else
                {
                    MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(Settings.LogHolder));
                    await message.ModifyAsync($"Build {latest.incrementAlpha()} failed uh oh.!");
                    await ctx.RespondWithFileAsync("log.txt",stream);
                }
            }
            // all the commands will need to be executed as <prefix>build <command> <arguments>
        }
    }
}