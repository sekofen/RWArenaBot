

using System;
using System.Collections.Generic;

namespace Server;

public class LobbyState
{
    public string Name;
    public int Players;
    public int MaxPlayers;
    public List<string> Mods;

    // I thing this is a set, but the AI recomended this mmmmmm
    static readonly HashSet<string> TrackedMods = new()
    {
        "moreslugcats",
        "watcher",
        "gulagArenas",
    };


    public static List<string> DetectMods()
    {
        var found = new List<string>();
        if (ModManager.ActiveMods == null) return found;

        foreach (var t in ModManager.ActiveMods)
        {
            if (TrackedMods.Contains(t.id))
            {
                found.Add(t.name);
            }
        }
        return found;
    }
};




public class POST
{
    public static void Init()
    {
        // Initialize the POST system
    }

    public static void Send(LobbyState state)
    {
        // Send to the bot server the information about the lobby
    }

    public static void Close()
    {
        // Notify lobby is closed
    }
}