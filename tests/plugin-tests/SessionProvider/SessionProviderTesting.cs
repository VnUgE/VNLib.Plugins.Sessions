using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Collections.Generic;

using VNLib.Plugins;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Runtime;
using VNLib.Plugins.Essentials.ServiceStack.Testing;
using VNLib.Plugins.Sessions.Tests.Config;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Sessions.Tests.SessionProvider
{
    [TestClass()]
    public class SessionProviderTesting
    {
        [TestMethod()]
        public void LoadSessionProvider()
        {
            new TestPluginLoader<SessionProviderEntry>()
                .WithCliArgs(["--verbose"])  //Enable verbose logging
                .WithLocalHostConfig()
                .WithLocalPluignConfig("Essentials.Sessions.json")
                .Load()
                .GetServices(services =>
                {
                    Assert.IsTrue(services.HasService<ISessionProvider>());
                    Assert.IsTrue(services.HasService<IVirtualEndpointDefinition>());
                    Assert.IsTrue(services.HasService<IEnumerable<IHttpMiddleware>>());
                    Assert.AreEqual(3, services.Count);

                    //Oauth token and revoke endpoints should be loaded
                    Assert.AreEqual(2, services.GetEndpoints().Length);

                    //Session security middleware is enabled
                    Assert.AreEqual(1, services.GetService<IEnumerable<IHttpMiddleware>>().Count());
                })
                .Unload(delayMilliseconds: 5000)
                .TryDispose();
        }
    }
}
