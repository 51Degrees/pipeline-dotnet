# 51Degrees Pipeline API

![51Degrees](https://raw.githubusercontent.com/51Degrees/common-ci/main/images/logo/360x67.png "Data rewards the curious") **Pipeline API**

[Developer Documentation](https://51degrees.com/pipeline-dotnet/index.html?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=readme.md&utm_term=top "developer documentation")
| NuGet | Package | NuGet | Package |
| --- | --- | --- | --- |
| [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.CloudRequestEngine.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.CloudRequestEngine) | CloudRequestEngine | [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Examples.Shared.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Examples.Shared) | Examples.Shared |
| [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Core.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Core) | Core | [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.JavaScriptBuilder.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.JavaScriptBuilder) | JavaScriptBuilder |
| [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Engines.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Engines) | Engines | [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.JsonBuilder.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.JsonBuilder) | JsonBuilder |
| [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Engines.FiftyOne.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Engines.FiftyOne) | Engines.FiftyOne | [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Web.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Web) | Web |
| [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Engines.TestHelpers.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Engines.TestHelpers) | Engines.TestHelpers | [![NuGet](https://img.shields.io/nuget/v/FiftyOne.Pipeline.Web.Shared.svg)](https://www.nuget.org/packages/FiftyOne.Pipeline.Web.Shared) | Web.Shared |

## Introduction

This repository contains all the projects required to build the .NET implementation of the Pipeline API.
Individual engines (For example, device detection) are in separate repositories.

The [specification](https://github.com/51Degrees/specifications/blob/main/pipeline-specification/README.md)
is also available on GitHub and is recommended reading if you wish to understand
the concepts and design of this API.

## Dependencies

Visual Studio 2022 or later is recommended. Although Visual Studio Code can be used for working with most of the projects.

The Pipeline projects are written in C# and target .NET Standard 2.0.3
The Web integration multi-targets the following:
    - .NET Core 3.1
    - .NET Core 8.0
    - .NET Framework 4.6.2

The [tested versions](https://51degrees.com/documentation/_info__tested_versions.html?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=readme.md&utm_term=dependencies) page shows the .NET versions that we currently test against. The software may run fine against other versions, but additional caution should be applied.

## Strong naming

All NuGet packages published from this repository are strong-name signed so they can be referenced by strong-named consumer applications (notably .NET Framework hosts that enforce strong-name identity at assembly load).

### How signing is wired

The same `51Degrees.publickey` file is committed at the root of each shipping repo. Signing is configured once via [`Directory.Build.props`](Directory.Build.props) and applies to every project:

- **Local developer builds** use the [PublicSign](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/security#publicsign) pattern — the compiler stamps the public key into each assembly so it is identified as strong-named, but no cryptographic signature is produced. Only `51Degrees.publickey` (committed) is needed, so contributors can build and test without any secrets.
- **Production builds (CI only)** receive the matching private `.snk` from a GitHub organization secret, so the assemblies in the published NuGet packages carry a real cryptographic signature.

The same public key token appears on every `FiftyOne.*` DLL shipped from this repo and its upstream library repos (`caching-dotnet`, `common-dotnet`), so the dependency chain remains consistent under strong-name identity checks.

### Why `vendor/Stubble/` exists

Strong-named assemblies can only reference other strong-named assemblies. `FiftyOne.Pipeline.JavaScriptBuilder` depends on [Stubble.Core](https://github.com/StubbleOrg/Stubble), and the upstream NuGet package is not strong-named. A request to publish a strong-named build has been open in that project since December 2019 — see [StubbleOrg/Stubble#85](https://github.com/StubbleOrg/Stubble/issues/85) — with no progress in over five years.

Rather than wait, the Stubble source is vendored under [`vendor/Stubble/`](vendor/Stubble/) and built from source as a project reference. It inherits the strong-naming configuration from the repo root and ships strong-signed alongside our own assemblies. Stubble is MIT-licensed and the upstream copyright and licence notices are preserved unchanged in the vendored tree.

## Solutions and projects

- **FiftyOne.Pipeline** - The core projects that comprise the Pipeline API.
  - *FiftyOne.Pipeline.Core* - The core Pipeline classes such as Pipeline, FlowData, FlowElement and Evidence.
  - *FiftyOne.Pipeline.Engines* - Functionality for AspectEngines, a specialized FlowElement with additional features.
  - *FiftyOne.Pipeline.Engines.FiftyOne* - Functionality that is specific to 51Degrees aspect engines.
- **FiftyOne.Pipeline.Web** - Projects that are relevant to the Pipeline API ASP.NET integration.
  - *FiftyOne.Pipeline.Web* - ASP.NET Core integration.
  - *FiftyOne.Pipeline.Web.Framework* - ASP.NET Framework integration.
  - *FiftyOne.Pipeline.Web.Minify* - FlowElement which takes the JavaScript function from the JavaScriptBundler element and minifies it.
  - *FiftyOne.Pipeline.Web.Shared* - Shared code that is used by both Core and Framework ASP.NET integrations.
- **FiftyOne.Pipeline.Elements** - Projects for various common Flow Elements that are used by multiple other solutions.
  - *FiftyOne.Pipeline.JavaScriptBuilder* - An element that packages values from all 'JavaScript' properties from all engines into a single JavaScript function.
  - *FiftyOne.Pipeline.JsonBuilder* - An element that serializes all properties from all engines into JSON format.
- **FiftyOne.CloudRequestEngine** - Projects related to making general requests to the 51Degrees cloud.
  - *FiftyOne.Pipeline.CloudRequestEngine* - An engine that makes requests to the 51Degrees cloud service.

## Installation

You can either clone this repository and reference the projects locally or you can reference the [NuGet][nuget] packages directly.

```
Install-Package FiftyOne.Pipeline.Core
Install-Package FiftyOne.Pipeline.Engines
Install-Package FiftyOne.Pipeline.Engines.FiftyOne
Install-Package FiftyOne.Pipeline.Web
Install-Package FiftyOne.Pipeline.Web.Minify
Install-Package FiftyOne.Pipeline.JsonBuilder
Install-Package FiftyOne.Pipeline.JavaScriptBuilder
Install-Package FiftyOne.Pipeline.CloudRequestEngine
```

Note that the packages have dependencies on each other so you'll never need to install all of them individually.
For example, Installing `FiftyOne.Pipeline.Engines.FiftyOne` will automatically add `FiftyOne.Pipeline.Engines` and `FiftyOne.Pipeline.Core`.

## Examples

### Pipeline Examples

There are several examples available that demonstrate how to make use of the Pipeline API in isolation. These are described in the table below.
If you want examples that demonstrate how to use 51Degrees products such as device detection, then these are available in the corresponding [repository](https://github.com/51Degrees/device-detection-dotnet) and on our [website](https://51degrees.com/documentation/_examples__device_detection__index.html?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=readme.md&utm_term=pipeline-examples).

| Example                                   | Description |
|-------------------------------------------|-------------|
| CustomFlowElement\1. Simple Flow Element  | Shows how to create a custom flow element that returns star sign based on a supplied date of birth. |
| CustomFlowElement\2. On Premise Engine    | Shows how to modify SimpleFlowElement to make use of the 'engine' functionality and use a custom data file to map dates to star signs rather than relying on hard coded data. |
| CustomFlowElement\3. Client-side evidence | Shows how to modify SimpleFlowElement to request the data of birth from the user using client-side JavaScript. |
| CustomFlowElement\4. Cloud Engine         | Shows how to modify SimpleFlowElement to perform the star sign lookup via a cloud service rather than locally. |
| ResultCaching                             | Shows how the result caching feature works. |
| UsageSharing                              | Shows how to share usage with 51Degrees. This helps us to keep our products up to date and accurate. |

The CloudRequestEngine\GettingStarted example requires a resource key. It reads the aligned `_51DEGREES_RESOURCE_KEY` environment variable first, then the legacy `RESOURCE_KEY` variable. A resource key with the free properties used by the examples can be created at https://configure.51degrees.com/Wkqxf3Bs?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=readme.md&utm_term=pipeline-examples.

## Cancelling processing

Pass a `CancellationToken` to `CreateFlowData` to stop processing when the token
is cancelled. The pipeline checks the token between elements, so once it is
cancelled no further elements run.

```csharp
using var cts = new CancellationTokenSource();
using var flowData = pipeline.CreateFlowData(cts.Token);
flowData.AddEvidence("key", "value");

// Cancel processing if it has not finished within one second (for example
// when a web request is aborted). Elements after the cancellation point are
// skipped.
cts.CancelAfter(TimeSpan.FromSeconds(1));
flowData.Process();
```

In the ASP.NET Core integration the token is wired to `HttpContext.RequestAborted`,
and in the .NET Framework integration to `HttpResponse.ClientDisconnectedToken`, so
processing stops automatically when the client disconnects.

An element with long-running work can check `flowData.GetStopToken()` itself
to stop sooner; elements that don't are simply skipped once the token is cancelled.

## Tests

- **FiftyOne.Pipeline.CloudRequestEngine.Tests** - Tests for the CloudRequestEngine and builder.
- **FiftyOne.Pipeline.Core.Tests** - Tests for FlowElement and FlowData base classes.
- **FiftyOne.Pipeline.Engines.Tests** - Tests for AspectEngines and AspectData base classes.
- **FiftyOne.Pipeline.Engines.FiftyOne.Tests** - Tests for 51Degrees specific aspect engines.
- **FiftyOne.Pipeline.Examples.Tests** - Tests for developer examples. This will automatically run all the examples and ensure they do not crash.
- **FiftyOne.Pipeline.Web.Tests** - Tests for web integration functionality.

The tests can be run from within Visual Studio or (in most cases) by using the `dotnet test` command line tool.

## Project documentation

For complete documentation on the Pipeline API and associated engines, see the [51Degrees documentation site][Documentation].

[Documentation]: https://51degrees.com/documentation/index.html?utm_source=github&utm_medium=readme&utm_campaign=pipeline-dotnet&utm_content=readme.md&utm_term=project-documentation
[nuget]: https://www.nuget.org/profiles/51Degrees
