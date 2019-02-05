using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace tabletop_bro {
    class Config {
        private const string resources_folder = "Resources";
        private const string config_file = "config.json";

        public static BotConfig bot;

        static Config()
        {
            if (!Directory.Exists(resources_folder))
                Directory.CreateDirectory(resources_folder);

            if (!File.Exists(resources_folder + "/" + config_file)) {
                bot = new BotConfig();
                string json = JsonConvert.SerializeObject(bot, Formatting.Indented);
                File.WriteAllText(resources_folder + "/" + config_file, json);
            } else {
                string json = File.ReadAllText(resources_folder + "/" + config_file);
                bot = JsonConvert.DeserializeObject<BotConfig>(json);
            }
        }
    }

    public struct BotConfig {
        public string token;
        public string cmd_prefix;
    }
}
