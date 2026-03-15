namespace Aspire.Shared
{
    public class AWSResources
    {
        public const string Section = "AWS:Resources";
        public S3Settings? s3 { get; set; }
        public class S3Settings
        {
            public string BucketName { get; set; } = string.Empty;
        }
    }
}
