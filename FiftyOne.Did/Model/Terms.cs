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
    /// The terms document a 51Did was created under, carried in the byte
    /// after the match key so that the terms travel with the identifier
    /// rather than beside it. A receiver may only use an identifier
    /// created for marketing where it has accepted the terms that
    /// identifier was created under, and an identifier passed on its own,
    /// as a query string parameter for instance, arrives with nowhere to
    /// put a separate answer.
    /// <para>
    /// The byte is an index into a table in the specification and is not
    /// a version number, so that a later document can live at any address
    /// rather than only at one a number could be composed into. An index
    /// is never reused or repointed once published, because repointing
    /// one rewrites what an identifier already issued says it agreed to.
    /// The table is the whole of the definition, at
    /// https://github.com/51Degrees/specifications/blob/main/did-specification/identifier-layout.md#terms
    /// and every package has to be released to know a new row in it.
    /// </para>
    /// <para>
    /// The named value is deliberately not the index. Every one of the
    /// 256 byte values is a possible index, so no byte could stand for
    /// <see cref="Unknown"/>, which is why this is not a byte-backed
    /// enumeration as <see cref="Usage"/> and <see cref="IdType"/> are.
    /// Read <see cref="FodId.TermsIndex"/> for the index itself.
    /// </para>
    /// <para>
    /// The usage says where an identifier may go and the terms say which
    /// document it was created under, so they answer different questions
    /// and a receiver needs both. See <see cref="Usage"/>.
    /// </para>
    /// </summary>
    public enum Terms
    {
        /// <summary>
        /// The terms are not stated in the identifier. This is what an
        /// identifier issued before the byte existed reads as, and what a
        /// non-marketing identifier carries, because the Model Terms
        /// govern marketing and a non-marketing identifier is not created
        /// under them.
        /// <para>
        /// It does not mean the identifier is unrestricted. It means only
        /// that the identifier does not carry the answer, so the answer
        /// has to come from somewhere else, being the Terms Document
        /// Locator in an OpenRTB request or whatever the surrounding
        /// protocol provides. Where an identifier may go is a separate
        /// question answered by <see cref="Usage"/>, which still bars a
        /// non-marketing identifier from a demand source.
        /// </para>
        /// </summary>
        NotStated = 0,

        /// <summary>
        /// The Model Terms for Marketing, version 2, whose address
        /// <see cref="FodId.TermsUrl"/> gives.
        /// </summary>
        ModelTermsForMarketing2 = 1,

        /// <summary>
        /// An index added to the table after this package was released,
        /// so this package cannot name the document. It is not
        /// <see cref="NotStated"/>, because <see cref="NotStated"/> says
        /// no terms are stated whilst this says terms are stated that
        /// this package cannot name, and a receiver confusing the two
        /// would read an identifier created under terms as one created
        /// under none. A caller meeting this should treat the identifier
        /// as covered by terms it cannot yet read, and either take a
        /// newer package or refuse the identifier.
        /// <see cref="FodId.TermsIndex"/> says which index it met, so the
        /// document can be looked up by hand and named in a report.
        /// </summary>
        Unknown = -1,
    }
}
