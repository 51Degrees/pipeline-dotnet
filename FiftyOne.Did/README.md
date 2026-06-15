# FiftyOne.Did

Strongly-typed .NET parser for the 51Did (51Degrees device identifier)
returned by the 51Degrees Cloud service.

## Terminology

The 51Did has two layers. The wording below treats them as distinct.

- The **51Did** is the **identifier**. The whole base64 OWID envelope
  (version, domain, date, payload, signature). It changes byte-for-byte
  every time the cloud issues one, even for the same inputs, because
  the date and signature change with each call.
- The **probabilistic value** is one of the fields *inside* the payload
  (a 32-byte SHA-256 hash). It is stable across reissues for the same
  device + IP + usage: if two 51Dids were issued for the same inputs,
  their probabilistic values are equal even though the wrapping
  identifiers differ.

Comparing two browsers means comparing the probabilistic values
carried inside their identifiers, never the identifiers themselves.
Calling either layer "the identifier" without qualification leads to
incorrect comparisons; calling the inner field "the probabilistic
identifier" is the same conflation in a different costume.

## Payload layout

| Offset | Length | Field      | Type                                  |
|-------:|-------:|------------|---------------------------------------|
|      0 |      1 | Flags      | uint8 usage-flags bit-mask            |
|      1 |      4 | LicenseId  | uint32 (little-endian)                |
|      5 |     32 | Hash       | 32 bytes, SHA-256 probabilistic value |

`FodId` inherits from `Owid.Client.Model.Owid` (see
[SWAN-community/owid-dotnet](https://github.com/SWAN-community/owid-dotnet)),
so a `FodId` instance behaves as an OWID for all OWID-level concerns
(domain, date, payload bytes, signature, base64 round-tripping) and
adds strongly-typed accessors for the three 51Did payload fields on
top.

## Usage

```csharp
using FiftyOne.Did.Model;

var fodId = new FodId(base64FromCloudService);

byte    flags     = fodId.Flags;
uint    licenseId = fodId.LicenseId;
byte[]  hash      = fodId.Hash;        // 32-byte probabilistic value

// Inherited OWID-level fields.
string   domain   = fodId.Domain;
DateTime date     = fodId.Date;

// Inherited OWID-level operations.
bool     verified = await fodId.VerifyAsync(publicKey);
string   roundTrip = fodId.AsBase64();
```

## Comparing two 51Dids

```csharp
var a = new FodId(idprobglobalA);
var b = new FodId(idprobglobalB);

// Wrapper bytes (Domain, Date, Signature) ARE different; the
// identifier itself is not stable across reissues:
bool sameDate = a.Date == b.Date;                           // false
bool sameSig  = a.Signature.SequenceEqual(b.Signature);     // false

// The probabilistic value inside the payload IS stable; this is
// what you actually compare:
bool sameValue = a.Hash.SequenceEqual(b.Hash);              // true
```

Use `FodId.Hash` as the cache / dedup key. The same value means the
same browser instance under the same usage policy on the same License
Key (for `idproblic`) or across all callers (for `idprobglobal`).

## Non-goals

- **Signature verification on construction.** Constructing a `FodId`
  does not check the signature. Call `VerifyAsync` (inherited from
  `Owid`) when needed.
- **Construction of new 51Dids.** This is a parser. New 51Dids are
  issued by the 51Degrees cloud / on-premise hashing engines.

## See also

- `https://github.com/SWAN-community/owid-dotnet`, OWID envelope
  library this package builds on.
- The [51Did inspector](https://51degrees.com/developers/51did-inspector?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=fiftyone.did-readme.md&utm_term=see-also)
  on `51degrees.com` for a visual breakdown of the same byte layout,
  with signature verification and a "Live 51d.es v3" sample.
- The [51Did comparer](https://51degrees.com/developers/51did-comparer?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=fiftyone.did-readme.md&utm_term=see-also)
  for a side-by-side, byte-by-byte comparison of two 51Dids that
  highlights the wrapper-vs-value distinction in action.
