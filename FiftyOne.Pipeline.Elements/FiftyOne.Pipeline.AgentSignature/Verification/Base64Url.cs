/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using System;

namespace FiftyOne.Pipeline.AgentSignature.Verification
{
    /// <summary>
    /// The base64url encoding without padding that RFC 7515 Appendix C
    /// defines. JSON Web Keys and key thumbprints use it.
    /// </summary>
    internal static class Base64Url
    {
        /// <summary>
        /// Decode base64url text into bytes.
        /// </summary>
        /// <param name="value">The text to decode.</param>
        /// <param name="bytes">The bytes decoded.</param>
        /// <returns>True if the text could be decoded.</returns>
        public static bool TryDecode(string value, out byte[] bytes)
        {
            bytes = null;
            if (value == null)
            {
                return false;
            }
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
                case 1:
                    return false;
                default:
                    break;
            }
            try
            {
                bytes = Convert.FromBase64String(padded);
            }
            catch (FormatException)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Encode bytes as base64url text without padding.
        /// </summary>
        /// <param name="bytes">The bytes to encode.</param>
        /// <returns>The encoded text.</returns>
        public static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
