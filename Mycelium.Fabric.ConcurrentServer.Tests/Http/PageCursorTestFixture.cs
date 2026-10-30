// ------------------------------------------------------------------------------------------------
//  <copyright file="PageCursorTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Http
{
    using System;
    using System.Text;

    using Mycelium.Fabric.ConcurrentServer.Http;

    using SysML2.NET.PSM;

    [TestFixture]
    public class PageCursorTestFixture
    {
        private static readonly Guid Identifier = Guid.Parse("20109f40-4825-416a-86f6-7508ac42a021");

        [Test]
        public void VerifyEncode()
        {
            var cursor = PageCursor.Encode(new Page { Epoch = 1618861496, Identifier = Identifier });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), Is.EqualTo($"1618861496|{Identifier}"));
                Assert.That(cursor, Is.Not.EqualTo(PageCursor.Encode(new Page { Epoch = 1618861497, Identifier = Identifier })));
            }
        }

        [Test]
        public void VerifyTryDecode()
        {
            var cursor = PageCursor.Encode(new Page { Epoch = 1618861496, Identifier = Identifier });

            Assert.That(PageCursor.TryDecode(cursor, out var page), Is.True);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Epoch, Is.EqualTo(1618861496));
                Assert.That(page.Identifier, Is.EqualTo(Identifier));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(PageCursor.TryDecode(null, out _), Is.False);
                Assert.That(PageCursor.TryDecode(string.Empty, out _), Is.False);
                Assert.That(PageCursor.TryDecode("   ", out _), Is.False);
                Assert.That(PageCursor.TryDecode("not base64 at all!", out _), Is.False);
                Assert.That(PageCursor.TryDecode(Base64("no separator"), out _), Is.False);
                Assert.That(PageCursor.TryDecode(Base64($"notanepoch|{Identifier}"), out _), Is.False);
                Assert.That(PageCursor.TryDecode(Base64("1618861496|not-a-guid"), out _), Is.False);
            }

            // Page.Epoch is 32-bit in the SDK, so a millisecond timestamp does not round-trip and is
            // rejected rather than truncated.
            Assert.That(PageCursor.TryDecode(Base64($"1618861496632|{Identifier}"), out _), Is.False);
        }

        private static string Base64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }
    }
}
