// ------------------------------------------------------------------------------------------------
//  <copyright file="DataBodyTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Serialization
{
    using System;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Systems.Parts;

    [TestFixture]
    public class DataBodyTestFixture
    {
        private Mock<IRequestBodyReader> reader;

        private static readonly ParameterInfo Parameter = Mock.Of<ParameterInfo>();

        private DefaultHttpContext context;

        [SetUp]
        public void SetUp()
        {
            this.reader = new Mock<IRequestBodyReader>();

            this.context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton(this.reader.Object).BuildServiceProvider()
            };
        }

        [Test]
        public async Task VerifyBindAsync()
        {
            Assert.That(async () => await DataBody.BindAsync(null, Parameter), Throws.TypeOf<ArgumentNullException>());

            IReadOnlyList<IData> data = [new PartUsage { Id = Guid.NewGuid() }];

            this.reader
                .Setup(bodyReader => bodyReader.ReadDataAsync(this.context.Request))
                .ReturnsAsync(ErrorOrFactory.From(data));

            var body = await DataBody.BindAsync(this.context, Parameter);

            Assert.That(body.Content.Value, Is.SameAs(data));

            this.reader
                .Setup(bodyReader => bodyReader.ReadDataAsync(this.context.Request))
                .ReturnsAsync(Error.Custom(RequestBodyReader.UnsupportedMediaType, "body.unsupportedMediaType", "unsupported"));

            body = await DataBody.BindAsync(this.context, Parameter);

            Assert.That(body.Content.FirstError.NumericType, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));
        }
    }
}
