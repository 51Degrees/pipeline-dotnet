/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2022 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.Web.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace FiftyOne.Pipeline.Web.Framework
{
    /// <summary>
    /// Implementation of the <see cref="IAspSession"/> interface giving
    /// access to an ASP.NET Framework session (instance of
    /// <see cref="HttpSessionStateBase"/>).
    /// </summary>
    internal class AspFrameworkSession : IAspSession
    {
        private HttpSessionStateBase _session;

        internal AspFrameworkSession(HttpSessionStateBase session)
        {
            _session = session;
        }

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var key in _session.Keys)
                {
                    yield return key.ToString();
                }
            }
        }

        public void SetString(string key, string value)
        {
            _session.Add(key, value);
        }

        public string GetString(string key)
        {
            if (Keys.Contains(key))
            {
                return _session[key].ToString();
            }
            return null;
        }
    }
}
