// ------------------------------------------------------------------------------------------------
//  <copyright file="OneTimePassword.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;

    /// <summary>
    /// RFC 6238 time-based one-time passwords with Keycloak's default policy: HmacSHA1, 6 digits, 30 s.
    /// </summary>
    internal static class OneTimePassword
    {
        /// <summary>
        /// The RFC 4648 base32 alphabet Keycloak encodes the secret with.
        /// </summary>
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// The lifetime of one code, Keycloak's default policy.
        /// </summary>
        private const int PeriodSeconds = 30;

        /// <summary>
        /// Computes the code of the current period, or of a neighbouring one.
        /// </summary>
        /// <param name="base32Secret">The secret as Keycloak shows it in manual mode (spaces allowed).</param>
        /// <param name="periodOffset">0 for the current period, 1 for the next one.</param>
        /// <returns>The six-digit code.</returns>
        public static string Compute(string base32Secret, int periodOffset)
        {
            var counter = new byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counter, (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds) + periodOffset);

            var hash = HMACSHA1.HashData(DecodeBase32(base32Secret), counter);
            var offset = hash[^1] & 0x0f;
            var truncated = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7fffffff;

            return (truncated % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Decodes a base32 secret, ignoring spaces and case.
        /// </summary>
        /// <param name="text">The secret as Keycloak shows it.</param>
        /// <returns>The secret bytes.</returns>
        private static byte[] DecodeBase32(string text)
        {
            var values = text.Where(char.IsLetterOrDigit).Select(character => Base32Alphabet.IndexOf(char.ToUpperInvariant(character)));
            var bits = string.Concat(values.Select(value => Convert.ToString(value, 2).PadLeft(5, '0')));

            return [.. Enumerable.Range(0, bits.Length / 8).Select(index => Convert.ToByte(bits.Substring(index * 8, 8), 2))];
        }
    }
}
