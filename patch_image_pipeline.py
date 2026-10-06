import re

with open('infrastructure/AlphaZero.Cdk/Constructs/ImagePipelineConstruct.cs', 'r') as f:
    content = f.read()

# Replace InputBucket with InputBucketPrivate and InputBucketPublic in props
content = content.replace('public required IBucket InputBucket { get; set; }', 'public required IBucket InputBucketPrivate { get; set; }\n    public required IBucket InputBucketPublic { get; set; }')

# Update GrantRead
content = content.replace('props.InputBucket.GrantRead(MoverLambda);', 'props.InputBucketPrivate.GrantRead(MoverLambda);\n        props.InputBucketPublic.GrantRead(MoverLambda);')

# Update EventBridge Rules
event_rule_private = """var eventRulePrivate = new Amazon.CDK.AWS.Events.Rule(this, "ImageProcessingRulePrivate", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.InputBucketPrivate.BucketName } } } }
                }
            }
        });
        eventRulePrivate.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));

        var eventRulePublic = new Amazon.CDK.AWS.Events.Rule(this, "ImageProcessingRulePublic", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.InputBucketPublic.BucketName } } } }
                }
            }
        });
        eventRulePublic.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));"""

content = re.sub(r'var eventRule = new Amazon.CDK.AWS.Events.Rule.*?eventRule.AddTarget.*?;\n', event_rule_private, content, flags=re.DOTALL)

with open('infrastructure/AlphaZero.Cdk/Constructs/ImagePipelineConstruct.cs', 'w') as f:
    f.write(content)
