using System;
using BepInEx;
using RainMeadow;
using MonoMod.RuntimeDetour;
using System.Security.Permissions;
using BepInEx.Logging;
using System.Reflection;
using System.Data;

#pragma warning disable CS0618 // SecurityAction.RequestMinimum is obsolete. However, this does not apply to the mod, which still needs it. Suppress the warning indicating that it is obsolete.
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace LobbyAnnouncer;

[BepInPlugin(MODID, MODNAME, MODVERSION)]

public class ModPlugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger;

    public const string MODID = "seko.arenabotlobby";
    public const string MODNAME = "Arena Bot Lobby";
    public const string MODVERSION = "0.1";

    bool IsInit;

    public void OnEnable()
    {
        Logger = base.Logger;
        On.RainWorld.OnModsInit += OnModsInit;
    }

    private static Server.LobbyState lobbyState;

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (IsInit) return;
        IsInit = true;

        try
        {
            
            new Hook(
                typeof(LobbyCreateMenu).GetMethod("CreateLobby", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                typeof(ModPlugin).GetMethod(nameof(ModPlugin.MakingLobbyNotify), BindingFlags.Static | BindingFlags.Public)
            );

            new Hook(
                typeof(OnlineManager).GetMethod(nameof(OnlineManager.LeaveLobby), BindingFlags.Static | BindingFlags.Public),
                typeof(ModPlugin).GetMethod(nameof(ModPlugin.CloseLobbyNotify), BindingFlags.Static | BindingFlags.Public)
            );

            new Hook(
                typeof(Lobby).GetMethod("NewParticipant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                typeof(ModPlugin).GetMethod(nameof(ModPlugin.PlayerJoinedNotify), BindingFlags.Static | BindingFlags.Public)
            );

            new Hook(
                typeof(Lobby).GetMethod("ParticipantLeftImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                typeof(ModPlugin).GetMethod(nameof(ModPlugin.PlayerLeftNotify), BindingFlags.Static | BindingFlags.Public)
            );



        }
        catch (Exception e)
        {
            Logger.LogDebug($"Error in OnModsInit: {e.Message}");
        }
        Server.POST.WakeUpBot();

        // Initialize assets, your mod config, and anything that uses RainWorld here
        Logger.LogDebug($"[{MODID}] {MODNAME} Hello world! {MODVERSION}");
    }

    public static void MakingLobbyNotify(Action<LobbyCreateMenu, SimplerButton> orig, LobbyCreateMenu self, SimplerButton obj)
    {
        orig(self, obj);

        //send information about the lobby
        // max player, mod used.

        lobbyState = new Server.LobbyState
        {
            Name = OnlineManager.mePlayer.id.DisplayName,
            Mods = Server.LobbyState.DetectMods(),
            Players = 1, // the host is alone when starting the lobby
            MaxPlayers = self.maxPlayerCount,
        };

        Server.POST.StartLobby(lobbyState);


        Logger.LogDebug($"Lobby name, max-players: {lobbyState.MaxPlayers}");
    }

    public static void CloseLobbyNotify(Action orig)
    {
        // Notify the bot that a lobby has been closed
        orig();
        Server.POST.CloseLobby(lobbyState);

        Logger.LogDebug("Closing lobby");
    }

    void OnApplicationQuit()
    {
        Server.POST.CloseLobby(lobbyState);
        Logger.LogDebug("Closing lobby in application quit");
    }

       private static void PlayerLeftNotify(Action<Lobby, OnlinePlayer> orig, Lobby self, OnlinePlayer player)
    {
        orig(self, player);
        if (lobbyState == null) return;
        Server.LobbyState.UpdateLobbyPLayers(-1, lobbyState);
    }

    private static void PlayerJoinedNotify(Action<Lobby, OnlinePlayer> orig , Lobby self, OnlinePlayer player)
    {
        orig(self, player);

        if (!self.isOwner) return;
        if (lobbyState == null) return;
        Server.LobbyState.UpdateLobbyPLayers(1, lobbyState);
    }
}
