/*
 * Copyright 2023 MONAI Consortium
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

using System.Security.Cryptography;

namespace Monai.Deploy.Storage.SimpleStorage
{
    public class HashCreator : IHashCreator
    {
        public async Task<string> GetMd5Hash(Stream dataStream)
        {
            using var md5 = MD5.Create();
            dataStream.Seek(0, SeekOrigin.Begin);
            var compMd5 = await md5.ComputeHashAsync(dataStream).ConfigureAwait(false);
            return BitConverter.ToString(compMd5).Replace("-", "").ToLowerInvariant();
        }
    }
}
