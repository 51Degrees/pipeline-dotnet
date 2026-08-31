# FiftyOne.Did

Strongly-typed .NET parser for the 51Did (51Degrees Identifier)
returned by the 51Degrees Cloud service, and the client a server uses
to verify a 51Did and redeem its creator context result.

## Terminology

A 51Did is described at three levels, and the wording below is used
deliberately.

- The **51Did** (51Degrees Identifier) is the identifier as a whole,
  meaning the concept together with the rules for how it is issued,
  compared and licensed. "A 51Did" means the identifier in this complete
  sense, not any single field.
- The **envelope** (also called the **wrapper**) is the data model that
  carries a 51Did. It is a signed OWID holding the version, domain, date,
  payload and signature, and it changes byte-for-byte every time the cloud
  issues one, even for the same inputs, because the date and signature
  change with each call.
- The **value** is the part of the envelope that is stable and comparable.
  It is the payload bytes after Flags and LicenseId, a 32-byte SHA-256 for
  Probabilistic and HashedEmail identifiers, or a 16-byte GUID for Random.
  Two 51Dids for the same inputs share the same value even though their
  envelopes differ.

Comparing two 51Dids means comparing their values, never their envelopes.

## Payload layout

The header is shared by every identifier type. Bits 6-7 of Flags
select the type and the length of the value that follows.

| Offset | Length | Field      | Type                                            |
|-------:|-------:|------------|-------------------------------------------------|
|      0 |      1 | Flags      | uint8: bits 0-2 usage, bits 6-7 identifier type |
|      1 |      4 | LicenseId  | uint32 (little-endian)                          |
|      5 |  16/32 | Value      | SHA-256 (Probabilistic, HashedEmail) or GUID bytes (Random) |

| Bits 7-6 | `FodId.Type`    | Value length | Minimum payload |
|---------:|-----------------|-------------:|----------------:|
|     `00` | `Probabilistic` |           32 |              37 |
|     `01` | `Random`        |           16 |              21 |
|     `10` | `HashedEmail`   |           32 |              37 |
|     `11` | `Reserved`      |    remainder |               5 |

Identifiers issued before the type tag existed have bits 6-7 zeroed
and decode as `Probabilistic`.

The minimum payload is a lower bound and the only length rule this
package applies. A 51Did that carries a creator context, which binds
the identifier to the browser and connection it was created on, has a
section after the value, so its payload is longer than the minimum.
The lengths of that section belong to the cloud, and this package has
no upper bound of its own, so a longer payload is accepted and the
same three fields are exposed. An older reader therefore keeps working
when a newer context version ships. On such an identifier the four
LicenseId bytes hold an encrypted value that only 51Degrees can turn
back into a licence identifier, so `LicenseId` is the raw field value
and identifies nothing outside 51Degrees.

`FodId` inherits from `Owid.Client.Model.Owid` (see
[SWAN-community/owid-dotnet](https://github.com/SWAN-community/owid-dotnet)),
so a `FodId` instance behaves as an OWID for all OWID-level concerns
(domain, date, payload bytes, signature, base64 round-tripping) and
adds strongly-typed accessors for the three 51Did payload fields on
top. An OWID cannot be assembled by calling code, because an unsigned
one would be indistinguishable from a signed one downstream, so a
`FodId` reaches your code only by parsing bytes that were already a
complete, signed envelope.

## Parsing

A 51Did arrives from outside, from a page, a header or a query string,
so a value that is not a 51Did is an ordinary result and not a fault.
`FodId.TryParse` reads a value and says why when the value is not a
51Did, without throwing.

```csharp
using FiftyOne.Did.Model;

// Either base64 alphabet is accepted, the standard one with padding as
// the cloud issues it, or the URL-safe one without padding as a page
// puts it in a link.
if (FodId.TryParse(valueFromThePage, out var fodId, out var status) == false)
{
    // fodId is null and status says which of the expected problems it
    // was. Nothing has been thrown and no signature has been examined.
    return;
}

// fodId is a 51Did whose structure and payload rules are satisfied. Its
// signature has NOT been checked. See "Parsing is not verification".
```

A `byte[]` overload reads the raw envelope bytes in the same way.

Every result carries three facts, and they always agree. The return
value says whether the parse succeeded. The `out` value is the 51Did on
success and `null` on failure, never a partly built one. The status is
`Parsed` on success and the reason on failure.

`FodIdParseStatus` is the OWID vocabulary, carried through with the same
names and values, plus the two outcomes that belong to the 51Did
payload rules. A failure the OWID reader found is reported exactly as
the OWID reader named it.

| Status | Meaning |
|--------|---------|
| `Parsed` | A structurally valid 51Did whose payload meets the minimum for its type. Says nothing about the signature. |
| `MissingInput` | Nothing was supplied. Null, empty and whitespace-only values all report this. |
| `InvalidInputType` | The input arrived in a form the surface cannot read. |
| `InvalidBase64` | The string is not valid base64 in either alphabet, so there are no bytes. |
| `UnsupportedVersion` | The first byte names an OWID version the library does not know. |
| `UnexpectedEnd` | The data stopped in the middle of an envelope field. |
| `InvalidDomainEncoding` | The creator domain is not terminated, or is longer than the published maximum. |
| `ByteCountMismatch` | The declared payload count disagrees with the bytes present. |
| `ImplementationCapacityExceeded` | Structurally valid, but larger than this runtime can hold. |
| `MalformedEnvelope` | Malformed in a way none of the above describes. |
| `AbsentNode` | The OWID version 0 marker, which stands for an absent node and is never a 51Did. |
| `PayloadTooShort` | A valid OWID whose payload is shorter than the five byte 51Did header, so the type cannot be read. |
| `InvalidTypePayloadLength` | The header is present but the payload is shorter than the minimum for the type it names. |

The throwing surface, `new FodId(string)`, `FodId.FromBase64`,
`new FodId(byte[])` and the `As51Did()` extension, runs the same walk
and turns a failure into an exception, so the two never disagree about
an input. `ArgumentNullException` is thrown for a null value,
`ArgumentException` for an empty value or for a payload that breaks the
51Did rules (`PayloadTooShort` and `InvalidTypePayloadLength`), and
`FormatException` for a value that is not an envelope at all, whether
the base64 or the bytes under it. The message names the status. Use
the throwing form where a bad value is a programming error in your own
code, and `TryParse` where the value came from outside.

## Usage

```csharp
using FiftyOne.Did.Model;

// The throwing form, for a value you already trust to be a 51Did.
var fodId = FodId.FromBase64(base64FromCloudService);

byte    flags     = fodId.Flags;
IdType  type      = fodId.Type;        // Probabilistic / Random / HashedEmail
uint    licenseId = fodId.LicenseId;
byte[]  hash      = fodId.Hash;        // SHA-256 or GUID bytes, see Type

// Inherited OWID-level fields.
string   domain   = fodId.Domain;
DateTime date     = fodId.Date;
uint     minutes  = fodId.DateMinutes; // the date field as minutes since 2020

// Base64 in both alphabets.
string   roundTrip = fodId.AsBase64();    // standard, with padding
string   forUrls   = fodId.AsBase64Url(); // URL-safe, no padding
```

## Comparing two 51Dids

```csharp
var a = FodId.FromBase64(idprobglobalA);
var b = FodId.FromBase64(idprobglobalB);

// Envelope bytes (Domain, Date, Signature) ARE different. The
// envelope is not stable across reissues:
bool sameDate = a.Date == b.Date;                           // false
bool sameSig  = a.Signature.SequenceEqual(b.Signature);     // false

// The value inside the payload IS stable. This is what you
// actually compare:
bool sameValue = a.Hash.SequenceEqual(b.Hash);              // true
```

Use `FodId.Hash` as the cache / dedup key. The same value means the
same browser instance under the same usage policy on the same License
Key (for `idproblic`) or across all callers (for `idprobglobal`).

## Parsing is not verification

A parsed `FodId` is structurally a 51Did and nothing more. Whether the
signature is genuine is a separate question with its own answer, and
only two of the answers are about the signature itself. The rest say
the question could not be answered, which must never be read as a
forgery. From server code the supported way to ask is `DidClient`
below, which also picks the signing key in force at the identifier's
date. With a public key you already hold, the OWID library answers
directly on the instance.

```csharp
using Owid.Client;
using Owid.Client.Model;

var signature = fodId.SignatureStatus(publicKeyPem);
if (signature == OwidSignatureStatus.SignatureValid)
{
    // Genuine for this key.
}
else if (signature == OwidSignatureStatus.SignatureInvalid)
{
    // The only answer that means the identifier should be distrusted.
}
else
{
    // KeyUnavailable, InvalidKey, VerificationError and the rest. The
    // check could not be made, so nothing has been proved either way.
}
```

## Verifying on your server

`FiftyOne.Did.Client.DidClient` is the supported way to check a 51Did
from server code. Create one for the process and reuse it, because it
caches the signing public keys. The resource key is the page's key and
is public by nature. The licence key is server side only and is needed
to redeem where the account holds licence keys. The endpoint defaults
to `https://cloud.51degrees.com/api/v4/`, or to the `FOD_CLOUD_API_URL`
environment variable when that is set, which is the same variable the
cloud request engine honours, and a trailing slash is optional.

```csharp
using FiftyOne.Did.Client;
using FiftyOne.Did.Model;

using var client = new DidClient(resourceKey, licenceKey);
```

The four steps, in the order a server takes them.

1. **Parse.** The value a page sends arrives in the URL-safe alphabet,
   which both parse surfaces accept as they do the standard one. A
   value from a page is external input, so `TryParse` is the natural
   fit.

   ```csharp
   if (FodId.TryParse(valueFromThePage, out var fodId, out var status) == false)
   {
       // Answer the page with a 400 naming status.
   }
   ```

2. **Verify the signature offline.** The client fetches the signing
   public keys, keeps them, and picks the key in force when the
   identifier was created from the identifier's own date. The fetch is
   one use, and it is repeated only when the list is more than a day
   old or an identifier's date falls outside the keys held, so checking
   an identifier costs nothing in the normal case. A signature that
   does not match is `Invalid`. A key that could not be fetched is an
   `HttpRequestException` and a date no key covers is `NoKeyForDate`,
   because neither says anything about the signature.

   ```csharp
   bool genuine = await client.VerifySignatureAsync(fodId);
   // Or, to see why a check did not pass:
   SignatureCheck check = await client.VerifySignatureDetailedAsync(fodId);
   ```

3. **Verify the signature through the cloud.** The same answer from the
   cloud's verify endpoint, which needs no licence key and counts as one
   use. The string overload parses the value locally first, so a value
   that is not a 51Did raises `ArgumentException` naming the parse
   status before any call is made and costs nothing. A value the cloud
   refuses raises `ArgumentException` with the cloud's message.

   ```csharp
   bool genuine = await client.VerifyAsync(fodId);
   ```

4. **Redeem a sealed creator context result.** The page's call to
   `verify-full` (or `verify-context`) returns the verdict only inside
   an encrypted `result` the page cannot read or forge. The page hands
   that to your server, which redeems it with the licence key and reads
   the true verdict. The `challenge` is the single-use value your server
   gave the page for that verification, or null where none was.

   ```csharp
   RedeemResult redeemed = await client.RedeemAsync(fodId, result, challenge);
   if (redeemed.Signature == SignatureOutcome.Verified
       && redeemed.Context == ContextOutcome.Verified)
   {
       // Genuine, and presented from the browser and connection it was
       // created on.
   }
   else if (redeemed.Context == ContextOutcome.Mismatch)
   {
       // Genuine but moved. redeemed.Factors says which factor differed.
   }
   ```

   `Context` is one of `Verified`, `Mismatch`, `NoContext`,
   `NotCheckable`, `Expired`, `Replayed`, `Unreadable` or `Unconfirmed`,
   `Signature` is `Verified`, `Invalid` or `Unknown`, and `VerifiedAt`
   and `SecondsSinceVerified` say when the verification happened.
   `Unconfirmed` arrives with status 503 and may be retried. A host that
   does not offer the creator context answers 404, which raises
   `NotSupportedException`, and a 400 for a malformed identifier raises
   `ArgumentException` with the cloud's message. Every cryptographic
   failure is reported as the one word `Unreadable` by design, so the
   client does not try to tell them apart either.

The string overloads of `VerifyAsync` and `RedeemAsync` also refuse a
value longer than 4096 characters before parsing it. That figure is
the client's own policy, deliberately arbitrary and generous, so that
the client does no work for a value that cannot be an identifier. It
is not a property of the 51Did format, which has no upper bound in
this package, and it does not appear as a parse status.

The `verify-context` and `verify-full` calls are made from the browser,
not from this client, because the creator context describes the
browser's own connection and only that browser can present it. The
`Examples/Did/CreatorContextWeb` project in this repository shows the
whole flow, page and server together.

The OWID library this package builds on also has a `VerifyAsync()`
overload that takes no key and fetches one itself. That path asks the
envelope's domain for an OWID public-key endpoint without any
credential, which the 51Degrees cloud does not serve, so it cannot work
against the cloud. Use `DidClient` instead. The `Verify(ECDsa)`,
`VerifyAsync(ECDsa)` and `SignatureStatus` overloads, which take a key
you already hold, still work, and `Verify(ECDsa)` is the check
`DidClient` makes underneath with the key it picked.

## Migrating from the OWID constructors

Earlier versions of the OWID library let calling code build an OWID
from base64 or bytes with a constructor that threw on bad data, and
set its fields afterwards. Those constructors and setters are gone,
because an OWID that can be assembled by hand can exist unsigned.
Code that reached them through this package changes as follows.

Before:

```csharp
var owid = new Owid.Client.Model.Owid(base64);   // threw on bad data
var fodId = new FodId(owid);
```

After:

```csharp
if (FodId.TryParse(base64, out var fodId, out var status) == false)
{
    // status says why. Nothing was thrown.
}
// Or, where an exception is wanted:
var fodId2 = FodId.FromBase64(base64);
```

`new FodId(Owid)` remains for an OWID that came from the OWID library's
own `TryParse` or `Creator`. `Payload` and `Signature` now hand out
copies, so writing into the returned array cannot alter an envelope
whose signature covers the original bytes. Compare them by content.

## Non-goals

- **Signature verification on parse.** Parsing a `FodId` does not check
  the signature. Use `DidClient.VerifySignatureAsync` when needed.
- **Construction of new 51Dids.** This is a parser and a verifier. New
  51Dids are issued by the 51Degrees cloud `json` endpoint, through the
  cloud request engine and pipeline, or by the on-premise hashing
  engine.

## See also

- `https://github.com/SWAN-community/owid-dotnet`, OWID envelope
  library this package builds on.
- The [51Did inspector](https://51degrees.com/developers/51did-inspector?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=fiftyone.did-readme.md&utm_term=see-also)
  on `51degrees.com` for a visual breakdown of the same byte layout,
  with signature verification and a "Live 51d.es v3" sample.
- The [51Did comparer](https://51degrees.com/developers/51did-comparer?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=fiftyone.did-readme.md&utm_term=see-also)
  for a side-by-side, byte-by-byte comparison of two 51Dids that
  highlights the envelope-vs-value distinction in action.
