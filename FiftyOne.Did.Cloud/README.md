# 51Degrees Identifier (51Did) cloud engine

[![NuGet](https://img.shields.io/nuget/v/FiftyOne.Did.Cloud)](https://www.nuget.org/packages/FiftyOne.Did.Cloud)
[![License](https://img.shields.io/badge/license-EUPL--1.2-blue)](LICENSE)

Returns the **51Degrees Identifier (51Did)** from the 51Degrees Cloud
service through the 51Degrees Pipeline API, mapping the cloud response
into the 51Did properties a pipeline exposes.

The namespace and assembly are named `FiftyOne.Did.Cloud` only because a
.NET identifier cannot start with a digit. The product is the 51Did.

## This package

This is a thin adapter. The outbound call to the cloud is made by
`CloudRequestEngine` from
[FiftyOne.Pipeline.CloudRequestEngine](https://www.nuget.org/packages/FiftyOne.Pipeline.CloudRequestEngine),
so register both builders in the pipeline. A 51Degrees resource key is
required.

Six properties are returned, being probabilistic, random and hashed
email 51Dids, each either global across all callers or scoped to the
caller's licence key. They are described in
[FiftyOne.Did.Core](https://www.nuget.org/packages/FiftyOne.Did.Core).
To read the 51Dids returned here, and to compare them correctly by match
key rather than by envelope, use
[FiftyOne.Did](https://www.nuget.org/packages/FiftyOne.Did).

## Find out more

- [51Degrees documentation](https://51degrees.com/documentation?utm_source=nuget&utm_medium=package&utm_campaign=pipeline-dotnet&utm_content=fiftyone.did.cloud-readme.md&utm_term=documentation)
- [Get a resource key](https://configure.51degrees.com/?utm_source=nuget&utm_medium=package&utm_campaign=pipeline-dotnet&utm_content=fiftyone.did.cloud-readme.md&utm_term=resource-key)
- https://github.com/51Degrees/pipeline-dotnet
