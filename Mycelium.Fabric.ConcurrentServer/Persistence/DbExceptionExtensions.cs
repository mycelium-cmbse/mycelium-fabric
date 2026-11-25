// ------------------------------------------------------------------------------------------------
//  <copyright file="DbExceptionExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Data.Common;

    using ErrorOr;

    /// <summary>
    /// Translates the failures a SQL database reports into the errors the persistence contracts return.
    /// </summary>
    /// <remarks>
    /// The translation reads the SQLSTATE of the failure, whose classes ISO/IEC 9075 defines.
    /// </remarks>
    public static class DbExceptionExtensions
    {
        /// <summary>
        /// The SQLSTATE of a uniqueness violation.
        /// </summary>
        private const string UniqueViolation = "23505";

        /// <summary>
        /// The SQLSTATE of a foreign key violation.
        /// </summary>
        private const string ForeignKeyViolation = "23503";

        /// <summary>
        /// The SQLSTATE class of the integrity constraint violations.
        /// </summary>
        private const string IntegrityConstraintViolationClass = "23";

        /// <summary>
        /// The SQLSTATE class of the data exceptions, such as a value of the wrong representation.
        /// </summary>
        private const string DataExceptionClass = "22";

        /// <summary>
        /// The SQLSTATE class of the transaction rollbacks, such as a serialization failure or a deadlock.
        /// </summary>
        private const string TransactionRollbackClass = "40";

        /// <param name="exception">The <see cref="DbException"/> that is translated.</param>
        extension(DbException exception)
        {
            /// <summary>
            /// Translates the exception into the <see cref="Error"/> that states what the store refused.
            /// </summary>
            /// <returns>
            /// A <see cref="ErrorType.Conflict"/> for a uniqueness or concurrency failure, a <see cref="ErrorType.Validation"/> for a broken
            /// integrity constraint or an invalid value, and an <see cref="ErrorType.Unexpected"/> error otherwise.
            /// </returns>
            /// <exception cref="ArgumentNullException">The exception is <c>null</c>.</exception>
            public Error ToError()
            {
                ArgumentNullException.ThrowIfNull(exception);

                return exception.SqlState switch
                {
                    UniqueViolation => Error.Conflict("store.unique", exception.Message),
                    ForeignKeyViolation => Error.Validation("store.reference", exception.Message),
                    { } sqlState when sqlState.StartsWith(TransactionRollbackClass, StringComparison.Ordinal) => Error.Conflict("store.concurrency", exception.Message),
                    { } sqlState when sqlState.StartsWith(IntegrityConstraintViolationClass, StringComparison.Ordinal)
                                      || sqlState.StartsWith(DataExceptionClass, StringComparison.Ordinal) => Error.Validation("store.constraint", exception.Message),
                    _ => Error.Unexpected("store.failure", exception.Message)
                };
            }
        }
    }
}
