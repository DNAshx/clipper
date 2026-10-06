using clipper.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace clipper.Services;

public sealed class SettingsStore
{
    private readonly string path;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string? LoadError { get; private set; }

    public SettingsStore(string? path = null)
    {
        this.path = path ?? Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "ClipperAI", "settings.dat");
    }

    public LlmSettings Load()
    {
        if (!File.Exists(path)) return new();
        try
        {
            byte[] data = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            try
            {
                var settings = JsonSerializer.Deserialize<LlmSettings>(data, JsonOptions) ?? new();
                settings.Normalize();
                return settings;
            }
            finally { CryptographicOperations.ZeroMemory(data); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            LoadError = "Не удалось прочитать настройки. Откройте настройки LLM и сохраните их заново.";
            return new();
        }
    }

    public void Save(LlmSettings settings)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(data); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllBytes(temporary, encrypted);
        File.Move(temporary, path, overwrite: true);
        LoadError = null;
    }

    public static LlmSettings Copy(LlmSettings settings) => JsonSerializer.Deserialize<LlmSettings>(
        JsonSerializer.Serialize(settings, JsonOptions), JsonOptions)!;
}
