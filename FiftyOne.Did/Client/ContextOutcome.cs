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
    /// The creator context verdict the cloud's redeem endpoint reports,
    /// mapped from the <c>context</c> string in its answer. The first four
    /// are verdicts about the identifier. The rest describe the redemption
    /// itself, being why no verdict could be read this time.
    /// </summary>
    public enum ContextOutcome
    {
        /// <summary>
        /// Every factor of the creator context matched the connection the
        /// identifier was verified on.
        /// </summary>
        Verified,

        /// <summary>
        /// At least one factor did not match, and
        /// <see cref="RedeemResult.Factors"/> says which. An identifier
        /// whose signature verifies is still genuine, so this reports a
        /// moved identifier rather than a bad one.
        /// </summary>
        Mismatch,

        /// <summary>
        /// The identifier carries no creator context at all, for example
        /// because a self-hosted service was configured not to add one.
        /// </summary>
        NoContext,

        /// <summary>
        /// Nothing could be checked, because the service holds no context
        /// key covering the identifier's creation time.
        /// </summary>
        NotCheckable,

        /// <summary>
        /// The sealed result was presented after the service's freshness
        /// window closed. <see cref="RedeemResult.VerifiedAt"/> says when
        /// the verification happened.
        /// </summary>
        Expired,

        /// <summary>
        /// The sealed result had already been redeemed on this service
        /// instance.
        /// </summary>
        Replayed,

        /// <summary>
        /// The sealed result could not be read. Every cryptographic failure
        /// (tampered, made for another identifier, sealed under a key the
        /// service does not hold, or presented without a licence key the
        /// account requires) gives this one answer by design, so nothing
        /// finer is available. A <c>context</c> string this client does not
        /// recognise also maps here, with the raw string kept in
        /// <see cref="RedeemResult.ContextValue"/>.
        /// </summary>
        Unreadable,

        /// <summary>
        /// The service could not confirm first use of the sealed result and
        /// answered 503. Not a verdict. The caller may retry.
        /// </summary>
        Unconfirmed,
    }
}
