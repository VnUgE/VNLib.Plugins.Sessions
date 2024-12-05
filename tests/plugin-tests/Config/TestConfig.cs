using System;
using System.IO;

using VNLib.Plugins;
using VNLib.Plugins.Essentials.ServiceStack.Testing;

namespace VNLib.Plugins.Sessions.Tests.Config
{
    internal static class TestConfig
    {
        private static string ConfigDirPath => Environment.GetEnvironmentVariable("TEST_CONFIG_DIR")!;

        public static TestPluginLoader<T> WithLocalPluignConfig<T>(this TestPluginLoader<T> pl, string file) where T : class, IPlugin, new()
        {
            string path = Path.Combine(ConfigDirPath, file);
            return pl.WithPluginConfigFile(path);
        }

        public static TestPluginLoader<T> WithLocalHostConfig<T>(this TestPluginLoader<T> pl) where T : class, IPlugin, new()
        {
            string path = Path.Combine(ConfigDirPath, "Test.Plugins.Sessions.Config.json");
            return pl.WithHostConfigFile(path);
        }

        public static TestPluginLoader<T> WithExternalPluginConfig<T>(this TestPluginLoader<T> pl, string file) where T : class, IPlugin, new()
        {
            return pl.WithPluginConfigFile(file);
        }
    }
}
