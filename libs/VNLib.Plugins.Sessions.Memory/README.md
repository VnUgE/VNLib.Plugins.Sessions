# VNLib.Plugins.Sessions.Memory
*A memory-backed, self-contained, session provider, that may be loaded directly as a plugin or supports dynamic loading with multiple providers*

#### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

## Notes
When dynamically loading as a sesstion provider, since this plugin provides Web based sessions, it should be a lower priority than an OAuth2 session provider, because it attempts to attach sessions to all applicable connections, potentially bypassing an OAuth2 session.

## License
Source files in for this project are licensed to you under the GNU Affero General Public License (or any later version). See the LICENSE files for more information.