from enum import Enum
import logging
import os
import random
import time
from collections import defaultdict, deque
from aiohttp import web

HOST = os.getenv("HOST", "0.0.0.0")
PORT = int(os.getenv("PORT", "8080"))


log = logging.getLogger("lobbybot.server")
# Request rate limiting per IP
_hits = defaultdict(deque)

class LobbyEvent(str, Enum):
    OPEN = "open"
    UPDATE = "update"
    CLOSE = "close"

class LobbyState():
    def __init__(self, id: str, name: str, players: int, max_players: int, mods: list[str], event: LobbyEvent):
        self.Id = id
        self.Name = name
        self.Players = players
        self.MaxPlayers = max_players
        self.Mods = mods
        self.Event = event
        self.Channels_id = {}
        self.Last_update = time.time()

    def parseJSON(self, JSON: dict):
        return LobbyState(
            id=JSON.get("lobbyId", ""),
            name=JSON.get("name", "Default"),
            players=JSON.get("players", 0),
            max_players=JSON.get("maxPlayers", 0),
            mods=JSON.get("mods", []),
            event=LobbyEvent(JSON.get("event", "open"))
        )

def generateLobby(JSON: dict):
    return LobbyState(
        id=str(random.randint(1000, 9999)), 
        name=JSON.get("name", "Default"),
        players=JSON.get("players", 0),
        max_players=JSON.get("maxPlayers", 0),
        mods=JSON.get("mods", []),
        event=LobbyEvent.OPEN
    )


# ---------------------------------------------------------------- IP & Rate limiting
def get_client_ip(request: web.Request) -> str:
    fwd = request.headers.get("CF-Connecting-IP") or request.headers.get("X-Forwarded-For", "")
    if fwd:                                                                                             
        return fwd.split(",")[0].strip()
    return request.remote or "unknown"


def is_rate_ok(ip: str) -> bool:
    now = time.monotonic()
    q = _hits[ip]
    while q and now - q[0] > 60:
        q.popleft()
    if len(q) >= 60:
        log.warning("Rate limit exceeded for IP %s (max %d requests/min)", ip, 60)
        return False
    q.append(now)
    return True

# ---------------------------------------------------------------- HTTP Handlers
async def handle_health(request: web.Request) -> web.Response:
    bot = request.app["bot"]
    return web.json_response({"ok": True, "lobbies": len(bot.lobbies)})


async def handle_lobby(request: web.Request) -> web.Response:
    bot = request.app["bot"]
    ip = get_client_ip(request)

    # Rate limiting by IP
    if not is_rate_ok(ip):
        return web.json_response({"error": "rate limited"}, status=429)

    JSON = await request.json()
    event = JSON.get("event")

    if event == LobbyEvent.OPEN:
        id = await bot.makeLobby(generateLobby(JSON))
        log.info(f"Lobby {id} opened")
        return web.json_response({"ok": True, "id": id})

    if event == LobbyEvent.CLOSE:
        await bot.closeLobby(JSON.get("lobbyId"))
        log.info(f"Lobby {JSON.get('lobbyId')} closed")
        return web.json_response({"ok": True})

    if event == LobbyEvent.UPDATE:
        id, players = JSON.get("lobbyId"), JSON.get("players")
        await bot.updateLobby(id, players)
        log.info(f"Lobby {id} updated: {players} players")
        return web.json_response({"ok": True})


# ---------------------------------------------------------------- Initialization
def create_app(bot) -> web.Application:
    app = web.Application(client_max_size=8192)
    app["bot"] = bot
    app.router.add_post("/lobby", handle_lobby)
    app.router.add_get("/health", handle_health)
    return app


async def start_http_server(bot, host: str = HOST, port: int = PORT) -> web.AppRunner:
    app = create_app(bot)
    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, host, port)
    await site.start()
    log.info("HTTP server listening on %s:%s", host, port)
    return runner
