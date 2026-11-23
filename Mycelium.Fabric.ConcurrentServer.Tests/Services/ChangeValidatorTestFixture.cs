// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeValidatorTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Testing;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM.DTO;

    [TestFixture]
    public class ChangeValidatorTestFixture
    {
        private Mock<ICoreDataService> partUsageValidator;

        private Mock<ICoreDataService> dependencyValidator;

        private Mock<ICoreDataServiceProvider> serviceProvider;

        private Mock<IChangeContext> context;

        private FakeLogger<ChangeValidator> logger;

        [SetUp]
        public void SetUp()
        {
            this.partUsageValidator = new Mock<ICoreDataService>(MockBehavior.Strict);

            this.partUsageValidator.Setup(validator => validator.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success);

            this.dependencyValidator = new Mock<ICoreDataService>(MockBehavior.Strict);

            this.dependencyValidator.Setup(validator => validator.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success);

            this.serviceProvider = new Mock<ICoreDataServiceProvider>(MockBehavior.Strict);
            this.serviceProvider.Setup(provider => provider.Resolve(typeof(PartUsage))).Returns(this.partUsageValidator.Object);
            this.serviceProvider.Setup(provider => provider.Resolve(typeof(Dependency))).Returns(this.dependencyValidator.Object);

            this.context = new Mock<IChangeContext>();
            this.logger = new FakeLogger<ChangeValidator>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ChangeValidator(null, this.logger), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ChangeValidator(this.serviceProvider.Object, null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public async Task VerifyValidateAsync()
        {
            var changeValidator = new ChangeValidator(this.serviceProvider.Object, this.logger);

            Assert.That(() => changeValidator.ValidateAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            this.context.Setup(context => context.Change).Returns([]);

            Assert.That((await changeValidator.ValidateAsync(this.context.Object, CancellationToken.None)).IsError, Is.False);

            var partUsage = new PartUsage { Id = Guid.NewGuid() };
            var dependency = new Dependency { Id = Guid.NewGuid() };

            this.context.Setup(context => context.Change).Returns(
            [
                new DataVersion { Payload = partUsage },
                new DataVersion { Payload = dependency },
                new DataVersion { Identity = new DataIdentity { Id = Guid.NewGuid() } },
                new DataVersion { Payload = new ProjectUsage { Id = Guid.NewGuid() } }
            ]);

            var valid = await changeValidator.ValidateAsync(this.context.Object, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(valid.IsError, Is.False);
                Assert.That(this.logger.LatestRecord.Level, Is.EqualTo(LogLevel.Debug));
                Assert.That(this.logger.LatestRecord.Message, Is.EqualTo("Validated 2 elements of a change of 4 versions, with 0 errors"));
                this.partUsageValidator.Verify(validator => validator.ValidateAsync(this.context.Object, It.IsAny<IElement>(), It.IsAny<CancellationToken>()), Times.Once);
                this.partUsageValidator.Verify(validator => validator.ValidateAsync(this.context.Object, partUsage, It.IsAny<CancellationToken>()), Times.Once);
                this.dependencyValidator.Verify(validator => validator.ValidateAsync(this.context.Object, It.IsAny<IElement>(), It.IsAny<CancellationToken>()), Times.Once);
                this.dependencyValidator.Verify(validator => validator.ValidateAsync(this.context.Object, dependency, It.IsAny<CancellationToken>()), Times.Once);
            }

            this.partUsageValidator.Setup(validator => validator.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("partUsage.invalid"));

            this.dependencyValidator.Setup(validator => validator.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("dependency.invalid"));

            var invalid = await changeValidator.ValidateAsync(this.context.Object, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invalid.Errors, Is.EqualTo([Error.Validation("partUsage.invalid"), Error.Validation("dependency.invalid")]));
                Assert.That(this.logger.LatestRecord.Message, Does.EndWith("with 2 errors"));
                Assert.That(this.logger.Collector.Count, Is.EqualTo(3));
            }
        }
    }
}
