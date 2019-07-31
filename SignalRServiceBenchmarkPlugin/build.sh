#!/bin/bash

build()
{
  cd src/rpc
  ./build.sh
  cd ../..
  cd src/appserver
  ./build.sh
  cd ../..
}

kore_build()
{
  set -euo pipefail
  DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Call "sync" between "chmod" and execution to prevent "text file busy" error in Docker (aufs)
  chmod +x "$DIR/run.sh"; sync
  "$DIR/run.sh" default-build "$@"
}

build
kore_build
