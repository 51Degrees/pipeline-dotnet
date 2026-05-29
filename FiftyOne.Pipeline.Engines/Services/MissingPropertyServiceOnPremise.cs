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

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// On-premise missing-property service. Inherits all behaviour from
    /// <see cref="MissingPropertyService"/> unchanged — on-premise engines
    /// do not depend on the per-request cloud state handled by
    /// <c>MissingPropertyServiceCloud</c>, so the base heuristics already
    /// report the correct reason.
    /// </summary>
    /// <remarks>
    /// This type exists so that callers make a deliberate cloud-vs-on-premise
    /// choice: cloud aspect-engine builders should wire
    /// <c>MissingPropertyServiceCloud</c>, on-premise builders should wire
    /// this service. The deprecated <see cref="MissingPropertyService.Instance"/>
    /// singleton remains only to surface that choice.
    /// </remarks>
    public class MissingPropertyServiceOnPremise : MissingPropertyService
    {
        private static IMissingPropertyService _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton instance of the on-premise missing-property service.
        /// </summary>
        public static new IMissingPropertyService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MissingPropertyServiceOnPremise();
                        }
                    }
                }
                return _instance;
            }
        }

        private MissingPropertyServiceOnPremise() { }
    }
}
