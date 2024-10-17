/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Web;
using System.Web.Configuration;

namespace FiftyOne.Pipeline.Web.Framework.Providers
{
    /// <summary>
    /// Extends the default HttpBrowserCapabilities implementation to override
    /// and add values using the result of the 51Degrees Pipeline's processing.
    /// </summary>
    public class PipelineCapabilities : HttpBrowserCapabilities
    {
        /// <summary>
        /// The results from <see cref="IPipeline"/> processing.
        /// </summary>
        public IFlowData FlowData { get; private set; }
        private readonly HttpRequest _request;
        private readonly HttpBrowserCapabilities _baseCaps;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>> _availableElementProperties;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseCaps">
        /// The parent <see cref="HttpBrowserCapabilities"/> instance to
        /// create this instance from.
        /// </param>
        /// <param name="request">
        /// The current <see cref="HttpRequest"/>
        /// </param>
        /// <param name="flowData">
        /// The results from the <see cref="IPipeline"/> for the current
        /// request
        /// </param>
        public PipelineCapabilities(
            HttpBrowserCapabilities baseCaps,
            HttpRequest request,
            IFlowData flowData) : base()
        {
            _baseCaps = baseCaps;
            _request = request;
            FlowData = flowData;
            _availableElementProperties 
                = (flowData.Errors != null && flowData.Errors.Count > 0)
                ? ImmutableDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>>.Empty
                : flowData.Pipeline.ElementAvailableProperties;
        }

        private string IterateDown(string[] keys, IDictionary<string, object> dict)
        {
            string[] subKeys = keys.Skip(1).ToArray();
            var nonStringValue = dict.ContainsKey(subKeys[0]) ? dict[subKeys[0]] : null;
            if (nonStringValue == null || nonStringValue is string)
            {
                return nonStringValue as string;
            }
            if (subKeys.Length > 1 && nonStringValue as IDictionary<string, object> != null)
            {
                return IterateDown(subKeys, nonStringValue as IDictionary<string, object>);
            }
            else if (nonStringValue as IDictionary<string, object> != null ||
                nonStringValue as IList<object> != null)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(nonStringValue);
            }
            else
            {
                return nonStringValue.ToString();
            }
        }

        /// <summary>
        /// Gets the value of the browser capability, first using the processed
        /// FlowData, then the base capabilities if the property is not present.
        /// </summary>
        /// <param name="key">
        /// The name of the browser capability to retrieve.
        /// </param>
        /// <returns>
        /// The browser capability, or element property with the specified key
        /// name.
        /// </returns>
        public override string this[string key]
        {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps[key];
                }
                string value = null;
                // Try getting the value from the FlowData
                try
                {
                    if (key != null && key.Contains("."))
                    {
                        var keys = key.Split('.');
                        if (_availableElementProperties.ContainsKey(keys[0]) &&
                            _availableElementProperties[keys[0]].ContainsKey(keys[1]) &&
                            _availableElementProperties[keys[0]][keys[1]].Available)
                        {
                            var nonStringValue = FlowData.Get(keys[0])[keys[1]];
                            if (nonStringValue as string != null)
                            {
                                value = nonStringValue as string;
                            }
                            else if (nonStringValue as IDictionary<string, object> != null &&
                                keys.Length > 2)
                            {
                                value = IterateDown(keys.Skip(1).ToArray(), nonStringValue as IDictionary<string, object>);
                            }
                            else if (nonStringValue as IDictionary<string, object> != null ||
                                nonStringValue as IList<object> != null ){
                                value = Newtonsoft.Json.JsonConvert.SerializeObject(nonStringValue);
                            }
                            else
                            {
                                value = nonStringValue.ToString();
                            }
                        }
                    }
                    else if (_availableElementProperties.Any(i => i.Value.ContainsKey(key)))
                    {
                        var elementKey = _availableElementProperties
                            .First(i => i.Value.ContainsKey(key))
                            .Key;
                        var valueObj = FlowData.Get(elementKey)[key];

                        if (_availableElementProperties[elementKey][key].Type.Equals(typeof(IReadOnlyList<string>)))
                        {
                            value = string.Join("|", ((IEnumerable<object>)valueObj).Select(i => i.ToString()));
                        }
                        else
                        {
                            value = valueObj.ToString();
                        }
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Regardless of the error that occurs, we want to handle
                // it in the same way.
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // There was an exception, ensure the value is null so it
                    // will be retrieved from the base class.
                    value = null;
                }

                // Return the value that was just fetched, or fetch it from the
                // base class if it was not found.
                return value == null ? _baseCaps[key] : value;
            }
        }

        /// <summary>
        /// Returns true if the current request was made from a mobile device.
        /// </summary>
        public override bool IsMobileDevice
        {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps.IsMobileDevice;
                }
                if (_availableElementProperties.ContainsKey("device") &&
                    _availableElementProperties["device"].ContainsKey("ismobile") &&
                    FlowData.GetAs<AspectPropertyValue<bool>>("ismobile").HasValue)
                {
                    return FlowData.GetAs<AspectPropertyValue<bool>>("ismobile").Value;
                }
                else
                {
                    return _baseCaps.IsMobileDevice;
                }
            }
        }

        /// <summary>
        /// Returns true if the current request was made from a device
        /// that has voice call capability.
        /// </summary>
        public override bool CanInitiateVoiceCall
        {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps.CanInitiateVoiceCall;
                }
                if (_availableElementProperties.ContainsKey("device") &&
                    _availableElementProperties["device"].ContainsKey("supportsphonecalls") &&
                    FlowData.GetAs<AspectPropertyValue<bool>>("supportsphonecalls").HasValue)
                {
                    return FlowData.GetAs<AspectPropertyValue<bool>>("supportsphonecalls").Value;
                }
                else
                {
                    return _baseCaps.CanInitiateVoiceCall;
                }
            }
        }

        /// <summary>
        /// Returns the name of manufacturer of the device that made the request.
        /// </summary>
        public override string MobileDeviceManufacturer
        {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps.MobileDeviceManufacturer;
                }
                if (_availableElementProperties.ContainsKey("device") &&
                    _availableElementProperties["device"].ContainsKey("oem") &&
                    FlowData.GetAs<AspectPropertyValue<string>>("oem").HasValue)
                {
                    return FlowData.GetAs<AspectPropertyValue<string>>("oem").Value;
                }
                else
                {
                    return _baseCaps.MobileDeviceManufacturer;
                }
            }
        }

        /// <summary>
        /// Returns the name of model name of the device that made the request.
        /// </summary>
        public override string MobileDeviceModel
        {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps.MobileDeviceModel;
                }
                if (_availableElementProperties.ContainsKey("device") &&
                    _availableElementProperties["device"].ContainsKey("hardwaremodel") &&
                    FlowData.GetAs<AspectPropertyValue<string>>("hardwaremodel").HasValue)
                {
                    return FlowData.GetAs<AspectPropertyValue<string>>("hardwaremodel").Value;
                }
                else
                {
                    return _baseCaps.MobileDeviceModel;
                }
            }
        }

        /// <summary>
        /// Returns the screen height in pixels of the device that 
        /// made the request.
        /// </summary>
        public override int ScreenPixelsHeight {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps.ScreenPixelsHeight;
                }
                if (_availableElementProperties.ContainsKey("device") &&
                    _availableElementProperties["device"].ContainsKey("screenpixelsheight") &&
                    FlowData.GetAs<AspectPropertyValue<int>>("screenpixelsheight").HasValue)
                {
                    return FlowData.GetAs<AspectPropertyValue<int>>("screenpixelsheight").Value;
                }
                else
                {
                    return _baseCaps.ScreenPixelsHeight;
                }
            }
        }

        /// <summary>
        /// Returns the screen width in pixels of the device that 
        /// made the request.
        /// </summary>
        public override int ScreenPixelsWidth
        {
            get
            {
                if (FlowData.Errors != null && FlowData.Errors.Count > 0)
                {
                    return _baseCaps.ScreenPixelsWidth;
                }
                if (_availableElementProperties.ContainsKey("device") &&
                    _availableElementProperties["device"].ContainsKey("screenpixelswidth") &&
                    FlowData.GetAs<AspectPropertyValue<int>>("screenpixelswidth").HasValue)
                {
                    return FlowData.GetAs<AspectPropertyValue<int>>("screenpixelswidth").Value;
                }
                else
                {
                    return _baseCaps.ScreenPixelsWidth;
                }
            }
        }

        #region Unaltered Properties
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int DefaultSubmitButtonLimit { get { return _baseCaps.DefaultSubmitButtonLimit; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanCombineFormsInDeck { get { return _baseCaps.CanCombineFormsInDeck; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderAfterInputOrSelectElement { get { return _baseCaps.CanRenderAfterInputOrSelectElement; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderEmptySelects { get { return _baseCaps.CanRenderEmptySelects; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderInputAndSelectElementsTogether { get { return _baseCaps.CanRenderInputAndSelectElementsTogether; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderMixedSelects { get { return _baseCaps.CanRenderMixedSelects; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderOneventAndPrevElementsTogether { get { return _baseCaps.CanRenderOneventAndPrevElementsTogether; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderPostBackCards { get { return _baseCaps.CanRenderPostBackCards; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanRenderSetvarZeroWithMultiSelectionList { get { return _baseCaps.CanRenderSetvarZeroWithMultiSelectionList; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool CanSendMail { get { return _baseCaps.CanSendMail; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int GatewayMajorVersion { get { return _baseCaps.GatewayMajorVersion; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override double GatewayMinorVersion { get { return _baseCaps.GatewayMinorVersion; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string GatewayVersion { get { return _baseCaps.GatewayVersion; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool HasBackButton { get { return _baseCaps.HasBackButton; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool HidesRightAlignedMultiselectScrollbars { get { return _baseCaps.HidesRightAlignedMultiselectScrollbars; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string InputType { get { return _baseCaps.InputType; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int MaximumHrefLength { get { return _baseCaps.MaximumHrefLength; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int MaximumRenderedPageSize { get { return _baseCaps.MaximumRenderedPageSize; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int MaximumSoftkeyLabelLength { get { return _baseCaps.MaximumSoftkeyLabelLength; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int NumberOfSoftkeys { get { return _baseCaps.NumberOfSoftkeys; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string PreferredRenderingMime { get { return _baseCaps.PreferredRenderingMime; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string PreferredRenderingType { get { return _baseCaps.PreferredRenderingType; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string PreferredRequestEncoding { get { return _baseCaps.PreferredRequestEncoding; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string PreferredResponseEncoding { get { return _baseCaps.PreferredResponseEncoding; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RendersBreakBeforeWmlSelectAndInput { get { return _baseCaps.RendersBreakBeforeWmlSelectAndInput; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RendersBreaksAfterHtmlLists { get { return _baseCaps.RendersBreaksAfterHtmlLists; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RendersBreaksAfterWmlAnchor { get { return _baseCaps.RendersBreaksAfterWmlAnchor; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RendersBreaksAfterWmlInput { get { return _baseCaps.RendersBreaksAfterWmlInput; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RendersWmlDoAcceptsInline { get { return _baseCaps.RendersWmlDoAcceptsInline; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RendersWmlSelectsAsMenuCards { get { return _baseCaps.RendersWmlSelectsAsMenuCards; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override string RequiredMetaTagNameValue { get { return _baseCaps.RequiredMetaTagNameValue; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresAttributeColonSubstitution { get { return _baseCaps.RequiresAttributeColonSubstitution; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresContentTypeMetaTag { get { return _baseCaps.RequiresContentTypeMetaTag; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresDBCSCharacter { get { return _baseCaps.RequiresDBCSCharacter; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresHtmlAdaptiveErrorReporting { get { return _baseCaps.RequiresHtmlAdaptiveErrorReporting; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresLeadingPageBreak { get { return _baseCaps.RequiresLeadingPageBreak; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresNoBreakInFormatting { get { return _baseCaps.RequiresNoBreakInFormatting; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresOutputOptimization { get { return _baseCaps.RequiresOutputOptimization; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresPhoneNumbersAsPlainText { get { return _baseCaps.RequiresPhoneNumbersAsPlainText; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresSpecialViewStateEncoding { get { return _baseCaps.RequiresSpecialViewStateEncoding; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresUniqueFilePathSuffix { get { return _baseCaps.RequiresUniqueFilePathSuffix; } }
        /// <summary>
        /// </summary>
        public override bool RequiresUniqueHtmlCheckboxNames { get { return _baseCaps.RequiresUniqueHtmlCheckboxNames; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresUniqueHtmlInputNames { get { return _baseCaps.RequiresUniqueHtmlInputNames; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool RequiresUrlEncodedPostfieldValues { get { return _baseCaps.RequiresUrlEncodedPostfieldValues; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int ScreenCharactersHeight { get { return _baseCaps.ScreenCharactersHeight; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override int ScreenCharactersWidth { get { return _baseCaps.ScreenCharactersWidth; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsAccesskeyAttribute { get { return _baseCaps.SupportsAccesskeyAttribute; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsBodyColor { get { return _baseCaps.SupportsBodyColor; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsBold { get { return _baseCaps.SupportsBold; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsCacheControlMetaTag { get { return _baseCaps.SupportsCacheControlMetaTag; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsCss { get { return _baseCaps.SupportsCss; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsDivAlign { get { return _baseCaps.SupportsDivAlign; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsDivNoWrap { get { return _baseCaps.SupportsDivNoWrap; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsEmptyStringInCookieValue { get { return _baseCaps.SupportsEmptyStringInCookieValue; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsFontColor { get { return _baseCaps.SupportsFontColor; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsFontName { get { return _baseCaps.SupportsFontName; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsFontSize { get { return _baseCaps.SupportsFontSize; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsImageSubmit { get { return _baseCaps.SupportsImageSubmit; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsIModeSymbols { get { return _baseCaps.SupportsIModeSymbols; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsInputIStyle { get { return _baseCaps.SupportsInputIStyle; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsInputMode { get { return _baseCaps.SupportsInputMode; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsItalic { get { return _baseCaps.SupportsItalic; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsJPhoneMultiMediaAttributes { get { return _baseCaps.SupportsJPhoneMultiMediaAttributes; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsJPhoneSymbols { get { return _baseCaps.SupportsJPhoneSymbols; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsQueryStringInFormAction { get { return _baseCaps.SupportsQueryStringInFormAction; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsRedirectWithCookie { get { return _baseCaps.SupportsRedirectWithCookie; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsSelectMultiple { get { return _baseCaps.SupportsSelectMultiple; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsUncheck { get { return _baseCaps.SupportsUncheck; } }
        /// <summary>
        /// The unaltered implementation from <see cref="HttpCapabilitiesDefaultProvider"/>
        /// </summary>
        public override bool SupportsXmlHttp { get { return _baseCaps.SupportsXmlHttp; } }

        #endregion
    }
}
