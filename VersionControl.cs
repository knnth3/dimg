using YamlDotNet.Serialization.NamingConventions;

namespace dimg
{
    static internal class VersionControl
    {
        class VersionConfig
        {
            public Dictionary<string, string> Versions { get; set; }
        }

        static readonly string m_ConfigPath = "dimg-versions.yaml";
        static internal string GetNextVersion(string imgName, string previousVersion)
        {
            string nextVersion;
            var config = GetConfigFile();
            if (config.Versions.TryGetValue(imgName, out string version))
            {
                nextVersion = string.IsNullOrEmpty(previousVersion) ? UpgradeVersion(version) : previousVersion;
                config.Versions[imgName] = nextVersion;
            }
            else
            {
                nextVersion = UpgradeVersion();
                config.Versions.Add(imgName, nextVersion);
            }

            SaveConfig(config);
            return nextVersion;
        }

        private static string UpgradeVersion(string previousVersion = null)
        {
            if (string.IsNullOrEmpty(previousVersion))
            {
                return "0.0.1";
            }

            int?[] versions = previousVersion
                .Split('.')
                .Select(x =>
                {
                    int? result = null;
                    if (int.TryParse(x, out int value))
                    {
                        result = value;
                    }

                    return result;
                })
                .Where(x => x.HasValue)
                .ToArray();

            if (versions.Length != 3)
            {
                Console.WriteLine("Invalid version detected! Reseting to '0.0.1'.");
                versions = new int?[] { 0, 0, 1 };
            }

            return $"{versions[0].Value}.{versions[1].Value}.{versions[2].Value + 1}";
        }

        private static VersionConfig GetConfigFile()
        {
            string text = string.Empty;
            if (File.Exists(m_ConfigPath))
            {
                text = File.ReadAllText(m_ConfigPath);
            }

            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var instance = deserializer.Deserialize<VersionConfig>(text);
            if (instance == null)
            {
                instance = new VersionConfig();
            }

            if (instance.Versions == null)
            {
                instance.Versions = new Dictionary<string, string>();
            }

            return instance;
        }

        private static void SaveConfig(VersionConfig config)
        {
            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            string text = serializer.Serialize(config);
            File.WriteAllText(m_ConfigPath, text);
        }
    }
}
