param(
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$StrongNameKeyBase64,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "msbuild"
)

[IO.File]::WriteAllBytes("$PSScriptRoot/../51Degrees.snk", [Convert]::FromBase64String($StrongNameKeyBase64))

$Solutions = @("FiftyOne.CloudRequestEngine.sln", "FiftyOne.Pipeline.Elements.sln", "FiftyOne.Pipeline.sln", "FiftyOne.Pipeline.Web.sln")

foreach($Solution in $Solutions){
    if ($BuildMethod -eq "dotnet"){

        ./dotnet/build-project-core.ps1 -RepoName $RepoName -ProjectDir $Solution -Name $Name -Configuration $Configuration -Arch $Arch

    }
    else{

        ./dotnet/build-project-framework.ps1 -RepoName $RepoName -ProjectDir $Solution -Name $Name -Configuration $Configuration -Arch $Arch
    }
}


exit $LASTEXITCODE
