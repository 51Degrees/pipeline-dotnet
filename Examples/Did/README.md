# 51Did creator context example

Every 51Did the cloud issues carries a creator context, which binds the
identifier to the browser and connection it was created on. The
project in this folder shows the flow that checks that context against
the cloud, with a two-step verification where `verify-full` returns an
encrypted result and your own server redeems it. The creator context
only makes sense from a browser, because the cloud compares the
connection presenting the identifier with the one that created it, so
the example is a web demo rather than a console program.

| Example | Description |
| --- | --- |
| `CreatorContextWeb` | A demo web server that serves `page.html` and redeems the encrypted result server side, running the flow the way production does. ASP.NET Core minimal API using `DidClient` from the `FiftyOne.Did` package in this repository. |

## The flow

1. **Create** a 51Did by calling the `json` endpoint, which issues an
   identifier for the calling connection.
2. **Verify** it with `verify-full`, which returns both the signature
   outcome and the creator context verdict only inside an encrypted
   `result` that the caller cannot read or forge.
3. **Redeem** the encrypted result with `redeem`, presenting the 51Did,
   the encrypted result and the account's licence key, and receive the
   true creator context verdict, when the verification happened
   (`verifiedAt`) and how long ago that was (`secondsSinceVerified`).

Step 2 runs in the visitor's browser (the page relays the encrypted
result to your server) and step 3 runs on your server, which is the
party holding the licence key. A verdict of mismatch or notcheckable
is expected mechanics rather than an error. So is `nocontext`, because
a self-hosted service can be configured not to emit the creator
context, and an identifier it issued then redeems as `nocontext`,
which the page shows the way it shows any verdict. A 404 from
`verify-full` or `redeem` means the host answering does not support
the creator context at all, and the page reports that
the feature is not supported by this host. The page also passes a
single-use `challenge`, binding the encrypted result to one
transaction.

## What to copy into your own server

The one server-side piece is the redeem call, which is where the
licence key is added, so the browser never sees it. The demo server
uses `DidClient` from the `FiftyOne.Did` package for it, one client for
the process, and the `/redeem` route in
`CreatorContextWeb/RedeemRoute.cs` does exactly this. Its essential
lines are:

```csharp
using var client = new DidClient(resource, licence);

// Per request. The page sends the 51Did in the URL-safe alphabet, which
// FromBase64 accepts.
var fodId = FodId.FromBase64(did);
var serverSignature = await client.VerifySignatureAsync(fodId)
    ? "verified" : "invalid";
var redeemed = await client.RedeemAsync(fodId, result, challenge);
```

`did`, `result` and `challenge` arrive from the page as the query
parameters `51did`, `result` and `challenge` (the identifier parameter
is named `51did` because the value is a 51Did and OWID is only the
envelope format, and a C# parameter cannot start with a digit, so the
route binds it with `[FromQuery(Name = "51did")] string did`), and
`resource` and `licence` are the account's resource key and licence
key. The client reads the cloud base from `FOD_CLOUD_API_URL` itself.
`VerifySignatureAsync` checks the signature offline against the
published signing keys, which the client fetches once and caches, and
`RedeemAsync` returns a typed `RedeemResult` with `Signature`,
`Context`, `Factors` when the context did not verify, `VerifiedAt` and
`SecondsSinceVerified`. The route answers the page with the cloud's
status and a JSON body in the cloud's own shape built from that result,
plus one extra field, `serverSignature`, carrying the offline outcome,
which the page ignores. A host without the feature makes `RedeemAsync`
throw `NotSupportedException`, which the route turns into a 404 with a
text body, and an unreachable cloud makes it throw
`HttpRequestException`, which becomes a 502 with an `error` field, so
the page reports both readably. A production server would also
remember the challenge it issued and reject a redemption carrying any
other.

## Environment variables

| Variable | Meaning |
| --- | --- |
| `_51DEGREES_RESOURCE_KEY` | The resource key, public by nature. Required. The legacy `RESOURCE_KEY` is read when it is not set. |
| `_51DEGREES_LICENSE_KEY` | A licence key of the same account. Server side only. Optional, because only an account that holds licence keys needs one to redeem, and an account holding none redeems without it. The legacy `LICENSE_KEY` is read when it is not set. |
| `FOD_CLOUD_API_URL` | Optional. The API base including the `/api/v4/` segment, defaulting to `https://cloud.51degrees.com/api/v4/`. This is the same variable the cloud request engine honours, so a developer who has set it once points every 51Degrees example at the same place. A host other than `cloud.51degrees.com` would be used to (a) use an on premise web server, or (b) use a privately hosted version of the 51Degrees cloud for performance reasons. This is the private hosting option of the cloud service. Both run the same service, so the demo works unchanged against either. |
| `PORT` | The port to listen on, defaults to `5100`. |

## How to run

From `CreatorContextWeb`:

```
dotnet run
```

then open `http://localhost:5100/`. To demonstrate across two devices,
serve on an address both can reach and open the copied link on the
second device.

## What a run costs

Every call the demo makes to `cloud.51degrees.com` is one use against
the subscription behind the resource key. Checking a 51Did from the
browser makes two, verify-full from the page and redeem from the
server, so a browser-based context check is two uses every time.
Checking only the signature with `verify` is one use. The server's
offline signature check costs nothing per identifier, because the
signing keys it uses are fetched once, one use, and then cached and
refreshed at most daily.

## The web demo, and the copy-and-paste proof

The demo page runs the flow the way production does. The browser
creates the 51Did and calls `verify-full`, the first verification
step, so the cloud observes the browser's live connection, then the
page hands the encrypted result to its own server, which redeems it
with the licence key as the second step. A fresh challenge is issued
per page load and bound through both steps by the cloud. A production
server would also remember the value it issued and reject a redemption
carrying any other, which this demo keeps out of scope.

The creation call requests every 51Did identifier in one request, and
the page shows all six in a table so anyone opening the demo sees the
full range: the probabilistic pair (`IdProbGlobal` and `IdProbLic`)
derived from the connection, the deterministic hashed-email pair
(`IdHemGlobal` and `IdHemLic`) derived from email evidence supplied as
`id.email` (the demo sends `demo@51did.example`, so the pair is the
same on every device that email appears on), and the random pair
(`IdRandGlobal` and `IdRandLic`). Global identifiers are shared across
customers, licensed ones are scoped to the licence key. The
verification and creator context flow then carries the licensed
probabilistic identifier through both steps, or the global one where
the account holds no licence keys.

Once the 51Did has fully validated, the page shows a **copy-and-paste
section** with a link carrying the same 51Did, and an explanation of
what will happen next. Open that link in a **different browser** and
the same page loads with the same identifier: the signature still
verifies and the identifier unpacks, because it is genuine, but the
creator context does **not** validate, because the context binds the
identifier to the browser and connection it was created on. That
visible failure is the demonstration that matters, a copied or stolen
identifier caught at presentation with nothing stored server side.
Opening the link in the same browser is not the demonstration, since
the same browser presents the same context and may still verify.

## The stylesheet

The vendored `examples-main.min.css` beside the demo is the design
system build and is refreshed by common-ci's `update-example-assets`
step.

## Running against a server without TLS capture

A service that does not terminate TLS itself still runs the whole flow.
The transport factor then uses a default that can never be produced by
a real TLS handshake, so creation and verification on the same server
work end to end, and the server logs one warning saying it is not
capturing TLS probabilistic values. In that setup a different browser
is still caught by the device factor rather than by transport. Against
the real cloud the transport factor participates fully.
