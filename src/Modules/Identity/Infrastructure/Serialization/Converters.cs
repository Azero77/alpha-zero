using AlphaZero.Modules.Identity.Domain.Models;
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

public class ConditionNodeJsonConverter : JsonConverter<IConditionNode>
{
    public override IConditionNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("Type", out var typeProp))
            throw new JsonException("Condition node must have a 'Type' property.");

        var typeStr = typeProp.GetString();
        if (!Enum.TryParse<ConditionType>(typeStr, true, out var conditionType))
            throw new JsonException($"Unknown condition type: {typeStr}");

        return conditionType switch
        {
            ConditionType.Statement => JsonSerializer.Deserialize<ConditionNode>(root.GetRawText(), options),
            ConditionType.And => JsonSerializer.Deserialize<AndNode>(root.GetRawText(), options),
            ConditionType.Or => JsonSerializer.Deserialize<OrNode>(root.GetRawText(), options),
            ConditionType.Not => JsonSerializer.Deserialize<NotNode>(root.GetRawText(), options),
            _ => throw new JsonException($"Unsupported condition type: {conditionType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, IConditionNode value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }
}
