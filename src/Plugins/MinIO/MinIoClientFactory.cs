/*
 * Copyright 2021-2023 MONAI Consortium
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

using System.Collections.Concurrent;
using Amazon.SecurityToken.Model;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Minio;
using Minio.ApiEndpoints;
using Monai.Deploy.Storage.Configuration;

namespace Monai.Deploy.Storage.MinIO
{
    public class MinIoClientFactory : IMinIoClientFactory
    {
        private static readonly string DefaultClient = "_DEFAULT_";
        internal static readonly int DefaultTimeout = 2500;
        private readonly ConcurrentDictionary<string, IMinioClient> _clients;

        private StorageServiceConfiguration Options { get; }

        public MinIoClientFactory(IOptions<StorageServiceConfiguration> options)
        {
            Guard.Against.Null(options, nameof(options));

            var configuration = options.Value;
            ValidateConfiguration(configuration);

            Options = configuration;

            _clients = new ConcurrentDictionary<string, IMinioClient>();
        }

        public IMinioClient GetClient()
        {
            return _clients.GetOrAdd(DefaultClient, _ =>
            {
                var accessKey = Options.Settings[ConfigurationKeys.AccessKey];
                var accessToken = Options.Settings[ConfigurationKeys.AccessToken];
                var client = CreateClient(accessKey, accessToken);

                return client.Build();
            });
        }

        public IMinioClient GetClient(Credentials credentials)
        {
            return GetClient(credentials, string.Empty);
        }

        public IMinioClient GetClient(Credentials credentials, string region)
        {
            return GetClientInternal(credentials, region);
        }

        public IBucketOperations GetBucketOperationsClient()
        {
            return _clients.GetOrAdd(DefaultClient, _ =>
            {
                var accessKey = Options.Settings[ConfigurationKeys.AccessKey];
                var accessToken = Options.Settings[ConfigurationKeys.AccessToken];
                var client = CreateClient(accessKey, accessToken);

                return client.Build();
            });
        }

        public IBucketOperations GetBucketOperationsClient(Credentials credentials)
        {
            return GetClientInternal(credentials, string.Empty);
        }

        public IBucketOperations GetBucketOperationsClient(Credentials credentials, string region)
        {
            return GetClientInternal(credentials, region);
        }

        public IObjectOperations GetObjectOperationsClient()
        {
            return _clients.GetOrAdd(DefaultClient, _ =>
                    {
                        var accessKey = Options.Settings[ConfigurationKeys.AccessKey];
                        var accessToken = Options.Settings[ConfigurationKeys.AccessToken];
                        var client = CreateClient(accessKey, accessToken);

                        return client.Build();
                    });
        }

        public IObjectOperations GetObjectOperationsClient(Credentials credentials)
        {
            return GetClientInternal(credentials, string.Empty);
        }

        public IObjectOperations GetObjectOperationsClient(Credentials credentials, string region)
        {
            return GetClientInternal(credentials, region);
        }

        private IMinioClient CreateClient(string accessKey, string accessToken)
        {
            var endpoint = Options.Settings[ConfigurationKeys.EndPoint];
            var securedConnection = Options.Settings[ConfigurationKeys.SecuredConnection];
            var timeout = DefaultTimeout;

            if (Options.Settings.ContainsKey(ConfigurationKeys.ApiCallTimeout) && !int.TryParse(Options.Settings[ConfigurationKeys.ApiCallTimeout], out timeout))
            {
                throw new ConfigurationException($"Invalid value specified for {ConfigurationKeys.ApiCallTimeout}: {Options.Settings[ConfigurationKeys.ApiCallTimeout]}");
            }

            var client = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, accessToken)
                .WithTimeout(timeout);

            if (bool.Parse(securedConnection))
            {
                client.WithSSL();
            }

            return client;
        }

        private IMinioClient GetClientInternal(Credentials credentials, string region)
        {
            Guard.Against.Null(credentials, nameof(credentials));
            Guard.Against.NullOrWhiteSpace(credentials.AccessKeyId, nameof(credentials.AccessKeyId));
            Guard.Against.NullOrWhiteSpace(credentials.SecretAccessKey, nameof(credentials.SecretAccessKey));
            Guard.Against.NullOrWhiteSpace(credentials.SessionToken, nameof(credentials.SessionToken));

            return _clients.GetOrAdd(credentials.SessionToken, _ =>
            {
                var client = CreateClient(credentials.AccessKeyId, credentials.SecretAccessKey);
                client.WithSessionToken(credentials.SessionToken);

                if (!string.IsNullOrWhiteSpace(region))
                {
                    client.WithRegion(region);
                }

                return client.Build();
            });
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
