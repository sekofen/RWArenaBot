using System;
using BepInEx;
using RainMeadow;
using MonoMod.RuntimeDetour;
using System.Security.Permissions;
using BepInEx.Logging;
using System.Reflection;

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

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (IsInit) return;
        IsInit = true;

        new Hook(
            typeof(LobbyCreateMenu).GetMethod("LobbyCreateMenu.CreateLobby", BindingFlags.NonPublic | BindingFlags.Instance),
            typeof(ModPlugin).GetMethod(nameof(ModPlugin.MakingLobbyNotify))
        );

        new Hook(
            typeof(OnlineManager).GetMethod(nameof(OnlineManager.LeaveLobby)),
            typeof(ModPlugin).GetMethod(nameof(ModPlugin.CloseLobbyNotify))
        );

        Server.POST.Init();

        // Initialize assets, your mod config, and anything that uses RainWorld here
        Logger.LogDebug($"[{MODID}] {MODNAME} Hello world! {MODVERSION}");
    }

    private static void MakingLobbyNotify(Action<LobbyCreateMenu, SimplerButton> orig, LobbyCreateMenu self, SimplerButton obj)
    {
        orig(self, obj);

        //send information about the lobby
        // max player, mod used.

        Server.POST.Send(new Server.LobbyState
        {
            Name = "idk seko", //player name
            Mods = Server.LobbyState.DetectMods(),
            Players = 1, // the host is alone when starting the lobby
            MaxPlayers = self.maxPlayerCount,
        });
    }

    private static void CloseLobbyNotify(Action<OnlineManager> orig, OnlineManager self)
    {
        // Notify the bot that a lobby has been closed
        orig(self);
        Server.POST.Close();
    }

    void OnApplicationQuit()
    {
        Server.POST.Close();
    }
}
