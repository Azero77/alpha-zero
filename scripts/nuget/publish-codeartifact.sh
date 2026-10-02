#!/bin/bash
# Example CI/CD script for AWS CodeArtifact
# You can run this in your pipeline after pack.sh

DOMAIN="alpha-zero"
DOMAIN_OWNER="555106000478"
REPOSITORY="AlphaZero.ImageSharper"
REGION="eu-north-1"

echo "Authenticating with AWS CodeArtifact..."
# Get temporary auth token
TOKEN=$(aws codeartifact get-authorization-token --domain $DOMAIN --domain-owner $DOMAIN_OWNER --region $REGION --query authorizationToken --output text)

# Get repository endpoint
ENDPOINT=$(aws codeartifact get-repository-endpoint --domain $DOMAIN --domain-owner $DOMAIN_OWNER --repository $REPOSITORY --format nuget --region $REGION --query repositoryEndpoint --output text)
ENDPOINT_V3="${ENDPOINT}v3/index.json"

# Find latest nupkg
PKG=$(ls -t "$(dirname "$0")/../../artifacts/nuget/"*.nupkg | head -n 1)

echo "Pushing $PKG to $ENDPOINT_V3..."
dotnet nuget push "$PKG" --source "$ENDPOINT_V3" --api-key "$TOKEN"
