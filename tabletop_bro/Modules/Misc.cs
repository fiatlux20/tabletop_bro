using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text;
using System.Xml;

using Discord;
using Discord.Commands;

using Newtonsoft.Json;


namespace tabletop_bro.Modules {
    public class Misc : ModuleBase<SocketCommandContext> {
        private const string resources_folder = "Resources";
        private const string help_file = "help.json";
        private const string char_folder = "CharSheets";

        private static Dictionary<string, string> dict_help;

        [Command("help")]
        public async Task Help(string message = "help")
        {
            message = message.ToLower();
            var embed = new EmbedBuilder();
            string json = "";
            dynamic data;

            if (File.Exists(resources_folder + "/" + help_file)) {
                json = File.ReadAllText(resources_folder + "/" + help_file);
                data = JsonConvert.DeserializeObject<dynamic>(json);
                dict_help = data.ToObject<Dictionary<string, string>>();
            }

            if (dict_help.ContainsKey(message)) {
                embed.WithTitle(":scroll: Help topic: g!" + message);
                embed.WithDescription(dict_help[message]);

                await Context.Channel.SendMessageAsync("", false, embed.Build());
            } else {
                BotUtils.EmbedErrorMsg(ref embed, "t_help", "help_not_found");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
            }
        }

        [Command("echo")]
        public async Task Echo()
        {
            var attachments = Context.Message.Attachments;
            var embed = new EmbedBuilder();

            Console.WriteLine(attachments.ToString());

            embed.WithTitle("Echo");
            embed.WithImageUrl(attachments.ElementAt(0).Url);
            await Context.Channel.SendMessageAsync("", false, embed.Build());
        }

        [Command("upload")]
        public async Task Upload(string name = null)
        {
            ulong user_id = Context.Message.Author.Id;
            string username = Context.Message.Author.Username;
            var attachment = Context.Message.Attachments;
            var char_sheet = new CharSheet();
            var embed = new EmbedBuilder();

            if (name == null) {
                BotUtils.EmbedErrorMsg(ref embed, "t_upload", "no_filename");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
                return;
            }

            if (!Directory.Exists(char_folder))
                Directory.CreateDirectory(char_folder);

            if (!File.Exists(char_folder + "/" + username + ".json")) {
                char_sheet.username = username;
                char_sheet.user_id = user_id;

                string json = JsonConvert.SerializeObject(char_sheet, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(char_folder + "/" + username + ".json", json);
            }
        }
    }

    public struct CharSheet {
        public string username;
        public ulong user_id;
    }
}
