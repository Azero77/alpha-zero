using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using AlphaZero.Modules.Identity.Domain.Models.Principals;

namespace AlphaZero.Modules.Identity.Infrastructure.Serialization;

public class CachedUserAssignmentSerializer : IHybridCacheSerializer<List<CachedUserAssignmentDto>>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new ConditionNodeJsonConverter() },
        PropertyNameCaseInsensitive = true
    };

    public List<CachedUserAssignmentDto> Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize<List<CachedUserAssignmentDto>>(ref reader, _jsonOptions) ?? new();
    }

    public void Serialize(List<CachedUserAssignmentDto> value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, _jsonOptions);
    }
}
