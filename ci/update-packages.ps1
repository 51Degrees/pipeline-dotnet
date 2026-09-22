
param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$Name
)

# Update packages one solution at a time. Passing the solution file (rather than
# the repository root) keeps 'dotnet-outdated' from touching projects that live in
# git submodules - none of these solutions reference submodule projects, so
# updating those here would only leave the submodules dirty and break the commit.
$Solutions = @("FiftyOne.CloudRequestEngine.sln", "FiftyOne.Pipeline.Elements.sln", "FiftyOne.Pipeline.sln", "FiftyOne.Pipeline.Web.sln")

foreach($Solution in $Solutions){

    ./dotnet/outdated.ps1 -RepoName:$RepoName -Target $Solution

}

exit $LASTEXITCODE