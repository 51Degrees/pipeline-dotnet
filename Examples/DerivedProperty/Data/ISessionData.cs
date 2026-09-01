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

using FiftyOne.Pipeline.Core.Data;

namespace Examples.DerivedProperty.Data
{
    /// <summary>
    /// The properties the example's own source element publishes under the
    /// element data key session. A real pipeline reads properties like
    /// these from device detection or from IP intelligence, and the
    /// derived property element does not care which element produced them.
    ///
    /// Each property is null where the request carried no evidence for it,
    /// which is how the example shows a source property being absent.
    /// </summary>
    public interface ISessionData : IElementData
    {
        /// <summary>
        /// How many pages the visitor has looked at.
        /// </summary>
        int? PagesViewed { get; }

        /// <summary>
        /// How long the page has been open, in seconds.
        /// </summary>
        int? SecondsSincePageLoad { get; }

        /// <summary>
        /// Whether the pointer has moved over the page. A real pipeline
        /// would learn this from client side JavaScript, so the value is
        /// often missing on the first request of a visit.
        /// </summary>
        bool? PointerMoved { get; }
    }
}
