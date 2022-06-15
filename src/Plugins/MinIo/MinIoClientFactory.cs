// SPDX-FileCopyrightText: © 2021-2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Minio;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.MinIo
{
    public class MinIoClientFactory : IMinIoClientFactory
    {
        private static readonly string DefaultClient = "_DEFAULT_";
        private readonly Dictionary<string, MinioClient> _clients;

        private StorageServiceConfiguration Options { get; }

        public MinIoClientFactory(IOptions<StorageServiceConfiguration> options)
        {
            Guard.Against.Null(options, nameof(options));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            Options = configuration;

            _clients = new Dictionary<string, MinioClient>();
        }

        public MinioClient GetClient()
        {
            if (_clients.ContainsKey(DefaultClient))
            {
                return _clients[DefaultClient];
            }

            var accessKey = Options.Settings[ConfigurationKeys.AccessKey];
            var accessToken = Options.Settings[ConfigurationKeys.AccessToken];
            var client = CreateClient(accessKey, accessToken);

            _clients[DefaultClient] = client.Build();
            return _clients[DefaultClient];
        }

        public MinioClient GetClient(Credentials credentials)
        {
            return GetClient(credentials, string.Empty);
        }

        public MinioClient GetClient(Credentials credentials, string region)
        {
            Guard.Against.Null(credentials, nameof(credentials));
            Guard.Against.NullOrWhiteSpace(credentials.AccessKeyId, nameof(credentials.AccessKeyId));
            Guard.Against.NullOrWhiteSpace(credentials.SecretAccessKey, nameof(credentials.SecretAccessKey));
            Guard.Against.NullOrWhiteSpace(credentials.SessionToken, nameof(credentials.SessionToken));

            if (_clients.ContainsKey(credentials.SessionToken))
            {
                return _clients[credentials.SessionToken];
            }

            var client = CreateClient(credentials.AccessKeyId, credentials.SecretAccessKey);
            client.WithSessionToken(credentials.SessionToken);

            if (!string.IsNullOrWhiteSpace(region))
            {
                client.WithRegion(region);
            }

            _clients[DefaultClient] = client.Build();
            return _clients[DefaultClient];
        }

        private MinioClient CreateClient(string accessKey, string accessToken)
        {
            var endpoint = Options.Settings[ConfigurationKeys.EndPoint];
            var securedConnection = Options.Settings[ConfigurationKeys.SecuredConnection];

            var client = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, accessToken);

            if (bool.Parse(securedConnection))
            {
                client.WithSSL();
            }

            return client;
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
