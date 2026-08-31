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

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// The signature outcome the cloud's redeem endpoint reports, mapped
    /// from the <c>signature</c> string in its answer.
    /// </summary>
    public enum SignatureOutcome
    {
        /// <summary>
        /// The identifier was signed by a 51Degrees signing key.
        /// </summary>
        Verified,

        /// <summary>
        /// The identifier was not signed by a 51Degrees signing key.
        /// </summary>
        Invalid,

        /// <summary>
        /// The answer carried no signature outcome. The redeem endpoint
        /// only reports one on the redeemed outcome, so every other
        /// <see cref="ContextOutcome"/> comes with this value.
        /// </summary>
        Unknown,
    }
}
