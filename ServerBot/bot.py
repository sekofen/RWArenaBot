import asyncio
import logging
import os
import time
from pathlib import Path

import discord
from discord import app_commands
from discord_handler import MsgResult, closeLobbyMsg, publishLobbyMsg, updateLobbyMsg
from server import HOST, PORT, LobbyState, start_http_server

from dotenv import load_dotenv
load_dotenv(Path(__file__).parent / ".env")  # antes de importar server
TOKEN = os.environ.get("DISCORD_TOKEN", "")
NO_MENTIONS = discord.AllowedMentions.none()
LOBBY_TIMEOUT = 30 * 60   


log = logging.getLogger("lobbybot")


SHOW_REPLIES = {
    MsgResult.CREATED: "✅ Done, the lobby is posted ^_^.",
    MsgResult.ALREADY_PUBLISHED: "ℹ️ Look above, i already posted this lobby",
    MsgResult.ERROR: "❌ Sorry, tell seko to fix the damm server (i don't know why this failed)",
}


class LobbyBot(discord.Client):
    def __init__(self):
        super().__init__(intents=discord.Intents.default(), allowed_mentions=NO_MENTIONS)
        self.tree = app_commands.CommandTree(self)
        self.lobbies: dict[str, LobbyState] = {}
        self.lock = asyncio.Lock()
        self._task = None
        self._server = None

    async def setup_hook(self):
        self._server = await start_http_server(self, HOST, PORT)
        self._task = asyncio.create_task(self.maintenance())
        await self.tree.sync()
        log.info("Slash commands synced with Discord.")

    async def makeLobby(self, data: LobbyState) -> str:
        async with self.lock:
            self.lobbies[str(data.Id)] = data
        return str(data.Id)

    async def updateLobby(self, lobby_id: str, players: list[str]):
        async with self.lock:
            lobby = self.lobbies.get(str(lobby_id))
            if lobby:
                lobby.Players = players
                lobby.Last_update = time.time()
        if lobby:
            await updateLobbyMsg(lobby)

    async def closeLobby(self, lobby_id: str) -> bool:
        async with self.lock:
            lobby = self.lobbies.pop(str(lobby_id), None)
        if lobby:
            await closeLobbyMsg(lobby)
        return lobby is not None

    async def maintenance(self):
        while True:
            await asyncio.sleep(10)
            try:
                now = time.time()
                to_close = []
                async with self.lock:
                    for lid, lobby in list(self.lobbies.items()):
                        if now - lobby.Last_update > LOBBY_TIMEOUT:
                            to_close.append(lid)
                            
                for lid in to_close:
                    await self.closeLobby(lid)
                    log.info("Lobby %s closed due to inactivity", lid)
            except Exception:
                log.exception("Error in maintenance loop")

    async def close(self):
        if self._task:
            self._task.cancel()
        if self._server:
            await self._server.cleanup()
        await super().close()


bot = LobbyBot()


@bot.tree.command(name="lobby", description="Post a Rain World lobby in this channel")
@app_commands.rename(code="id")
@app_commands.describe(code="4-digit lobby ID shown in-game")
@app_commands.guild_only()
@app_commands.checks.cooldown(1, 10)
async def showLobby(interaction: discord.Interaction, code: app_commands.Range[int, 1000, 9999]):
    await interaction.response.defer(ephemeral=True)
    lobby = bot.lobbies.get(str(code))
    if lobby is None:
        await interaction.followup.send("I did not found any lobby with this ID :(")
        return
    
    result = await publishLobbyMsg(interaction.channel, lobby)
    await interaction.followup.send(SHOW_REPLIES.get(result, "Command processed."))


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO,
                        format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    bot.run(TOKEN)
