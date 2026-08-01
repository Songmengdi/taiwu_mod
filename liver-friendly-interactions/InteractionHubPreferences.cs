using UnityEngine;

namespace LiverFriendlyInteractions.Frontend;

internal sealed class InteractionHubPreferences
{
    private const string VersionLine = "LiverFriendlyInteractions.InteractionHub.v1";
    private readonly List<string> _favorites;

    internal IReadOnlyList<string> Favorites => _favorites;
    internal string FilePath { get; }

    internal InteractionHubPreferences()
    {
        FilePath = Path.Combine(Application.persistentDataPath,
            "LiverFriendlyInteractions", "interaction-hub-favorites.txt");
        _favorites = Load(FilePath);
    }

    internal bool Contains(string key) => _favorites.Contains(key, StringComparer.Ordinal);

    internal void Add(string key)
    {
        if (!Contains(key)) _favorites.Add(key);
        Save();
    }

    internal void Remove(string key)
    {
        _favorites.RemoveAll(item => string.Equals(item, key, StringComparison.Ordinal));
        Save();
    }

    internal void Move(string key, int offset)
    {
        int index = _favorites.FindIndex(item => string.Equals(item, key, StringComparison.Ordinal));
        int target = Math.Clamp(index + offset, 0, _favorites.Count - 1);
        if (index < 0 || index == target) return;
        (_favorites[index], _favorites[target]) = (_favorites[target], _favorites[index]);
        Save();
    }

    internal void Reset()
    {
        _favorites.Clear();
        _favorites.AddRange(InteractionHubPolicy.DefaultFavorites);
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, new[] { VersionLine }.Concat(_favorites));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[护肝交互] 保存常用交互配置失败：" + exception.Message);
        }
    }

    private static List<string> Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                if (lines.FirstOrDefault() == VersionLine)
                    return lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
                        .Distinct(StringComparer.Ordinal).ToList();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[护肝交互] 读取常用交互配置失败：" + exception.Message);
        }
        return InteractionHubPolicy.DefaultFavorites.ToList();
    }
}
