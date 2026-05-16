using System.IO;
using System.Text.Json;
using AIConspect.Models;

namespace AIConspect.Services;

public class ProfileService
{
    private readonly string _profilesPath;
    private List<StudentProfile> _profiles = new();

    private static readonly string[] AvatarColors =
    {
        "#6200EE", "#03DAC6", "#E91E63", "#FF6D00",
        "#2979FF", "#00BFA5", "#D500F9", "#FF1744"
    };

    public ProfileService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AIConspect");
        Directory.CreateDirectory(dir);
        _profilesPath = Path.Combine(dir, "profiles.json");
    }

    public async Task<List<StudentProfile>> LoadProfilesAsync()
    {
        if (!File.Exists(_profilesPath))
        {
            _profiles = new List<StudentProfile>();
            return _profiles;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_profilesPath);
            _profiles = JsonSerializer.Deserialize<List<StudentProfile>>(json) ?? new();
        }
        catch
        {
            _profiles = new();
        }

        return _profiles;
    }

    public async Task SaveProfilesAsync()
    {
        var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_profilesPath, json);
    }

    public async Task<StudentProfile> CreateProfileAsync(string name)
    {
        var colorIndex = _profiles.Count % AvatarColors.Length;
        var profile = new StudentProfile
        {
            Name = name,
            AvatarColor = AvatarColors[colorIndex]
        };

        _profiles.Add(profile);
        await SaveProfilesAsync();
        return profile;
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        _profiles.RemoveAll(p => p.Id == profileId);
        await SaveProfilesAsync();
    }

    public async Task AddHistoryEntryAsync(string profileId, ConspectHistory entry)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;

        profile.History.Insert(0, entry); // новые сверху

        // Храним не более 50 записей на профиль
        if (profile.History.Count > 50)
            profile.History = profile.History.Take(50).ToList();

        await SaveProfilesAsync();
    }

    public List<StudentProfile> GetAll() => _profiles;
}