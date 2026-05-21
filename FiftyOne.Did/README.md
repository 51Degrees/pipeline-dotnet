# FiftyOne.Did

Strongly-typed .NET parser for the 51Did (51Degrees device identifier) value
returned by the 51Degrees Cloud service.

A 51Did is an OWID envelope whose payload encodes three fields:

| Offset | Length | Field      | Type                              |
|-------:|-------:|------------|-----------------------------------|
|      0 |      1 | Flags      | uint8 usage-flags bit-mask        |
|      1 |      4 | LicenseId  | uint32 (little-endian)            |
|      5 |     32 | Hash       | SHA-256 probabilistic identifier  |

`FodId` inherits from `Owid.Client.Model.Owid` (see
[SWAN-community/owid-dotnet](https://github.com/SWAN-community/owid-dotnet)),
so a `FodId` instance behaves as an OWID for all OWID-level concerns
(domain, date, payload bytes, signature, base64 round-tripping) and adds
strongly-typed accessors for the three 51Did payload fields on top.

## Usage

```csharp
using FiftyOne.Did.Model;

var fodId = new FodId(base64FromCloudService);

byte    flags     = fodId.Flags;
uint    licenseId = fodId.LicenseId;
byte[]  hash      = fodId.Hash;        // 32 bytes

// Inherited OWID-level fields.
string   domain   = fodId.Domain;
DateTime date     = fodId.Date;

// Inherited OWID-level operations.
bool     verified = await fodId.VerifyAsync(publicKey);
string   roundTrip = fodId.AsBase64();
```

## Non-goals

- **Signature verification on construction.** Constructing a `FodId` does
  not check the signature. Call `VerifyAsync` (inherited from `Owid`) when
  needed.
- **Construction of new 51Dids.** This is a parser. New 51Dids are issued by
  the 51Degrees cloud / on-premise hashing engines.

## See also

- `https://github.com/SWAN-community/owid-dotnet` — OWID envelope library this
  package builds on.
- The 51Did inspector page on `51degrees.com/developers/fodid-inspector` for
  a visual breakdown of the same byte layout.
