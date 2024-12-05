/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: O2SessionProviderEntry.cs 
*
* O2SessionProviderEntry.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Text.Json.Serialization;

using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Configuration;


namespace VNLib.Plugins.Sessions.OAuth
{
    sealed class OAuth2SessionConfig : IOnConfigValidation
    {
        [JsonPropertyName("max_tokens_per_app")]
        public int MaxTokensPerApp { get; set; } = 10;

        [JsonPropertyName("access_token_size")]
        public int AccessTokenSize { get; set; } = 64;

        [JsonPropertyName("token_valid_for_sec")]
        public int TokenLifeTimeSeconds { get; set; } = 3600;

        [JsonPropertyName("cache_prefix")]
        public string CachePrefix { get; set; } = "oauth2";

        [JsonPropertyName("access_token_type")]
        public string TokenType { get; set; } = "Bearer";

        public void OnValidate()
        {
            Validate.Range(MaxTokensPerApp, 1, int.MaxValue);
            Validate.Range(AccessTokenSize, 16, int.MaxValue);
            Validate.Range(TokenLifeTimeSeconds, 1, int.MaxValue);
            Validate.NotNull(CachePrefix, "You must specify a cache prefix");
            Validate.NotNull(TokenType, "You must specify an access token type");
        }
    }
}