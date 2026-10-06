import re

with open('infrastructure/AlphaZero.Cdk/Stacks/StorageStack.cs', 'r') as f:
    content = f.read()

# Replace InputBucket with InputBucketPrivate in the property
content = content.replace('public IBucket InputBucket { get; }', 'public IBucket InputBucketPrivate { get; }\n    public IBucket InputBucketPublic { get; }')

# Replace InputBucket assignment
input_bucket_assignment = """        InputBucketPrivate = new Bucket(this, "RawUploadsBucketPrivate", new BucketProps
        {
            BucketName = $"alphazero-raw-uploads-private-{env}",
            EventBridgeEnabled = true,
            Cors = new[]
            {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" },
                    MaxAge = 3600
                }
            }
        });

        InputBucketPublic = new Bucket(this, "RawUploadsBucketPublic", new BucketProps
        {
            BucketName = $"alphazero-raw-uploads-public-{env}",
            EventBridgeEnabled = true,
            Cors = new[]
            {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" },
                    MaxAge = 3600
                }
            }
        });"""

content = re.sub(r'InputBucket = new Bucket\(this, "RawUploadsBucket", new BucketProps\s*\{.*?\n        \}\);', input_bucket_assignment, content, flags=re.DOTALL)

# Update MediaConvertRole policy to use InputBucketPrivate (assuming it uses private bucket for video)
content = content.replace('InputBucket.BucketArn', 'InputBucketPrivate.BucketArn')

# Update Outputs
content = content.replace('new CfnOutput(this, "InputS3BucketName", new CfnOutputProps { Value = InputBucket.BucketName });', 'new CfnOutput(this, "InputS3BucketNamePrivate", new CfnOutputProps { Value = InputBucketPrivate.BucketName });\n        new CfnOutput(this, "InputS3BucketNamePublic", new CfnOutputProps { Value = InputBucketPublic.BucketName });')

with open('infrastructure/AlphaZero.Cdk/Stacks/StorageStack.cs', 'w') as f:
    f.write(content)
