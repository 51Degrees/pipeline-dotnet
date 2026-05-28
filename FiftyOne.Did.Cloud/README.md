# 51Degrees Did Cloud

[![NuGet](https://img.shields.io/nuget/v/FiftyOne.Did.Cloud)](https://www.nuget.org/packages/FiftyOne.Did.Cloud)
[![License](https://img.shields.io/badge/license-EUPL--1.2-blue)](LICENSE)

Cloud aspect engine for **51Degrees Device Identification (Did)**. Maps 51Did data properties to and from the cloud API JSON response.

## This Package

This is a thin adapter layer that integrates with the 51Degrees Pipeline API. The actual outbound API call is handled by `CloudRequestEngine` from `FiftyOne.Pipeline.CloudRequestEngine`. Register both builders in your pipeline to enable cloud-based device identification.

Requires a valid 51Degrees resource key for cloud API access.

## Links

- [51Degrees Website](https://51degrees.com)
- [Documentation](https://51degrees.com/documentation)
- [Source Repository](https://github.com/51Degrees/cloud)
