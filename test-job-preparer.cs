using System;
using System.Security.Cryptography;
using System.Text;

class Program {
    static void Main() {
        var masterSecret = "YuiUMqp/Dfl3NNK+RVS68EzgQHMrlUxjlzSq2lZiGcY=";
        var videoId = "2c584771-c78b-44d6-914e-8ab1f60b0346";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(masterSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(videoId));
        var hex = Convert.ToHexString(hash[..16]).ToLowerInvariant();
        Console.WriteLine($"Hex string from JobPreparer: {hex}");
    }
}
