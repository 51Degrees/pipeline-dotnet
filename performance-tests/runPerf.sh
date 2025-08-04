#!/bin/sh

# Constants
PASSES=20000
PROJECT=..
HOST=localhost:5000
CAL=calibrate
PRO=process?operation=1plus1
PERF=./ApacheBench-prefix/src/ApacheBench-build/bin/runPerf.sh

# Override the application URL to use port 5000
export ASPNETCORE_URLS="http://localhost:5000"

$PERF -n $PASSES -s "dotnet run --project $PROJECT" -c $CAL -p $PRO -h $HOST -v 2
