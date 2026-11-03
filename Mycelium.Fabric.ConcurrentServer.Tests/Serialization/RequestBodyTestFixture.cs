// ------------------------------------------------------------------------------------------------
//  <copyright file="RequestBodyTestFixture.cs" company="Starion Group S.A.">
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
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.PSM.DTO;

    using Error = ErrorOr.Error;

    [TestFixture]
    public class RequestBodyTestFixture
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
            Assert.That(async () => await RequestBody<BranchRequest>.BindAsync(null, Parameter), Throws.TypeOf<ArgumentNullException>());

            var request = new BranchRequest { Name = "feature", Head = Guid.NewGuid() };

            this.reader
                .Setup(bodyReader => bodyReader.ReadRequestAsync<BranchRequest>(this.context.Request))
                .ReturnsAsync(request);

            var body = await RequestBody<BranchRequest>.BindAsync(this.context, Parameter);

            Assert.That(body.Content.Value, Is.SameAs(request));

            this.reader
                .Setup(bodyReader => bodyReader.ReadRequestAsync<BranchRequest>(this.context.Request))
                .ReturnsAsync(Error.Validation("body.invalid", "malformed"));

            body = await RequestBody<BranchRequest>.BindAsync(this.context, Parameter);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body, Is.Not.Null);
                Assert.That(body.Content.FirstError.Type, Is.EqualTo(ErrorType.Validation));
            }

            // A body read in full is still checked against the members the OpenAPI document requires.
            this.reader
                .Setup(bodyReader => bodyReader.ReadRequestAsync<BranchRequest>(this.context.Request))
                .ReturnsAsync(new BranchRequest { Name = "feature" });

            body = await RequestBody<BranchRequest>.BindAsync(this.context, Parameter);

            Assert.That(body.Content.FirstError.Code, Is.EqualTo("body.head.missing"));
        }
    }
}
