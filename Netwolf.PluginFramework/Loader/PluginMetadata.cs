// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Loader;

public class PluginMetadata
{
    public int Id { get; init; }
    
    public string Name { get; init; }
    
    public string Description { get; init; }
    
    public string Author { get; init; }
    
    public string Version { get; init; }

    public string Path { get; init; }

    public bool IsLoaded => Context.IsAlive;

    private WeakReference Context { get; init; }

    internal PluginMetadata(int id, PluginInfo info)
    {
        Id = id;
        Name = info.Plugin.Name;
        Description = info.Plugin.Description;
        Author = info.Plugin.Author;
        Version = info.Plugin.Version;
        Path = info.Context.Path;
        Context = new WeakReference(info.Context);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not PluginMetadata other)
        {
            return false;
        }

        return Id == other.Id &&
               Name == other.Name &&
               Description == other.Description &&
               Author == other.Author &&
               Version == other.Version &&
               Path == other.Path;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Description, Author, Version, Path);
    }
}
