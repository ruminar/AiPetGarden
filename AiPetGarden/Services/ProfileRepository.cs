using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiPetGarden.Models;

namespace AiPetGarden.Services;

public sealed class ProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _profilesDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ProfileRepository(string? appDataRoot = null)
    {
        var root = appDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Pet-Garden");
        _profilesDirectory = Path.Combine(root, "profiles");
    }

    public async Task<PetProfile?> LoadAsync(string petId, CancellationToken cancellationToken = default)
    {
        return await LoadCoreAsync(petId, cancellationToken);
    }

    public async Task SaveAsync(PetProfile profile, CancellationToken cancellationToken = default)
    {
        ValidateProfile(profile);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await WriteProfileAsync(profile, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveViewAsync(
        string petId,
        string petAssetId,
        string displayName,
        PetViewSettings view,
        CancellationToken cancellationToken = default)
    {
        ValidateView(view);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var profile = await LoadCoreAsync(petId, cancellationToken) ?? new PetProfile
            {
                PetId = petId,
                PetAssetId = petAssetId,
                DisplayName = displayName,
                View = view
            };
            var updatedProfile = profile with { PetAssetId = petAssetId, View = view };
            ValidateProfile(updatedProfile);
            await WriteProfileAsync(updatedProfile, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<PetProfile?> LoadCoreAsync(string petId, CancellationToken cancellationToken)
    {
        var profilePath = GetProfilePath(petId);
        if (!File.Exists(profilePath)) return null;

        await using var stream = new FileStream(
            profilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var profile = await JsonSerializer.DeserializeAsync<PetProfile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The pet profile is empty.");

        if (!string.Equals(profile.PetId, petId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The pet profile ID does not match its requested pet.");
        }

        ValidateProfile(profile);
        return profile;
    }

    private async Task WriteProfileAsync(PetProfile profile, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_profilesDirectory);
        var profilePath = GetProfilePath(profile.PetId);
        string? temporaryPath = null;
        try
        {
            temporaryPath = profilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, profilePath, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private string GetProfilePath(string petId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(petId);
        var fileStem = IsSafeFileStem(petId)
            ? petId
            : "pet-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(petId)))[..16].ToLowerInvariant();
        return Path.Combine(_profilesDirectory, $"{fileStem}.json");
    }

    private static bool IsSafeFileStem(string value)
    {
        if (value.Length is 0 or > 80 || value is "." or "..") return false;
        return value.All(character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
    }

    private static void ValidateView(PetViewSettings view)
    {
        if (view is null)
        {
            throw new InvalidDataException("The pet profile does not contain view settings.");
        }
        if (!double.IsFinite(view.X) || !double.IsFinite(view.Y))
        {
            throw new InvalidDataException("Pet coordinates must be finite numbers.");
        }

        if (!double.IsFinite(view.Scale) || view.Scale <= 0)
        {
            throw new InvalidDataException("Pet scale must be a positive finite number.");
        }

        if (!Enum.IsDefined(view.LifeState))
        {
            throw new InvalidDataException("The pet life state is invalid.");
        }
    }

    private static void ValidateProfile(PetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.PetId) || string.IsNullOrWhiteSpace(profile.PetAssetId))
        {
            throw new InvalidDataException("Pet profile IDs must not be empty.");
        }
        if (profile.ProjectRef is null || profile.Persona is null || profile.Backend is null || profile.Relay is null)
        {
            throw new InvalidDataException("The pet profile is missing required settings.");
        }

        ValidateView(profile.View);
        if (!string.IsNullOrWhiteSpace(profile.ProjectRef.Url) &&
            (!Uri.TryCreate(profile.ProjectRef.Url, UriKind.Absolute, out var projectUri) ||
             projectUri.Scheme is not ("http" or "https")))
        {
            throw new InvalidDataException("Project URL must use http or https.");
        }
    }
}
