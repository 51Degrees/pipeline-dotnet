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

using FiftyOne.Pipeline.Engines.Data;
using System;

namespace FiftyOne.Pipeline.Translation.Data
{
    /// <summary>
    /// Defines a translation from one property to another.
    /// </summary>
    public class TranslationProperty
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public TranslationProperty(string source, string destination)
            : this(
                  source,
                  destination,
                  message => new AspectPropertyValue<string>()
                  {
                      NoValueMessage = message,
                  })
        {
        }

        private TranslationProperty(
            string source,
            string destination,
            Func<string, IAspectPropertyValue> createNoValuePlaceholder)
        {
            SourceProperty = source;
            DestinationProperty = destination;
            CreateNoValuePlaceholder = createNoValuePlaceholder;
        }

        /// <summary>
        /// Creates a translation whose no-value placeholder is typed for
        /// the readers of the destination property.
        /// </summary>
        /// <typeparam name="TDestination">
        /// The value type readers of the destination property expect,
        /// i.e. the T of the
        /// <see cref="IAspectPropertyValue{T}"/> they read it as. When the
        /// source property has no value, the placeholder stored against
        /// the destination is created with this type so typed reads of the
        /// destination do not fail with an invalid cast.
        /// </typeparam>
        /// <param name="source">
        /// Source property name on the source element data.
        /// </param>
        /// <param name="destination">
        /// Destination property name on translation engine data.
        /// </param>
        /// <returns>
        /// A new <see cref="TranslationProperty"/>.
        /// </returns>
        public static TranslationProperty Create<TDestination>(
            string source,
            string destination)
        {
            return new TranslationProperty(
                source,
                destination,
                message => new AspectPropertyValue<TDestination>()
                {
                    NoValueMessage = message,
                });
        }

        /// <summary>
        /// Source property name on the source element data.
        /// </summary>
        public string SourceProperty { get; set; }

        /// <summary>
        /// Destination property name on translation engine data.
        /// </summary>
        public string DestinationProperty { get; set; }

        /// <summary>
        /// Creates the no-value placeholder stored against the destination
        /// property when the source property has no value. The supplied
        /// string is the placeholder's no-value message.
        /// </summary>
        public Func<string, IAspectPropertyValue> CreateNoValuePlaceholder
        { get; }
    }
}
