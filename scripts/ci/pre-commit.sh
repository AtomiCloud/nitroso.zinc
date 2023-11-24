#!/usr/bin/env bash

set -eou pipefail
dotnet restore
pre-commit run --all
