#!/bin/bash
set -e

# Default values
ENV_FILE=".env"
PARAMETER_NAME="/AlphaZero/DocumentsPipeline/R2Credentials"

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        -env|--env) 
            ENV_FILE=".env.$2"
            shift 2
            ;;
        *) 
            PARAMETER_NAME="$1"
            shift
            ;;
    esac
done

# 1. Load from environment file if it exists
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ -f "$ROOT_DIR/$ENV_FILE" ]; then
    echo "🔍 Found environment file at root ($ROOT_DIR/$ENV_FILE), sourcing variables..."
    set -a
    source "$ROOT_DIR/$ENV_FILE"
    set +a
else
    echo "⚠️ Environment file '$ENV_FILE' not found at root. Relying on existing environment variables..."
fi

# 2. Ensure required variables are set
if [ -z "$CF_R2_ACCESS_KEY" ] || [ -z "$CF_R2_SECRET_KEY" ] || [ -z "$CF_R2_PUBLIC_BUCKET" ] || [ -z "$CF_R2_PRIVATE_BUCKET" ] || [ -z "$CF_R2_VIDEOS_BUCKET" ] || [ -z "$CF_R2_SERVICE_URL" ]; then
    echo "❌ Error: Missing required environment variables!"
    echo "Please ensure the following are exported or defined in your $ENV_FILE file:"
    echo "  - CF_R2_ACCESS_KEY"
    echo "  - CF_R2_SECRET_KEY"
    echo "  - CF_R2_PUBLIC_BUCKET"
    echo "  - CF_R2_PRIVATE_BUCKET"
    echo "  - CF_R2_VIDEOS_BUCKET"
    echo "  - CF_R2_SERVICE_URL"
    exit 1
fi

# 3. Generate JSON payload dynamically
echo "📦 Generating JSON payload from environment variables..."
PAYLOAD=$(cat <<EOF
{
  "AccessKeyId": "$CF_R2_ACCESS_KEY",
  "SecretAccessKey": "$CF_R2_SECRET_KEY",
  "PublicBucketName": "$CF_R2_PUBLIC_BUCKET",
  "PrivateBucketName": "$CF_R2_PRIVATE_BUCKET",
  "VideosBucketName": "$CF_R2_VIDEOS_BUCKET",
  "ServiceURL": "$CF_R2_SERVICE_URL"
}
EOF
)

# Minify JSON payload to avoid CLI escaping issues
if command -v jq &> /dev/null; then
    PAYLOAD=$(echo "$PAYLOAD" | jq -c .)
else
    PAYLOAD=$(echo "$PAYLOAD" | tr -d '\n' | tr -d ' ')
fi

echo "🚀 Seeding AWS SSM Parameter '$PARAMETER_NAME'..."

aws ssm put-parameter \
    --name "$PARAMETER_NAME" \
    --value "$PAYLOAD" \
    --type "SecureString" \
    --overwrite

echo "✅ Successfully seeded R2 credentials into AWS SSM Parameter Store."
