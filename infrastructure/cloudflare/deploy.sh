#!/bin/bash
set -e

ENV=${1:-dev}
ACTION=${2:-plan}
ENV_FILE="../../.env.${ENV}"

if [ -f "$ENV_FILE" ]; then
    echo "Sourcing $ENV_FILE..."
    set -o allexport
    source "$ENV_FILE"
    set +o allexport
else
    echo "Warning: $ENV_FILE not found."
fi

echo "Running terraform $ACTION for environment: $ENV..."
# Shift the first two arguments so any extra args are passed to terraform
shift 2 || true

if [ "$ACTION" = "apply" ]; then
    terraform apply "$@"
elif [ "$ACTION" = "destroy" ]; then
    terraform destroy "$@"
else
    terraform plan "$@"
fi
