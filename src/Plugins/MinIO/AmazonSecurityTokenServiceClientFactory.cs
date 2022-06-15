// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

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
