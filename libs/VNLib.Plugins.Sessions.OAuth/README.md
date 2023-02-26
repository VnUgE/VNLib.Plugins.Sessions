# VNLib.Plugins.Sessions.OAuth
*Provides OAuth2 sessions and required endpoints for authentication via user applications from the VNLib.Plugins.Essentials.Oauth library*

#### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

## Notes
When dynamically loading this session provider as an asset in the SessionProvider plugin, make sure to order this assembly ahead of other web-based providers if loading multiple providers. OAuth2 sessions are only valid if the connection contains a Bearer token (valid or not it "pre-accepted""), otherwise no session will be attached.

This plugin may be loaded as a standalone authentication server, by loading it directly as an IPlugin. Sessions are created in the backing stores when a token is generated, and therefore accessable via shared backing stores.

## License
Source files in for this project are licensed to you under the GNU Affero General Public License (or any later version). See the LICENSE files for more information.