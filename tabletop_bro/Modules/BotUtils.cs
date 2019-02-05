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
    class BotUtils {
        private const string resources_folder = "Resources";
        private const string configFile = "config.json";
        private const string errors_file = "errors.json";
        private const string help_file = "help.json";
        private const string gurps_file = "gurps.xml";

        private static Dictionary<string, string> dict_errors;
        public static readonly uint ERROR_RED = 0xE11E1E; // RGB: 225, 30, 30

        public static void EmbedErrorMsg(ref EmbedBuilder embed, string errortitle = "generic", string errortype = "generic_error")
        {
            string title = "";
            string description = "";
            string json = "";
            dynamic data;

            if (File.Exists(resources_folder + "/" + errors_file)) {
                json = File.ReadAllText(resources_folder + "/" + errors_file);
                data = JsonConvert.DeserializeObject<dynamic>(json);
                dict_errors = data.ToObject<Dictionary<string, string>>();
            }

            if (dict_errors.ContainsKey(errortitle) && dict_errors.ContainsKey(errortype)) {
                title = dict_errors[errortitle];
                description = dict_errors[errortype];

                embed.WithTitle(title);
                embed.WithDescription(description);
                embed.WithColor(new Color(ERROR_RED));
            } else {
                // This is hard-coded in case the errors.json is missing and no errors can be reported
                title = ":exclamation: Error message not found";
                description = "I tried to show you an error message, but I couldn't find it!" +
                              "\r\nYou should report the command that caused this.";

                embed.WithTitle(title);
                embed.WithDescription(description);
                embed.WithColor(new Color(ERROR_RED));
            }
        }
    }
}
