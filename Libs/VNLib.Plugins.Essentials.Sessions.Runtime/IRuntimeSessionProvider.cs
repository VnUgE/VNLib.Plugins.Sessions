
using VNLib.Net.Http;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// Represents a dynamically loadable type that an provide sessions to http connections
    /// </summary>
    public interface IRuntimeSessionProvider : ISessionProvider
    {
        /// <summary>
        /// Called immediatly after the plugin is loaded into the appdomain
        /// </summary>
        /// <param name="plugin">The plugin instance that is loading the module</param>
        /// <param name="localizedLog">The localized log provider for the provider</param>
        void Load(PluginBase plugin, ILogProvider localizedLog);

        /// <summary>
        /// Determines if the provider can return a session for the connection
        /// </summary>
        /// <param name="entity">The entity to process</param>
        /// <returns>A value indicating if this provider should be called to load a session for</returns>
        bool CanProcess(IHttpEvent entity);
    }
}
