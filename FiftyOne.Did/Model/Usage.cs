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

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// The usage a 51Did was created for, carried in bits 0-2 of
    /// the 51Did flags byte. It decides where the identifier may
    /// go: one created for <see cref="NonMarketing"/> must never be
    /// passed to a demand source, and one created for
    /// <see cref="Standard"/> or <see cref="Personalized"/> may be
    /// passed only to a recipient that has accepted the applicable terms.
    /// <para>
    /// The three usages are cumulative rather than exclusive in the byte.
    /// Non-marketing sets bit 0, standard sets bits 0 and 1, and
    /// personalized sets bits 0, 1 and 2, so every marketing identifier
    /// also carries the non-marketing bit. A caller who masked the byte
    /// for that bit alone would read every marketing identifier as
    /// non-marketing, which is the wrong way round for a data protection
    /// decision. <see cref="FodId.Usage"/> answers with the highest
    /// usage granted, so that mistake cannot be made.
    /// </para>
    /// <para>
    /// The names match the cloud's <c>id.usage</c> values,
    /// <c>non-marketing</c>, <c>standard</c> and <c>personalized</c>, and
    /// are the same in every 51Did package.
    /// </para>
    /// </summary>
    public enum Usage : byte
    {
        /// <summary>
        /// No usage bit is set. The cloud never issues such an identifier,
        /// so this is an identifier from somewhere else or a damaged one,
        /// and it should be treated as though it may not be passed on.
        /// </summary>
        None = 0,
        /// <summary>
        /// Created for use that is not marketing. Must not be passed to a
        /// demand source.
        /// </summary>
        NonMarketing = 1,
        /// <summary>
        /// Created for standard marketing, being targeting unrelated to
        /// the person's browsing history or interactions.
        /// </summary>
        Standard = 2,
        /// <summary>
        /// Created for personalized marketing, being targeting related to
        /// the person's browsing history or interactions.
        /// </summary>
        Personalized = 3,
    }
}
