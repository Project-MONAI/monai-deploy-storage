/*
 * Copyright 2021-2022 MONAI Consortium
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Minio;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.MinIO
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
