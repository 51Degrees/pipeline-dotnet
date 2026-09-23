
param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$BuildMethod,
    [hashtable]$Keys
)

if ($BuildMethod -ne "dotnet") {

    # Setup the MSBuild environment if it is required.
    ./environments/setup-msbuild.ps1
    ./environments/setup-vstest.ps1
}

# A chromedriver found on PATH is used even when it no longer matches the
# installed Chrome, and the CI images ship ones that lag behind (chromedriver
# 152 against Chrome 154 on macOS). Skipping PATH makes Selenium Manager fetch
# a matching driver for the JavaScriptBuilderElement browser tests.
$env:SE_SKIP_DRIVER_IN_PATH = "true"
