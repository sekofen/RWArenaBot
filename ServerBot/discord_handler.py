from enum import Enum
import logging
import discord
from server import LobbyState

log = logging.getLogger("lobbybot.discord")


COLOR_OPEN = 0x2ECC71 # green
COLOR_FULL = 0xE67E22 # orange
COLOR_CLOSED = 0x3B1212 # dark red

class MsgResult(Enum):
    CREATED = "created"
    ALREADY_PUBLISHED = "already_published"
    NOT_PUBLISHED = "not_published"
    UPDATED = "updated"
    ERROR = "error"

def esc(text: str) -> str:
    return discord.utils.escape_markdown(text)



def buildMsg(lobby: LobbyState) -> discord.Embed:
    name = lobby.Name
    players = lobby.Players
    max_players = lobby.MaxPlayers
    mods = lobby.Mods

    full = (players >= max_players)

    embed = discord.Embed(
        title=name,
        description=f"**{players}/{max_players}** | `{"█" * players + "─" * (max_players - players)}` ",
        color=COLOR_FULL if full else COLOR_OPEN,
        timestamp=discord.utils.utcnow(),
    )
    embed.add_field(name="Status", value="🟠 Full" if full else "🟢 Open")

    mods_str = "\n".join(f"• {esc(m)}" for m in mods) or "Vainilla"
    embed.add_field(name="Mods", value=mods_str[:1024], inline=False)
    return embed


async def publishLobbyMsg(channel: discord.abc.Messageable, lobby: LobbyState) -> MsgResult:
    if channel.id in lobby.Channels_id:
        return MsgResult.ALREADY_PUBLISHED
    
    try:
        embed = buildMsg(lobby)
        lobby.Channels_id[channel.id] = await channel.send(embed=embed)
        return MsgResult.CREATED
    
    except discord.HTTPException:
        log.exception("Failed to publish lobby %s in the channel %s", lobby.Id, channel.id)
        return MsgResult.ERROR    

async def updateLobbyMsg(lobby: LobbyState) -> MsgResult:
    embed = buildMsg(lobby)

    for msg in lobby.Channels_id.values():
        try:
            await msg.edit(embed=embed)
        except (discord.NotFound, discord.HTTPException):
            log.exception("Failed to update lobby %s in the channel %s", lobby.Id, msg.channel.id)



async def closeLobbyMsg(lobby: LobbyState):
    embed = discord.Embed(
            title="🔴 Lobby Closed",
            description=esc(lobby.Name),
            color=COLOR_CLOSED,
            timestamp=discord.utils.utcnow(),
        )
    
    for ch_id, msg in lobby.Channels_id.items():
        try:
            await msg.edit(embed=embed)
        except discord.NotFound:
            pass
        except discord.HTTPException:
            log.exception("Failed to close lobby %s in the channel %s", lobby.Id, ch_id)
    lobby.Channels_id.clear()