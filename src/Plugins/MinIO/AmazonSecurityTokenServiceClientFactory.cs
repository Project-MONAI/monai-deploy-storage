/*
 * Copyright 2021-2024 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Text;
using Amazon;
using Amazon.SecurityToken;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.MinIO
{
    public class AmazonSecurityTokenServiceClientFactory : IAmazonSecurityTokenServiceClientFactory
    {
        private AmazonSecurityTokenServiceClient? _client;

        private StorageServiceConfiguration Options { get; }

        public AmazonSecurityTokenServiceClientFactory(IOptions<StorageServiceConfiguration> options)
        {
            Guard.Against.Null(options, nameof(options));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            Options = configuration;
        }

        public AmazonSecurityTokenServiceClient GetClient()
        {
            if (_client is not null)
            {
                return _client;
            }

            _client = CreateClient();
            return _client;
        }

        private AmazonSecurityTokenServiceClient CreateClient()
        {
            var endpoint = Options.Settings[ConfigurationKeys.EndPoint];
            var accessKey = Options.Settings[ConfigurationKeys.AccessKey];
            var accessToken = Options.Settings[ConfigurationKeys.AccessToken];
            var securedConnection = Options.Settings[ConfigurationKeys.SecuredConnection];

            var sb = new StringBuilder();
            if (bool.Parse(securedConnection))
            {
                sb.Append("https://");
            }
            else
            {
                sb.Append("http://");
            }
            sb.Append(endpoint);

            var config = new AmazonSecurityTokenServiceConfig
            {
                AuthenticationRegion = RegionEndpoint.EUWest2.SystemName,
                ServiceURL = sb.ToString(),
            };

            return new AmazonSecurityTokenServiceClient(accessKey, accessToken, config);
        }

        private void ValidateConfiguration(StorageServiceConfiguration configuration)
        {
            Guard.Against.Null(configuration, nameof(configuration));

            foreach (var key in ConfigurationKeys.RequiredKeys)
            {
                if (!configuration.Settings.ContainsKey(key))
                {
                    throw new ConfigurationException($"{nameof(MinIoClientFactory)} is missing configuration for {key}.");
                }
            }
        }
    }
}
