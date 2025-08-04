$scriptRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

# Constants
$PASSES=20000
$PROJECT="$scriptRoot/.."
$SERVICEHOST="localhost:5000"
$CAL="calibrate"
$PRO="process?operation=1plus1"
$PERF="$scriptRoot/ApacheBench-prefix/src/ApacheBench-build/bin/runPerf.ps1"

Invoke-Expression "$PERF -n $PASSES -s 'dotnet run --project $PROJECT' -c $CAL -p $PRO -h $SERVICEHOST"
