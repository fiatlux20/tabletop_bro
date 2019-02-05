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
    public class Tabletop : ModuleBase<SocketCommandContext> {
        private const string resources_folder = "Resources";
        private const string gurps_file = "gurps.xml";
        private const string char_folder = "CharSheets";

        private static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        private static readonly int ROLLS_MAX = 25;
        private static readonly int SIDES_MAX = 100;
        private static readonly int MODIFIER_MAX = 15;
        private const string VALID_CHARS = "0123456789d+-*";

        // roll command and helper functions
        private static bool RollValidInput(string input)
        {
            bool is_valid = true, modifier = false;
            char modifier_char = '0';
            int d_count = 0, d_index = 0;
            int m_count = 0, m_index = 0;

            if (string.IsNullOrEmpty(input)) {
                is_valid = false;
                return is_valid;
            }
            if (!input.Contains('d') || input.Length < 2) {
                is_valid = false;
                return is_valid;
            }

            foreach (char c in input) {
                if (!(VALID_CHARS.Contains(c))) {
                    is_valid = false;
                    return is_valid;
                }

                if (c == 'd') {
                    d_count++;
                }

                if (c == '+' || c == '-' || c == '*') {
                    modifier_char = c;
                    m_count++;
                    modifier = true;
                }

                if (d_count > 1 || m_count > 1) {
                    is_valid = false;
                    return is_valid;
                }
            }

            d_index = input.IndexOf('d');

            if (d_index == (input.Length - 1)) {
                is_valid = false;
                return is_valid;
            }
            if (modifier) {
                m_index = input.IndexOf(modifier_char);

                if (m_index == (input.Length - 1)) {
                    is_valid = false;
                    return is_valid;
                }
                if (Math.Abs(d_index - m_index) == 1) {
                    is_valid = false;
                    return is_valid;
                }
            }

            return is_valid;
        }
        private static char RollGetModifier(string input)
        {
            char modifier = '0';

            if (input.Contains('+')) modifier = '+';
            else if (input.Contains('-')) modifier = '-';
            else if (input.Contains('*')) modifier = '*';

            return modifier;
        }
        private static bool RollCheckRange(byte rolls, byte sides, byte mod)
        {
            bool is_valid = true;

            if (rolls > ROLLS_MAX || sides > SIDES_MAX || mod > MODIFIER_MAX) {
                is_valid = false;
                return is_valid;
            }
            if (rolls <= 0 || sides <= 0 || mod < -MODIFIER_MAX) {
                is_valid = false;
                return is_valid;
            }

            return is_valid;
        }
        private static byte[] RollArrayDice(byte rolls, byte sides)
        {
            byte[] results = new byte[rolls];
            rng.GetBytes(results);

            for (int i = 0; i < rolls; i++) {
                results[i] %= sides;
                results[i] += 1;
            }

            return results;
        }
        [Command("roll")]
        public async Task Roll(string message = null)
        {
            var embed = new EmbedBuilder();

            if (!RollValidInput(message)) {
                BotUtils.EmbedErrorMsg(ref embed, "t_roll", "roll_input");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
                return;
            }

            bool modifier = true;
            char operation = RollGetModifier(message);
            if (operation == '0') modifier = false;

            byte rolls = 0, sides = 0, mod = 0;

            try // to convert user input to int values
            {
                if (message.IndexOf('d') == 0) rolls = 1;
                else rolls = Convert.ToByte(message.Substring(0, message.IndexOf('d')));

                if (modifier) {
                    sides = Convert.ToByte(message.Substring(message.IndexOf('d') + 1, message.IndexOf(operation) - (message.IndexOf('d') + 1)));
                    mod = Convert.ToByte(message.Substring(message.IndexOf(operation) + 1));
                } else sides = Convert.ToByte(message.Substring(message.IndexOf('d') + 1));
            } catch // fuckie wuckies
              {
                BotUtils.EmbedErrorMsg(ref embed, "t_roll", "roll_parse");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
                throw new ArgumentException("Parsing error in " + message + " with operator type " + operation + "\nGot rolls: " + rolls + " sides: " + sides + " mod_value: " + mod);
            }

            if (!RollCheckRange(rolls, sides, mod)) {
                BotUtils.EmbedErrorMsg(ref embed, "t_roll", "roll_range");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
                return;
            }

            int max = rolls * sides; // Used for determining the color value.
            byte[] results = RollArrayDice((byte)rolls, (byte)sides);
            int result = 0;
            string roll_list = "";
            for (int i = 0; i < results.Length; i++) {
                result += results[i];

                roll_list += results[i];
                if (i < results.Length - 1) roll_list += ", ";
            }

            embed.WithTitle("Rolling " + message + "... :game_die:");

            if (modifier) {
                switch (operation) {
                case '+':
                    result += mod;
                    max += mod;
                    break;
                case '-':
                    result -= mod;
                    max -= mod;
                    break;
                case '*':
                    result *= mod;
                    max *= mod;
                    break;
                default:
                    break;
                }

                embed.WithDescription("Result of the roll was **" + result + "** : [" + roll_list + "]" +
                                          " " + operation + " " + mod);
            } else {
                embed.WithDescription("Result of the roll was **" + result + "** : [" + roll_list + "]");
            }

            embed.WithColor(new Color(255 - (255 / max) * result, (255 / max) * result, 25));
            await Context.Channel.SendMessageAsync("", false, embed.Build());
        }


        private static readonly int CHAR_LIMIT = 1900;

        // gurps category command and helper functions
        private async Task GurpsSearch(string search_request)
        {
            int min_three(int x, int y, int z) { return Math.Min(x, Math.Min(y, z)); }
            int longest_substring(string name, string search)
            {
                int f_count = 0;
                int[,] f_distance = new int[name.Length, search.Length];

                for (int i = 0; i < name.Length; i++) {
                    for (int j = 0; j < search.Length; j++) {
                        if (name[i] == search[j]) {
                            if (i == 0 || j == 0) f_distance[i, j] = 1;
                            else f_distance[i, j] = f_distance[i - 1, j - 1] + 1;

                            if (f_distance[i, j] > f_count) f_count = f_distance[i, j];
                        } else f_distance[i, j] = 0;
                    }
                }

                return f_count;
            }
            int levenshtein_dist(string name, string search)
            {
                int[,] f_distance = new int[name.Length + 1, search.Length + 1];

                if (name.Equals(search)) return 0;
                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(search)) return search.Length;
                if (string.IsNullOrEmpty(search) && !string.IsNullOrEmpty(name)) return name.Length;
                if (string.IsNullOrEmpty(search) && string.IsNullOrEmpty(name)) return -1;

                for (int i = 1; i < name.Length + 1; i++) f_distance[i, 0] = i;
                for (int j = 1; j < search.Length + 1; j++) f_distance[0, j] = j;

                for (int i = 1; i < name.Length + 1; i++) {
                    for (int j = 1; j < search.Length + 1; j++) {
                        if (name[i - 1].Equals(search[j - 1])) f_distance[i, j] = f_distance[i - 1, j - 1];
                        else f_distance[i, j] = min_three(f_distance[i - 1, j] + 1, f_distance[i, j - 1] + 1, f_distance[i - 1, j - 1] + 1);
                    }
                }

                return f_distance[name.Length, search.Length];
            }
            List<string> split_message(string result)
            {
                var f_results = new List<string>();

                for (int i = 0; i <= (result.Length) / CHAR_LIMIT; i++) {
                    f_results.Add(result.Substring(CHAR_LIMIT * i,
                        (CHAR_LIMIT > result.Length - CHAR_LIMIT * i) ? result.Length - (CHAR_LIMIT * i)
                                                                      : CHAR_LIMIT));
                }

                return f_results;
            }

            var xml_doc = new XmlDocument();
            var embed = new EmbedBuilder();

            if (File.Exists(resources_folder + "/" + gurps_file)) {
                try {
                    xml_doc.Load(resources_folder + "/" + gurps_file);
                } catch {
                    BotUtils.EmbedErrorMsg(ref embed, "t_search_no_results", "search_file_open");
                    await Context.Channel.SendMessageAsync("", false, embed.Build());
                    throw new FileLoadException("Failed to load " + gurps_file);
                }
            }

            string name_match = "", search_result = "";
            string search_suggestion = "", substring_suggestion = "";

            int distance = 0, substring = 0, max_substring = 0;
            int min_distance = search_request.Length;
            bool match_found = false;

            Console.WriteLine(search_request);
            // read XML doc
            foreach (XmlNode node in xml_doc.DocumentElement) {
                // go through each advantage. if it matches search then display that, else compute
                // the distance between what was found and the search string
                string name = node.Attributes[0].InnerText;
                if (name.Equals(search_request, StringComparison.OrdinalIgnoreCase)) {
                    match_found = true;
                    name_match = name;
                    foreach (XmlNode child in node.ChildNodes) {
                        search_result += child.InnerText;
                        search_result += Environment.NewLine;
                    }
                    break;
                } else {
                    distance = levenshtein_dist(name, search_request);
                    substring = longest_substring(name, search_request);

                    if (distance < min_distance) {
                        search_suggestion = name;
                        min_distance = distance;
                    }
                    if (substring > max_substring) {
                        substring_suggestion = name;
                        max_substring = substring;
                    }

                    if (distance < 0) {
                        // ERROR
                        break;
                    }
                }
            }

            if (!match_found) {
                if (!string.IsNullOrEmpty(search_suggestion)) {
                    string[] results = new string[2];
                    results[0] = search_suggestion;
                    if (!substring_suggestion.Equals(search_suggestion))
                        results[1] = substring_suggestion;
                    embed.WithTitle(":mag_right: Search results:");
                    embed.WithDescription("I couldn't find the exact advantage or disadvantage." +
                                          "\r\nDid you mean:\r\n**" + results[0] + "\r\n" + results[1] + "**");

                    await Context.Channel.SendMessageAsync("", false, embed.Build());
                    return;
                } else {
                    BotUtils.EmbedErrorMsg(ref embed, "t_search_no_results", "search_not_found");
                    await Context.Channel.SendMessageAsync("", false, embed.Build());
                    return;
                }
            } else {
                if (search_result.Length > CHAR_LIMIT) {
                    Console.WriteLine("Attempting to print string of length " + search_result.Length);
                    int i = 1;
                    var split_results = split_message(search_result);

                    foreach (string str in split_results) {
                        embed.WithTitle(name_match + " (part " + i + " of " + ((search_result.Length / CHAR_LIMIT) + 1) + ")");
                        embed.WithDescription(str);
                        await Context.Channel.SendMessageAsync("", false, embed.Build());
                        i++;
                    }
                } else {
                    embed.WithTitle(name_match);
                    embed.WithDescription(search_result);
                    await Context.Channel.SendMessageAsync("", false, embed.Build());
                }
            }
        }
        private async Task GurpsGet(string request)
        {
            return;
        }
        [Command("gurps")]
        public async Task Gurps([Remainder] string message = null)
        {
            string[] split_args(string input) { return (string.IsNullOrEmpty(input) ? null : input.Split(' ')); }

            string[] arguments = split_args(message);
            string search_request = "";
            var embed = new EmbedBuilder();

            if (arguments == null || arguments.Length < 2) {
                BotUtils.EmbedErrorMsg(ref embed, "gurps", "too_few_args");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
                return;
            }

            switch (arguments[0]) {
            case "search":
                if (arguments.Length > 2)
                    for (int i = 1; i < arguments.Length; i++) {
                        if (i < arguments.Length - 1) search_request += arguments[i] + " ";
                        else search_request += arguments[i];
                    } else
                    search_request = arguments[1];
                await GurpsSearch(search_request);

                break;
            case "get":
                break;
                await GurpsGet(arguments[1]);

            default:
                BotUtils.EmbedErrorMsg(ref embed, "gurps", "bad_args");
                await Context.Channel.SendMessageAsync("", false, embed.Build());
                return;
            }
        }
    }
}