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

    // Voice chat
    public event Action<List<VoicePeerDto>>? OnVoiceJoined;
    public event Action<string, string>? OnVoicePeerJoined;
    public event Action<string>? OnVoicePeerLeft;
    public event Action<string, string>? OnVoiceOffer;
    public event Action<string, string>? OnVoiceAnswer;
    public event Action<string, string>? OnVoiceIceCandidate;

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

        _hubConnection.On<List<VoicePeerDto>>("VoiceJoined", (peers) => OnVoiceJoined?.Invoke(peers));
        _hubConnection.On<string, string>("VoicePeerJoined", (connId, name) => OnVoicePeerJoined?.Invoke(connId, name));
        _hubConnection.On<string>("VoicePeerLeft", (connId) => OnVoicePeerLeft?.Invoke(connId));
        _hubConnection.On<string, string>("VoiceOffer", (from, sdp) => OnVoiceOffer?.Invoke(from, sdp));
        _hubConnection.On<string, string>("VoiceAnswer", (from, sdp) => OnVoiceAnswer?.Invoke(from, sdp));
        _hubConnection.On<string, string>("VoiceIceCandidate", (from, c) => OnVoiceIceCandidate?.Invoke(from, c));

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

    public async Task DiscardAndDrawAsync(string cardId)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("DiscardAndDraw", cardId);
    }

    public async Task OpenSetAsync(List<string> cardIds)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("OpenSet", cardIds);
    }

    public async Task AddToSetAsync(string sequenceId, List<string> cardIds, bool addToLeft)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("AddToSet", sequenceId, cardIds, addToLeft);
    }

    public async Task JoinVoiceAsync()
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("JoinVoice");
    }

    public async Task LeaveVoiceAsync()
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("LeaveVoice");
    }

    public async Task SendVoiceOfferAsync(string targetConnectionId, string sdp)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("SendVoiceOffer", targetConnectionId, sdp);
    }

    public async Task SendVoiceAnswerAsync(string targetConnectionId, string sdp)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("SendVoiceAnswer", targetConnectionId, sdp);
    }

    public async Task SendVoiceIceCandidateAsync(string targetConnectionId, string candidate)
    {
        if (_hubConnection == null) return;
        await _hubConnection.SendAsync("SendVoiceIceCandidate", targetConnectionId, candidate);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
