param(
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$perfTests = "$PSScriptRoot/../performance-tests"
$testResults = New-Item -ItemType Directory -Force -Path "$PSScriptRoot/../test-results/performance-summary/"

dotnet build $perfTests -c $Configuration /p:Platform=$Arch
try {
    $server = dotnet run --project $perfTests &
    $results = ./scripts/httpbench.ps1 -HostPort 'localhost:5000' -Endpoint '/process?operation=1plus1' -CalibrateEndpoint '/calibrate' -UaFile 'assets/20000 User Agents.csv'
} finally {
    Stop-Job $server
}

Write-Host "Writing performance test results"
@{
    'HigherIsBetter' = @{
        'DetectionsPerSecond' = 1/($results.overhead_ms / 1000)
    }
    'LowerIsBetter' = @{
        'MsPerDetection' = $results.overhead_ms
    }
} | ConvertTo-Json | Tee-Object "$testResults/results_$Name.json" | Write-Host
