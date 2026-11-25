// ------------------------------------------------------------------------------------------------
//  <copyright file="DbExceptionExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence
{
    using System;
    using System.Data.Common;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    [TestFixture]
    public class DbExceptionExtensionsTestFixture
    {
        [Test]
        public void VerifyToError()
        {
            Assert.That(() => ((DbException)null).ToError(), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Translate("23505"), Is.EqualTo((ErrorType.Conflict, "store.unique")));
                Assert.That(Translate("40001"), Is.EqualTo((ErrorType.Conflict, "store.concurrency")));
                Assert.That(Translate("40P01"), Is.EqualTo((ErrorType.Conflict, "store.concurrency")));
                Assert.That(Translate("23503"), Is.EqualTo((ErrorType.Validation, "store.reference")));
                Assert.That(Translate("23502"), Is.EqualTo((ErrorType.Validation, "store.constraint")));
                Assert.That(Translate("23514"), Is.EqualTo((ErrorType.Validation, "store.constraint")));
                Assert.That(Translate("22P02"), Is.EqualTo((ErrorType.Validation, "store.constraint")));
                Assert.That(Translate("25006"), Is.EqualTo((ErrorType.Unexpected, "store.failure")));
                Assert.That(Translate(null), Is.EqualTo((ErrorType.Unexpected, "store.failure")));
                Assert.That(CreateException("23505").ToError().Description, Is.EqualTo("the message"));
            }
        }

        private static DbException CreateException(string sqlState)
        {
            var exception = new Mock<DbException>();
            exception.Setup(dbException => dbException.SqlState).Returns(sqlState);
            exception.Setup(dbException => dbException.Message).Returns("the message");

            return exception.Object;
        }

        private static (ErrorType Type, string Code) Translate(string sqlState)
        {
            var error = CreateException(sqlState).ToError();

            return (error.Type, error.Code);
        }
    }
}
