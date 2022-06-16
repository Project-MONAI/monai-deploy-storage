// SPDX-FileCopyrightText: © 2022 MONAI Consortium
// SPDX-License-Identifier: Apache License 2.0

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
