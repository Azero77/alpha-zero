using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AlphaZero.R2Mover.Tests")]

namespace AlphaZero.R2Mover;

public class R2Credentials
{
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string ServiceUrl { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string PublicUrl { get; set; } = "";
}

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        var videoId = Environment.GetEnvironmentVariable("VIDEO_ID");
        var transientBucket = Environment.GetEnvironmentVariable("TRANSIENT_BUCKET");

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(transientBucket))
        {
            Console.Error.WriteLine("Missing required environment variables.");
            return 1;
        }

        Console.WriteLine($"Starting R2Mover for {tenantId}/{videoId} from {transientBucket}");

        var ssmClient = new AmazonSimpleSystemsManagementClient();
        var credRequest = new GetParameterRequest
        {
            Name = "/AlphaZero/VideoPipeline/R2Credentials",
            WithDecryption = true
        };

        var credResponse = await ssmClient.GetParameterAsync(credRequest);
        var r2Creds = JsonSerializer.Deserialize<R2Credentials>(credResponse.Parameter.Value);

        if (r2Creds == null)
        {
            Console.Error.WriteLine("Failed to parse R2 credentials.");
            return 1;
        }

        var r2Config = new AmazonS3Config
        {
            ServiceURL = r2Creds.ServiceUrl,
        };
        var r2Client = new AmazonS3Client(r2Creds.AccessKeyId, r2Creds.SecretAccessKey, r2Config);
        var s3Client = new AmazonS3Client();

        var prefix = $"{tenantId}/{videoId}/";
        var listRequest = new ListObjectsV2Request
        {
            BucketName = transientBucket,
            Prefix = prefix
        };

        var listResponse = await s3Client.ListObjectsV2Async(listRequest);
        
        if (listResponse.S3Objects.Count == 0)
        {
            Console.WriteLine("No objects found to move.");
            return 0;
        }

        var semaphore = new SemaphoreSlim(20);
        var tasks = new List<Task>();

        foreach (var obj in listResponse.S3Objects)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    Console.WriteLine($"Copying {obj.Key} to R2...");
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = transientBucket,
                        Key = obj.Key
                    };
                    using var getResponse = await s3Client.GetObjectAsync(getRequest);
                    
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = r2Creds.BucketName,
                        Key = obj.Key,
                        InputStream = getResponse.ResponseStream,
                        ContentType = GetContentType(obj.Key),
                        DisablePayloadSigning = true
                    };
                    await r2Client.PutObjectAsync(putRequest);
                    Console.WriteLine($"Successfully copied {obj.Key}");
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("All files moved to R2 successfully.");
        // We output a JSON that can be picked up if needed, though Fargate .sync doesn't easily capture it.
        var resultUrl = $"{r2Creds.PublicUrl.TrimEnd('/')}/{tenantId}/{videoId}/master.m3u8";
        Console.WriteLine(JsonSerializer.Serialize(new { playbackUrl = resultUrl }));
        return 0;
    }

    internal static string GetContentType(string key)
    {
        if (key.EndsWith(".m3u8")) return "application/vnd.apple.mpegurl";
        if (key.EndsWith(".m4s")) return "video/iso.segment";
        if (key.EndsWith(".mp4")) return "video/mp4";
        if (key.EndsWith(".json")) return "application/json";
        if (key.EndsWith(".jpg") || key.EndsWith(".jpeg")) return "image/jpeg";
        return "application/octet-stream";
    }
}
