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
    /// The outcome of one creator context factor, reported when the context
    /// is <see cref="ContextOutcome.Mismatch"/> or
    /// <see cref="ContextOutcome.Misconfigured"/>.
    /// </summary>
    public enum FactorOutcome
    {
        /// <summary>The factor matched the verifying connection.</summary>
        Verified,

        /// <summary>The factor did not match the verifying connection.</summary>
        Mismatch,

        /// <summary>
        /// The service that checked the identifier is not configured to
        /// determine this factor, so it could not have checked it for any
        /// request. This is not a mismatch and must not be read as one, since
        /// the identifier says nothing about it either way. Nothing a caller
        /// sends can produce it.
        /// </summary>
        Misconfigured,
    }
}
