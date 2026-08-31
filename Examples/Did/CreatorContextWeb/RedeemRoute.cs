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

using FiftyOne.Did.Client;
using FiftyOne.Did.Model;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Examples.Did.CreatorContextWeb;

/// <summary>
/// What the /redeem route answers the page with: a status, a content
/// type and a body. Kept as plain values rather than an ASP.NET result so
/// the route's logic can be tested without a web host.
/// </summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="ContentType">The content type.</param>
/// <param name="Body">The body.</param>
public sealed record RedeemAnswer(int StatusCode, string ContentType, string Body);

/// <summary>
/// The server-side step of the creator context flow, and the part a
/// developer copies into their own server. It parses the 51Did the page
/// sent, checks the signature offline against the published keys, redeems
/// the encrypted result with the licence key, and answers the page in the
/// cloud's own shape plus one extra field, <c>serverSignature</c>, with
/// the offline outcome.
/// </summary>
public static class RedeemRoute
{
    private const string Json = "application/json";

    /// <summary>
    /// Handles one redeem request.
    /// </summary>
    /// <param name="client">The client holding the licence key.</param>
    /// <param name="did">
    /// The 51Did as the page sent it, in the URL-safe alphabet.
    /// </param>
    /// <param name="result">The encrypted result the page relayed.</param>
    /// <param name="challenge">The challenge the page was issued.</param>
    /// <returns>The answer for the page.</returns>
    public static async Task<RedeemAnswer> HandleAsync(
        DidClient client,
        string? did,
        string? result,
        string? challenge)
    {
        // 1. Parse. The page sends the URL-safe form, which FromBase64
        //    accepts. A value that is not a 51Did at all is the page's own
        //    mistake, so it is named plainly with a 400.
        FodId fodId;
        try
        {
            fodId = FodId.FromBase64(did ?? string.Empty);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return Errors(400,
                $"'{did}' is not a valid Base64-encoded 51Did: {e.Message}");
        }

        try
        {
            // 2. Verify the signature offline. The keys are fetched once and
            //    cached, so this costs no cloud call per request.
            var serverSignature = await client.VerifySignatureAsync(fodId)
                ? "verified"
                : "invalid";

            // 3. Redeem. The licence key is added here and only here, so the
            //    browser never sees it. The verdict the page shows
            //    (unconfirmed, replayed, expired) can arrive on a non-2xx
            //    status, which is passed on as received.
            var redeemed = await client.RedeemAsync(
                fodId, result ?? string.Empty, challenge);
            return new RedeemAnswer(
                redeemed.StatusCode, Json, ToJson(redeemed, serverSignature));
        }
        catch (ArgumentException e)
        {
            // The cloud refused the identifier as malformed.
            return Errors(400, e.Message);
        }
        catch (NotSupportedException e)
        {
            // The host does not offer the creator context. The page reads
            // a 404 as "not supported by this host".
            return new RedeemAnswer(404, "text/plain", e.Message);
        }
        catch (HttpRequestException e)
        {
            // No answer from the cloud, or an answer with an unexpected
            // status, which is passed on where there is one.
            var status = e.StatusCode is null ? 502 : (int)e.StatusCode;
            return new RedeemAnswer(status, Json, JsonSerializer.Serialize(
                new { error = $"redeem failed against {client.Endpoint}: {e.Message}" }));
        }
    }

    /// <summary>
    /// The cloud's own shape (<c>signature</c>, <c>context</c>,
    /// <c>factors</c> when present, <c>verifiedAt</c> and
    /// <c>secondsSinceVerified</c> when present) rebuilt from the typed
    /// result, plus <c>serverSignature</c>. The page ignores fields it
    /// does not know, so it needs no change.
    /// </summary>
    public static string ToJson(RedeemResult redeemed, string serverSignature)
    {
        var body = new Dictionary<string, object>();
        if (redeemed.Signature != SignatureOutcome.Unknown)
        {
            body["signature"] = redeemed.Signature == SignatureOutcome.Verified
                ? "verified"
                : "invalid";
        }
        body["context"] = RedeemResult.ToContextValue(redeemed.Context);
        if (redeemed.Factors is not null)
        {
            var factors = new Dictionary<string, string>();
            foreach (var factor in redeemed.Factors)
            {
                factors[factor.Key] = factor.Value == FactorOutcome.Verified
                    ? "verified"
                    : "mismatch";
            }
            body["factors"] = factors;
        }
        if (redeemed.VerifiedAt is DateTime verifiedAt)
        {
            body["verifiedAt"] = verifiedAt.ToString(
                RedeemResult.VerifiedAtFormat, CultureInfo.InvariantCulture);
        }
        if (redeemed.SecondsSinceVerified is int seconds)
        {
            body["secondsSinceVerified"] = seconds;
        }
        body["serverSignature"] = serverSignature;
        return JsonSerializer.Serialize(body);
    }

    private static RedeemAnswer Errors(int status, string message)
        => new RedeemAnswer(status, Json, JsonSerializer.Serialize(
            new { errors = new[] { message } }));
}
