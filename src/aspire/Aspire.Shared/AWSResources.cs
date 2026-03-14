namespace Aspire.Shared
{
    public class AWSResources
    {
        public const string Section = "AWS:Resources";
        public S3? s3 { get; set; }
        public class S3 
        {
            public string BucketName { get; set; } = string.Empty;
        }
    }
}
