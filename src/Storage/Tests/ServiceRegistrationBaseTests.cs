/*
 * Copyright 2022 MONAI Consortium
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

using System;
using Microsoft.Extensions.DependencyInjection;
using Monai.Deploy.Storage.Configuration;
using Xunit;

namespace Monai.Deploy.Storage.Tests
{
    internal class TestServiceRegistration : ServiceRegistrationBase
    {
        public TestServiceRegistration(string fullyQualifiedAssemblyName) : base(fullyQualifiedAssemblyName)
        {
        }

        public override IServiceCollection Configure(IServiceCollection services) => throw new NotImplementedException();
    }

    public class ServiceRegistrationBaseTests
    {
        [Theory(DisplayName = "ParseAssemblyName - throws if fully qualified assembly name is invalid")]
        [InlineData("mytype")]
        [InlineData("mytype,, myversion")]
        public void ParseAssemblyName_ThrowIfFullyQualifiedAssemblyNameIsInvalid(string assemblyeName)
        {
            Assert.Throws<ConfigurationException>(() => new TestServiceRegistrar(assemblyeName));
        }
    }
}
