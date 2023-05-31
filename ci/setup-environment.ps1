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
$RepoPath = [IO.Path]::Combine($pwd, $RepoName)

if ($BuildMethod -ne "dotnet") {
    # Setup the MSBuild environment if it is required.
    ./environments/setup-msbuild.ps1
    ./environments/setup-vstest.ps1
}

if ($IsLinux) {
    sudo apt-get update
    # Install multilib, as this may be required.
    sudo apt-get install -y gcc-multilib g++-multilib

}

