// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Minio;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.MinIo
{
    public class MinIoClientFactory : IMinIoClientFactory
    {
        public MinIoClientFactory(IOptions<StorageServiceConfiguration> options)
        {
            Guard.Against.Null(options, nameof(options));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            Options = configuration;
        }

        private MinioClient Client { get; set; }

        private StorageServiceConfiguration Options { get; }

        public MinioClient GetClient()
        {
            if (Client is not null)
            {
                return Client;
            }

            var endpoint = Options.Settings[ConfigurationKeys.EndPoint];
            var accessKey = Options.Settings[ConfigurationKeys.AccessKey];
            var accessToken = Options.Settings[ConfigurationKeys.AccessToken];
            var securedConnection = Options.Settings[ConfigurationKeys.SecuredConnection];

            Client = new MinioClient(endpoint, accessKey, accessToken);

            if (bool.Parse(securedConnection))
            {
                Client.WithSSL();
            }

            return Client;
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
