terraform {
  backend "s3" {
    bucket         = "alphazero-terraform-state"
    key            = "cloudflare/terraform.tfstate"
    region         = "eu-north-1"
    encrypt        = true
  }

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
  }
}

variable "cloudflare_api_token" {
  description = "Cloudflare API Token for Terraform"
  type        = string
  sensitive   = true
}

variable "cloudflare_account_id" {
  description = "Cloudflare Account ID"
  type        = string
}

variable "cloudflare_zone_id" {
  description = "Cloudflare Zone ID for the custom domain"
  type        = string
}

variable "environment" {
  description = "Environment (dev, prod)"
  type        = string
  default     = "dev"
}

variable "cdn_domain_name" {
  description = "Custom domain name for the public R2 bucket"
  type        = string
  default     = "cdn.alphazero.academy"
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

# 1. Private R2 Bucket (No public access)
resource "cloudflare_r2_bucket" "docs_private" {
  account_id = var.cloudflare_account_id
  name       = "alphazero-docs-private-${var.environment}"
}

# 2. Public R2 Bucket
resource "cloudflare_r2_bucket" "docs_public" {
  account_id = var.cloudflare_account_id
  name       = "alphazero-docs-public-${var.environment}"
}
resource "cloudflare_r2_bucket" "videos" {
  account_id = var.cloudflare_account_id
  name       = "alphazero-videos-${var.environment}"
}

locals {
  cdn_base = var.environment == "prod" ? var.cdn_domain_name : "${var.environment}-${var.cdn_domain_name}"
}

# Note: Custom Domains for R2 cannot be provisioned purely via Terraform in Cloudflare provider v4.x.
# You will need to manually connect these custom domains to the buckets in the Cloudflare Dashboard:
# - public.${local.cdn_base} -> docs_public
# - private.${local.cdn_base} -> docs_private
# - videos.${local.cdn_base} -> videos

