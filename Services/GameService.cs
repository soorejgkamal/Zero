using Microsoft.AspNetCore.SignalR.Client;
using Zero.Models;

namespace Zero.Services;

public class GameService
{
    private HubConnection? _hubConnection;
    private string? _playerId;
    private string? _roomId;

    public event Action<GameStateDto>? OnGameStateUpdated;
    public event Action<string, string>? OnRoomCreated;
    public event Action<string, string>? OnRoomJoined;
    public event Action? OnGameStarted;
    public event Action<string>? OnRoundEnded;
    public event Action<List<string>>? OnPlayerEliminated;
    public event Action<string>? OnError;

    public string? PlayerId => _playerId;
    public string? RoomId => _roomId;
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public GameStateDto? CurrentGameState { get; private set; }

    public async Task InitializeAsync(string hubUrl)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<GameStateDto>("GameStateUpdated", (state) =>
        {
            CurrentGameState = state;
            OnGameStateUpdated?.Invoke(state);
        });

        _hubConnection.On<string, string>("RoomCreated", (roomId, playerId) =>
        {
            _roomId = roomId;
            _playerId = playerId;
            OnRoomCreated?.Invoke(roomId, playerId);
        });

        _hubConnection.On<string, string>("RoomJoined", (roomId, playerId) =>
        {
            _roomId = roomId;
            _playerId = playerId;
            OnRoomJoined?.Invoke(roomId, playerId);
        });

        _hubConnection.On("GameStarted", () =>
        {
            OnGameStarted?.Invoke();
        });

        _hubConnection.On<string>("RoundEnded", (winnerId) =>
        {
            OnRoundEnded?.Invoke(winnerId);
        });

        _hubConnection.On<List<string>>("PlayerEliminated", (playerIds) =>
        {
            OnPlayerEliminated?.Invoke(playerIds);
        });

        _hubConnection.On<string>("Error", (message) =>
        {
            OnError?.Invoke(message);
        });

        await _hubConnection.StartAsync();
    }

    public async Task CreateRoomAsync(string playerName, int maxPlayers = 6)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("CreateRoom", playerName, maxPlayers);
    }

    public async Task JoinRoomAsync(string roomId, string playerName)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("JoinRoom", roomId, playerName);
    }

    public async Task StartGameAsync(string roomId)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("StartGame", roomId);
    }

    public async Task DrawCardAsync()
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("DrawCard");
    }

    public async Task DiscardCardAsync(string cardId)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("DiscardCard", cardId);
    }

    public async Task OpenSetAsync(List<string> cardIds)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("OpenSet", cardIds);
    }

    public async Task AddToSetAsync(string sequenceId, string cardId, bool addToLeft)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("AddToSet", sequenceId, cardId, addToLeft);
    }

    public async Task EndTurnAsync()
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("EndTurn");
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
