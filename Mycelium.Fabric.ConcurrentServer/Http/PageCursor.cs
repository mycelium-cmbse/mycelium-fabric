// ------------------------------------------------------------------------------------------------
//  <copyright file="PageCursor.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Http
{
    using System;
    using System.Text;

    using SysML2.NET.PSM;

    /// <summary>
    /// Encodes and decodes the opaque base64 <c>epoch|identifier</c> pagination cursor (§8.1.3).
    /// </summary>
    public static class PageCursor
    {
        /// <summary>
        /// The separator between the epoch and the identifier.
        /// </summary>
        private const char Separator = '|';

        /// <summary>
        /// Encodes a page as a cursor.
        /// </summary>
        /// <param name="page">The page to encode.</param>
        /// <returns>The cursor.</returns>
        public static string Encode(Page page)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(page.ToString()));
        }

        /// <summary>
        /// Decodes a cursor, rejecting an epoch that does not fit the 32-bit <see cref="Page.Epoch"/>.
        /// </summary>
        /// <param name="cursor">The cursor from the query string.</param>
        /// <param name="page">The decoded page.</param>
        /// <returns>True when the cursor is well formed.</returns>
        public static bool TryDecode(string cursor, out Page page)
        {
            page = default;

            if (string.IsNullOrWhiteSpace(cursor))
            {
                return false;
            }

            if (!TryFromBase64(cursor, out var decoded))
            {
                return false;
            }

            var separatorIndex = decoded.IndexOf(Separator);

            if (separatorIndex < 0)
            {
                return false;
            }

            if (!int.TryParse(decoded[..separatorIndex], out var epoch) || !Guid.TryParse(decoded[(separatorIndex + 1)..], out var identifier))
            {
                return false;
            }

            page = new Page { Epoch = epoch, Identifier = identifier };

            return true;
        }

        /// <summary>
        /// Decodes the base64 text of a cursor.
        /// </summary>
        /// <param name="cursor">The cursor from the query string.</param>
        /// <param name="decoded">The decoded text.</param>
        /// <returns>True when the cursor is valid base64 of UTF-8.</returns>
        private static bool TryFromBase64(string cursor, out string decoded)
        {
            decoded = null;

            var buffer = new byte[cursor.Length * 3 / 4 + 3];

            if (!Convert.TryFromBase64String(cursor, buffer, out var written))
            {
                return false;
            }

            decoded = Encoding.UTF8.GetString(buffer, 0, written);

            return true;
        }
    }
}
