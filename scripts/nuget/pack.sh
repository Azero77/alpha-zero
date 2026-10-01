#!/bin/bash
# Builds and packs the shared library into a .nupkg file
cd "$(dirname "$0")/../../src/shared/AlphaZero.ImageProcessing"
dotnet pack -c Release -o ../../../artifacts/nuget
echo "Package built successfully at AlphaZeroLearningAcademy/artifacts/nuget"
