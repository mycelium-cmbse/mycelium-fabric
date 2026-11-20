// ------------------------------------------------------------------------------------------------
//  <copyright file="CoreDataServiceTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;

    [TestFixture]
    public class CoreDataServiceTestFixture
    {
        private Mock<IElementRule<IFeature>> featureRule;

        private Mock<IElementRule<IElement>> elementRule;

        private Mock<IElementRule<IPartUsage>> partUsageRule;

        private Mock<IElementRule> markerOnlyRule;

        private Mock<IElementEffect<IFeature>> featureEffect;

        private Mock<IElementEffect<IElement>> elementEffect;

        private Mock<IElementEffect<IPartUsage>> partUsageEffect;

        private Mock<IElementEffect> markerOnlyEffect;

        private Mock<IChangeContext> context;

        private CoreDataService<IFeature> service;

        [SetUp]
        public void SetUp()
        {
            this.featureRule = new Mock<IElementRule<IFeature>>(MockBehavior.Strict);

            this.featureRule.Setup(rule => rule.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IFeature>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success);

            this.elementRule = new Mock<IElementRule<IElement>>(MockBehavior.Strict);

            this.elementRule.Setup(rule => rule.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success);

            this.partUsageRule = new Mock<IElementRule<IPartUsage>>(MockBehavior.Strict);
            this.markerOnlyRule = new Mock<IElementRule>(MockBehavior.Strict);

            this.featureEffect = new Mock<IElementEffect<IFeature>>(MockBehavior.Strict);

            this.featureEffect.Setup(effect => effect.ComputeAsync(It.IsAny<IChangeContext>(), It.IsAny<IFeature>(), It.IsAny<ChangeType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>(Array.Empty<DataVersion>()));

            this.elementEffect = new Mock<IElementEffect<IElement>>(MockBehavior.Strict);

            this.elementEffect.Setup(effect => effect.ComputeAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>(Array.Empty<DataVersion>()));

            this.partUsageEffect = new Mock<IElementEffect<IPartUsage>>(MockBehavior.Strict);
            this.markerOnlyEffect = new Mock<IElementEffect>(MockBehavior.Strict);
            this.context = new Mock<IChangeContext>();

            IEnumerable<IElementRule> rules = [this.featureRule.Object, this.partUsageRule.Object, this.elementRule.Object, this.markerOnlyRule.Object];
            IEnumerable<IElementEffect> effects = [this.featureEffect.Object, this.partUsageEffect.Object, this.elementEffect.Object, this.markerOnlyEffect.Object];

            this.service = new CoreDataService<IFeature>(rules, effects);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new CoreDataService<IFeature>(null, []), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new CoreDataService<IFeature>([], null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public async Task VerifyComputeEffectsAsync()
        {
            var feature = new Feature { Id = Guid.NewGuid() };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.service.ComputeEffectsAsync(null, feature, ChangeType.CREATED, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.service.ComputeEffectsAsync(this.context.Object, null, ChangeType.CREATED, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

                Assert.That(() => this.service.ComputeEffectsAsync(this.context.Object, new Comment { Id = Guid.NewGuid() }, ChangeType.CREATED, CancellationToken.None),
                    Throws.TypeOf<ArgumentException>());
            }

            Assert.That((await this.service.ComputeEffectsAsync(this.context.Object, feature, ChangeType.DELETED, CancellationToken.None)).Value, Is.Empty);

            this.featureEffect.Verify(effect => effect.ComputeAsync(this.context.Object, feature, ChangeType.DELETED, It.IsAny<CancellationToken>()), Times.Once);
            this.elementEffect.Verify(effect => effect.ComputeAsync(this.context.Object, feature, ChangeType.DELETED, It.IsAny<CancellationToken>()), Times.Once);

            var featureVersion = new DataVersion { Id = Guid.NewGuid() };
            var elementVersion = new DataVersion { Id = Guid.NewGuid() };

            this.featureEffect.Setup(effect => effect.ComputeAsync(It.IsAny<IChangeContext>(), It.IsAny<IFeature>(), It.IsAny<ChangeType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>([featureVersion]));

            this.elementEffect.Setup(effect => effect.ComputeAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>([elementVersion]));

            Assert.That((await this.service.ComputeEffectsAsync(this.context.Object, feature, ChangeType.UPDATED, CancellationToken.None)).Value,
                Is.EqualTo([featureVersion, elementVersion]));

            this.elementEffect.Setup(effect => effect.ComputeAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<ChangeType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("element.effect"));

            var failed = await this.service.ComputeEffectsAsync(this.context.Object, feature, ChangeType.UPDATED, CancellationToken.None);

            Assert.That(failed.Errors, Is.EqualTo([Error.Validation("element.effect")]));
        }

        [Test]
        public async Task VerifyValidateAsync()
        {
            var feature = new Feature { Id = Guid.NewGuid() };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.service.ValidateAsync(null, feature, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.service.ValidateAsync(this.context.Object, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.service.ValidateAsync(this.context.Object, new Comment { Id = Guid.NewGuid() }, CancellationToken.None), Throws.TypeOf<ArgumentException>());
            }

            Assert.That((await this.service.ValidateAsync(this.context.Object, feature, CancellationToken.None)).IsError, Is.False);

            this.featureRule.Verify(rule => rule.ValidateAsync(this.context.Object, feature, It.IsAny<CancellationToken>()), Times.Once);
            this.elementRule.Verify(rule => rule.ValidateAsync(this.context.Object, feature, It.IsAny<CancellationToken>()), Times.Once);

            this.elementRule.Setup(rule => rule.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("element.invalid"));

            this.featureRule.Setup(rule => rule.ValidateAsync(It.IsAny<IChangeContext>(), It.IsAny<IFeature>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Validation("feature.invalid"));

            var invalid = await this.service.ValidateAsync(this.context.Object, feature, CancellationToken.None);

            Assert.That(invalid.Errors, Is.EqualTo([Error.Validation("feature.invalid"), Error.Validation("element.invalid")]));
        }
    }
}
