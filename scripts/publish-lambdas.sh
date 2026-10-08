#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==========================================="
echo " Publishing Code Lambdas (Native AOT/ZIP)  "
echo "==========================================="

CODE_LAMBDAS=(
    "src/lambdas/AlphaZero.JobPreparer/src/AlphaZero.JobPreparer/AlphaZero.JobPreparer.csproj"
    "src/lambdas/AlphaZero.S3VideoCreatedEventParser/S3VideoCreatedEventParser.csproj"
    "src/lambdas/DocumentUploadedS3EventParser/DocumentUploadedS3EventParser.csproj"
    "src/lambdas/ImageProcessorAndMoverToR2/ImageProcessorAndMoverToR2.csproj"
)

for csproj in "${CODE_LAMBDAS[@]}"; do
    # Extract the project name from the path to determine the output directory
    LAMBDA_NAME=$(basename $(dirname "$csproj"))
    OUTPUT_DIR="${REPO_ROOT}/src/lambdas/${LAMBDA_NAME}/publish"
    
    echo "==> Publishing ${LAMBDA_NAME} to ${OUTPUT_DIR}..."
    mkdir -p "${OUTPUT_DIR}"
    
    dotnet publish "${REPO_ROOT}/${csproj}" \
        -c Release \
        -r linux-x64 \
        --self-contained \
        -o "${OUTPUT_DIR}"
    
    echo "  -> Published ${LAMBDA_NAME} successfully!"
done

echo ""
echo "==========================================="
echo " Building Docker Images for Lambdas/Workers"
echo "==========================================="

DOCKER_TARGETS=(
    "src/lambdas/AlphaZero.VideoAnalyzer"
    "src/workers/AlphaZero.R2Mover"
)

for target in "${DOCKER_TARGETS[@]}"; do
    IMAGE_NAME=$(basename "$target" | tr '[:upper:]' '[:lower:]')
    TARGET_DIR="${REPO_ROOT}/${target}"
    
    echo "==> Building Docker Image: ${IMAGE_NAME} from ${TARGET_DIR}..."
    
    # We build from REPO_ROOT so Dockerfile can access things like Directory.Packages.props
    docker build -t "${IMAGE_NAME}:latest" -f "${TARGET_DIR}/Dockerfile" "${REPO_ROOT}"
    
    echo "  -> Built ${IMAGE_NAME}:latest successfully!"
done

echo ""
echo "==> All lambdas and workers published/built successfully!"
