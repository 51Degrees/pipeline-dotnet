#!/bin/sh

# Constants
PASSES=20000
PROJECT=..
HOST=127.0.0.1:5000
CAL=calibrate
PRO=process?operation=1plus1
PERF=./ApacheBench-prefix/src/ApacheBench-build/bin/runPerf.sh

$PERF -n $PASSES -s "dotnet run --project $PROJECT" -c $CAL -p $PRO -h $HOST -v 2
