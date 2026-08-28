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

The header is shared by every identifier type; bits 6-7 of Flags
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

A 51Did that carries a creator context, which binds the identifier to
the browser and connection it was created on, has a section after the
value, so its payload is longer than the minimum. The reader accepts
that and exposes the same three fields. On such an identifier the four
LicenseId bytes hold an encrypted value that only 51Degrees can turn
back into a licence identifier, so `LicenseId` is the raw field value
and identifies nothing outside 51Degrees.

The complete serialized envelope of a valid 51Did is at most 136 bytes.
`FodId.MaximumByteLength` exposes that boundary for callers that want to
check raw input before parsing; every `FodId` constructor also enforces it
and rejects a longer value for its length. This is a limit on the identifier
itself, not on an HTTP response that happens to carry one.

`FodId` inherits from `Owid.Client.Model.Owid` (see
[SWAN-community/owid-dotnet](https://github.com/SWAN-community/owid-dotnet)),
so a `FodId` instance behaves as an OWID for all OWID-level concerns
(domain, date, payload bytes, signature, base64 round-tripping) and
adds strongly-typed accessors for the three 51Did payload fields on
top.

## Usage

```csharp
using FiftyOne.Did.Model;

// Either base64 alphabet is accepted: the standard one with padding, as
// the cloud issues it, or the URL-safe one without padding, as a page
// puts it in a link.
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
   which `FromBase64` accepts as it does the standard one.

   ```csharp
   var fodId = FodId.FromBase64(valueFromThePage);
   ```

2. **Verify the signature offline.** The client fetches the signing
   public keys, keeps them, and picks the key in force when the
   identifier was created from the identifier's own date. The fetch is
   one use, and it is repeated only when the list is more than a day
   old or an identifier's date falls outside the keys held, so checking
   an identifier costs nothing in the normal case.

   ```csharp
   bool genuine = await client.VerifySignatureAsync(fodId);
   // Or, to see why a check did not pass:
   SignatureCheck check = await client.VerifySignatureDetailedAsync(fodId);
   ```

3. **Verify the signature through the cloud.** The same answer from the
   cloud's verify endpoint, which needs no licence key and counts as one
   use. A malformed value raises `ArgumentException` with the cloud's
   message.

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

The `verify-context` and `verify-full` calls are made from the browser,
not from this client, because the creator context describes the
browser's own connection and only that browser can present it. The
`Examples/Did/CreatorContextWeb` project in this repository shows the
whole flow, page and server together.

The OWID library this package builds on also has a `VerifyAsync()`
overload that takes no key and fetches one itself. That path asks the
envelope's domain for an OWID public-key endpoint without any
credential, which the 51Degrees cloud does not serve, so it cannot work
against the cloud. Use `DidClient` instead. The `Verify(ECDsa)` and
`VerifyAsync(ECDsa)` overloads, which take a key you already hold,
still work, and `Verify(ECDsa)` is the check `DidClient` makes
underneath with the key it picked.

## Non-goals

- **Signature verification on construction.** Constructing a `FodId`
  does not check the signature. Use `DidClient.VerifySignatureAsync`
  when needed.
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
