#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${REPO_ROOT}/src/lambdas/AlphaZero.JobPreparer/publish"

echo "==> Packaging Native AOT JobPreparer for linux-x64..."
mkdir -p "${OUTPUT_DIR}"

dotnet publish "${REPO_ROOT}/src/lambdas/AlphaZero.JobPreparer/src/AlphaZero.JobPreparer/AlphaZero.JobPreparer.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -o "${OUTPUT_DIR}"

echo "==> JobPreparer published successfully to: ${OUTPUT_DIR}"
ls -la "${OUTPUT_DIR}"
