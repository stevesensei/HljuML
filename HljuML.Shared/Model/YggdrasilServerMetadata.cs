using System.Text.Json;
using System.Text.Json.Serialization;

namespace HljuML.Shared.Model;

/// <summary>Yggdrasil API 根地址返回的服务器信息。</summary>
public sealed class YggdrasilServerMetadata
{
    [JsonPropertyName("meta")]
    public YggdrasilServerMeta? Meta { get; init; }

    [JsonPropertyName("skinDomains")]
    public string[]? SkinDomains { get; init; }

    [JsonPropertyName("signaturePublickey")]
    public string? SignaturePublicKey { get; init; }
}

public sealed class YggdrasilServerMeta
{
    [JsonPropertyName("serverName")]
    public string? ServerName { get; init; }

    [JsonPropertyName("implementationName")]
    public string? ImplementationName { get; init; }

    [JsonPropertyName("implementationVersion")]
    public string? ImplementationVersion { get; init; }

    [JsonPropertyName("feature.non_email_login")]
    public bool? NonEmailLogin { get; init; }

    [JsonPropertyName("feature.legacy_skin_api")]
    public bool? LegacySkinApi { get; init; }

    [JsonPropertyName("feature.no_mojang_namespace")]
    public bool? NoMojangNamespace { get; init; }

    [JsonPropertyName("feature.enable_mojang_anti_features")]
    public bool? EnableMojangAntiFeatures { get; init; }

    [JsonPropertyName("feature.enable_profile_key")]
    public bool? EnableProfileKey { get; init; }

    [JsonPropertyName("feature.username_check")]
    public bool? UsernameCheck { get; init; }

    [JsonPropertyName("links")]
    public YggdrasilServerLinks? Links { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public sealed class YggdrasilServerLinks
{
    [JsonPropertyName("homepage")]
    public string? Homepage { get; init; }

    [JsonPropertyName("register")]
    public string? Register { get; init; }
}
