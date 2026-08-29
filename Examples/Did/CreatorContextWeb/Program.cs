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

// 51Did creator context demo server. ASP.NET Core minimal API using the
// FiftyOne.Did package's DidClient for the server side. Run:
//   dotnet run    then open http://localhost:5100/
//
// Every 51Did the cloud issues carries a creator context, which binds the
// identifier to the browser and connection it was created on. This demo
// runs the full flow the way production does, in three steps.
//
// 1. Create. The browser calls the json endpoint, which issues a 51Did
//    for the browser's connection.
// 2. Verify. The browser calls verify-full, which returns both the
//    signature outcome and the creator context verdict only inside an
//    encrypted result that the browser cannot read or forge. The cloud
//    observes the browser's live connection in this step.
// 3. Redeem. The page hands the encrypted result to this server, which
//    parses the 51Did, checks its signature offline against the published
//    keys, then calls redeem with the 51Did, the encrypted result and the
//    account's licence key, and receives the true creator context verdict,
//    when the verification happened (verifiedAt) and how long ago that
//    was (secondsSinceVerified). This server is the only party holding
//    the licence key, which the browser never sees. See RedeemRoute.cs.
//
// This server serves page.html with a fresh challenge per load, which the
// cloud binds through both steps, and redeems the encrypted result server
// side. A production server would also remember the challenge it issued
// and reject a redemption carrying any other, which this demo keeps out
// of scope.
//
// What a run costs. Every call to cloud.51degrees.com is one use against
// the subscription behind the resource key. A browser-based context check
// makes two calls, verify-full from the page and redeem from this server,
// so two uses every time, plus one use for the creation call. The
// signing keys the offline check uses are fetched once, one use, and
// then cached and refreshed at most daily.
//
// Environment variables:
//   _51DEGREES_RESOURCE_KEY  the resource key, required (the legacy
//                            RESOURCE_KEY is read when it is not set)
//   _51DEGREES_LICENSE_KEY   a licence key of the same account, optional
//                            (the legacy LICENSE_KEY is read when it is
//                            not set)
//   FOD_CLOUD_API_URL        the API base including the /api/v4/ segment,
//                            defaults to https://cloud.51degrees.com/api/v4/
//                            and is the same variable the cloud request
//                            engine and DidClient honour
//   PORT                     the port to listen on, defaults to 5100
//
// See ../README.md for the flow and the copy-and-paste proof.
using System.Security.Cryptography;
using Examples.Did.CreatorContextWeb;
using FiftyOne.Did.Client;
using Microsoft.AspNetCore.Mvc;

// The aligned _51DEGREES_RESOURCE_KEY environment variable is checked
// first, then the legacy RESOURCE_KEY variable.
var resource = Environment.GetEnvironmentVariable("_51DEGREES_RESOURCE_KEY");
if (string.IsNullOrEmpty(resource))
{
    resource = Environment.GetEnvironmentVariable("RESOURCE_KEY");
}
if (string.IsNullOrEmpty(resource))
{
    Console.Error.WriteLine(
        "Set _51DEGREES_RESOURCE_KEY (or the legacy RESOURCE_KEY) to the "
        + "resource key of the page.");
    return 1;
}
var licence = Environment.GetEnvironmentVariable("_51DEGREES_LICENSE_KEY");
if (string.IsNullOrEmpty(licence))
{
    licence = Environment.GetEnvironmentVariable("LICENSE_KEY");
}
var port = Environment.GetEnvironmentVariable("PORT") ?? "5100";
if (string.IsNullOrEmpty(licence))
{
    // Only an account that holds licence keys needs one to redeem,
    // because the licence key is what keeps redemption to the acting
    // party's own servers. An account holding none has nothing to check
    // against, so the demo runs without it. Saying so here means an
    // account that DOES hold licence keys, run without one, is diagnosed
    // at start-up rather than by an unreadable verdict three steps later
    // that looks like a cryptographic failure.
    Console.WriteLine(
        "No _51DEGREES_LICENSE_KEY set. Redemption will work where the "
        + "account holds no licence keys, and will report the context "
        + "unreadable where it holds some.");
}

// One client for the process. It reads FOD_CLOUD_API_URL itself and
// normalises the base to end in one slash, and the page is given the same
// base so every URL, here and in the browser, is base plus its path. A
// host other than cloud.51degrees.com would be used to (a) use an on
// premise web server, or (b) use a privately hosted version of the
// 51Degrees cloud for performance reasons. This is the private hosting
// option of the cloud service. Both run the same service, so this demo
// works unchanged against either.
using var client = new DidClient(resource, licence);
var api = client.Endpoint;

// Both are read PER REQUEST, not once at start-up. A demo left running
// while its page is edited would otherwise keep serving the version it
// started with, which looks exactly like an edit that did not work. The
// cost is one small file read per request, which is nothing at demo
// scale. The stylesheet is the design system build, vendored beside this
// server exactly as the other 51Degrees web examples vendor it.
string ReadPage() => File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "page.html"));
byte[] ReadCss() => File.ReadAllBytes(
    Path.Combine(AppContext.BaseDirectory, "examples-main.min.css"));

var builder = WebApplication.CreateBuilder(args);
// The wildcard binding lets a second device open the copied link, which
// is the demonstration that matters.
builder.WebHost.UseUrls($"http://*:{port}");
var app = builder.Build();

app.MapGet("/", () =>
    Results.Content(
        ReadPage().Replace("__RESOURCE__", resource)
            .Replace("__CHALLENGE__", Convert.ToHexString(
                RandomNumberGenerator.GetBytes(16)))
            .Replace("__API__", api),
        "text/html; charset=utf-8"));

app.MapGet("/examples-main.min.css", () =>
    Results.Bytes(ReadCss(), "text/css"));

// The identifier parameter is named 51did, because the value is a 51Did
// and OWID is only the envelope format. A C# parameter cannot start with
// a digit, so the query name is bound explicitly. The work is in
// RedeemRoute.HandleAsync, which is the part to copy into your own
// server.
app.MapGet("/redeem", async (
    [FromQuery(Name = "51did")] string did,
    string result,
    string? challenge) =>
{
    var answer = await RedeemRoute.HandleAsync(client, did, result, challenge);
    return Results.Content(
        answer.Body, answer.ContentType, statusCode: answer.StatusCode);
});

Console.WriteLine($"51Did demo on http://localhost:{port}/");
app.Run();
return 0;
