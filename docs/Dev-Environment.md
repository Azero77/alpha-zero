# 💻 AlphaZero Local Development Environment

This document outlines the local development architecture for the AlphaZero platform. Our setup utilizes a **Hybrid Development Environment**, combining local compute orchestration with real cloud infrastructure to provide a seamless, production-accurate developer experience.

## 🏗️ The Hybrid Architecture

Because our platform relies on complex AWS serverless offerings (like AWS Elemental MediaConvert, ECS Fargate, and Step Functions) that cannot be accurately emulated offline, we split our environment into two distinct layers:

1. **Local Compute (Orchestrated by .NET Aspire):** 
   - Core API (`alphazero-api`)
   - PostgreSQL Database & PgAdmin
   - Keycloak (Identity Provider)
   - AWS Lambda Functions (`VideoAnalyzer`, `JobPreparer`)

2. **Cloud Infrastructure (AWS Developer Account):**
   - S3 Buckets (Input & Transient)
   - SQS Queues
   - AWS Step Functions (Video Pipeline Orchestration)
   - AWS Elemental MediaConvert
   - IAM Roles & KMS Keys

By deploying stateful and complex serverless services to a personal AWS Developer Account, we ensure exact parity with production without attempting to mock brittle services via LocalStack.

---

## 🛠️ How It Works Under the Hood

### 1. Infrastructure as Code (AWS CDK)
Our infrastructure is defined strictly in the `infrastructure/AlphaZero.Cdk` project.
Unlike standard Aspire setups that attempt to provision CDK constructs automatically, **we do not use Aspire to provision our infrastructure.** Aspire's local provisioner does not support packaging Lambda or Docker assets (`Code.FromAsset()`).

Instead, developers manually deploy the CDK stacks to their AWS Dev Account using the AWS CDK CLI.
These stacks emit explicit **CloudFormation Outputs** (e.g., Bucket Names, Queue URLs, ARNs).

### 2. .NET Aspire Orchestration (`AlphaZero.AppHost`)
When you start the `AppHost`, it uses the `AddAWSCloudFormationStack` extension method.
This method reaches out to your authenticated AWS account, inspects the deployed CDK stacks (`AlphaZeroStorageStack`, `AlphaZeroVideoPipelineStack`), and imports their exported outputs.

These outputs are then dynamically injected into the local `alphazero-api` and Lambda projects as strongly-typed environment variables (e.g., `AWS__Resources__InputS3__BucketName`).

### 3. Local Lambda Debugging
Our Lambda functions (`AlphaZero.VideoAnalyzer` and `AlphaZero.JobPreparer`) are registered in the `AppHost` using `builder.AddAWSLambdaFunction()`.

When Aspire launches these projects, it natively intercepts them and hosts them using the **.NET Mock Lambda Test Tool**. This means:
- The Lambdas run directly on your local machine.
- You can place breakpoints in your IDE and step through them effortlessly.
- They are pre-configured by Aspire with the correct IAM credentials and injected with the real ARNs of your cloud S3 buckets and SQS queues.

---

## 🚀 Developer Workflow

To get started with local development on a fresh machine:

### Step 1: Deploy Infrastructure (Once per Dev Account)
Ensure you have authenticated your AWS CLI with your personal Sandbox/Dev AWS account.
```bash
cd infrastructure/AlphaZero.Cdk
cdk bootstrap aws://<YOUR_ACCOUNT_ID>/<YOUR_REGION>
cdk deploy --all
```
*This will provision the S3 buckets, Step Functions, and output the ARNs needed by Aspire.*

### Step 2: Run Aspire
Open `AlphaZero.sln` in Visual Studio or Rider, or run via the CLI:
```bash
cd src/alphazero-api/aspire/AlphaZero.AppHost
dotnet run
```
*Aspire will automatically fetch the outputs from your CDK deployment and wire up the local API and Lambdas.*

### Step 3: Debugging Serverless Workflows
- **Core API**: Set breakpoints normally in `alphazero-api`.
- **Lambdas**: Set breakpoints in `Function.cs` of your Lambda project. Use the Lambda Test Tool dashboard (launched by Aspire) to trigger them with mock JSON payloads.
- **Step Functions**: Trigger the pipeline via your API. Monitor the visual execution graph in the real AWS Step Functions Console to see it interact with your cloud-hosted services.
