

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http;
using System.Linq;

namespace Server;

public class LobbyState
{
    public readonly int Id = new Random().Next(10000000, 99999999);

    public string Name = "Default";
    public int Players;
    public int MaxPlayers;
    public List<string> Mods;

    public LobbyEvent Event = LobbyEvent.open;

    static readonly HashSet<string> TrackedMods = new()
    {
        "moreslugcats",
        "watcher",
        "gulagArenas",
    };

    public enum LobbyEvent
    {
        open,
        update,
        close
    }


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

    public static string ToJSON(LobbyState state)
    {
        string modsJson = state.Mods is { Count: > 0 }
                        ? "[\"" + string.Join("\", \"", state.Mods) + "\"]"
                        : "[]";
        return $$"""
        {
            "event": "{{state.Event}}",
            "lobbyId": "{{state.Id}}",
            "name": "{{state.Name}}",
            "players": {{state.Players}},
            "maxPlayers": {{state.MaxPlayers}},
            "mods": {{modsJson}}
        }
        """;
    }

    public static string CloseAndJSON(LobbyState state)
    {
        state.Event = LobbyEvent.close;
        return $$"""
        {
            "event": "{{state.Event}}",
            "lobbyId": "{{state.Id}}"
        }
        """;
        
    }
};




public class POST
{
    public static string ServerUrl = "http://localhost:8080/lobby";
    public static HttpClient client = new HttpClient();


    public static void WakeUpBot()
    {
        // For now i dont think i will use this, but idk
    }

    public static void StartLobby(LobbyState state)
    {
        Task.Run(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ServerUrl)
                {
                    Content = new StringContent(LobbyState.ToJSON(state), Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request);

            }
            catch (Exception ex)
            {
                LobbyAnnouncer.ModPlugin.Logger.LogDebug("Error sending lobby state: " + ex.Message);
            }
        });
    }

    public static void UpdateLobby(LobbyState state)
    {
        // TODO: IMplement this later
    }

    public static void CloseLobby(LobbyState state)
    {
        if (state == null) return;
        
        Task.Run(async () =>
        {

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ServerUrl)
                {
                    Content = new StringContent(LobbyState.CloseAndJSON(state), Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request);

            }
            catch (Exception ex)
            {
                LobbyAnnouncer.ModPlugin.Logger.LogDebug("Error sending lobby state: " + ex.Message);
            }
        });
    }
}