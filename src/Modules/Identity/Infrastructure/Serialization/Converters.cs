using AlphaZero.Shared.Domain;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlphaZero.Modules.Identity.Infrastructure.Serialization;
public class ResourcePatternJsonConverter : JsonConverter<ResourcePattern>
{
    public override ResourcePattern Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return ResourcePattern.Create(value!).Value;
    }

    public override void Write(Utf8JsonWriter writer, ResourcePattern value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public class ResourceArnJsonConverter : JsonConverter<ResourceArn>
{
    public override ResourceArn Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return ResourceArn.Create(value!).Value;
    }

    public override void Write(Utf8JsonWriter writer, ResourceArn value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}


