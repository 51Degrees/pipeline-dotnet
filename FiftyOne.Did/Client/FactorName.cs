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

using System.Collections.Generic;

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// The names of the creator context factors, as the cloud writes them
    /// in the <c>factors</c> object of a verify or redeem answer and as
    /// they appear as keys of <see cref="RedeemResult.Factors"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The browser used to be reported as one factor named
    /// <c>browser</c>. Cloud release 4.4.38 reports it as four, being the
    /// operating system name and version and the browser name and
    /// version, and <c>browser</c> is no longer sent. A version mismatch
    /// beside a verified name means an upgrade, whilst a mismatched name
    /// means a different operating system or browser.
    /// </para>
    /// <para>
    /// <see cref="RedeemResult.Factors"/> keeps every name the cloud sent,
    /// including any this package does not list here, so a factor added
    /// by a later cloud is still reported.
    /// </para>
    /// </remarks>
    public static class FactorName
    {
        /// <summary>The browser's connection characteristics.</summary>
        public const string Transport = "transport";

        /// <summary>The device hardware.</summary>
        public const string Device = "device";

        /// <summary>
        /// The network of the public address the browser presents through
        /// the request chain.
        /// </summary>
        public const string BrowserIp = "browserip";

        /// <summary>
        /// The network of the address the connection actually arrived
        /// from.
        /// </summary>
        public const string ConnectionIp = "connectionip";

        /// <summary>
        /// The autonomous system number, being the number identifying
        /// which operator announces the visitor's IP address to the
        /// internet.
        /// </summary>
        public const string Asn = "asn";

        /// <summary>The operating system name.</summary>
        public const string PlatformName = "platformname";

        /// <summary>The operating system version.</summary>
        public const string PlatformVersion = "platformversion";

        /// <summary>The browser name.</summary>
        public const string BrowserName = "browsername";

        /// <summary>The browser version.</summary>
        public const string BrowserVersion = "browserversion";

        /// <summary>
        /// Every factor name this package knows, in the order the cloud
        /// lists them.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Transport,
            Device,
            BrowserIp,
            ConnectionIp,
            Asn,
            PlatformName,
            PlatformVersion,
            BrowserName,
            BrowserVersion,
        };
    }
}
