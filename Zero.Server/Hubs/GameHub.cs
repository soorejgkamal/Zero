using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Zero.Shared.Models;
using Zero.Shared.DTOs;
using Zero.Server.Services;

namespace Zero.Server.Hubs;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private static readonly ConcurrentDictionary<string, string> _connectionToRoom = new();
    private static readonly ConcurrentDictionary<string, string> _connectionToPlayer = new();

    public async Task CreateRoom(string playerName)
    {
        playerName = playerName?.Trim() ?? "";
        if (string.IsNullOrEmpty(playerName) || playerName.Length > 30)
        {
            await Clients.Caller.SendAsync("Error", "Invalid player name");
            return;
        }
        var roomId = GenerateRoomCode();
        var room = new GameRoom { RoomId = roomId };
        var player = new Player 
        { 
            Id = Guid.NewGuid().ToString(), 
            Name = playerName,
            ConnectionId = Context.ConnectionId
        };
        room.Players.Add(player);
        _rooms[roomId] = room;
        _connectionToRoom[Context.ConnectionId] = roomId;
        _connectionToPlayer[Context.ConnectionId] = player.Id;
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Caller.SendAsync("RoomCreated", roomId, player.Id);
        await BroadcastGameState(room, Context.ConnectionId);
    }

    public async Task JoinRoom(string roomId, string playerName)
    {
        playerName = playerName?.Trim() ?? "";
        if (string.IsNullOrEmpty(playerName) || playerName.Length > 30)
        {
            await Clients.Caller.SendAsync("Error", "Invalid player name");
            return;
        }
        roomId = roomId?.Trim().ToUpper() ?? "";
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", "Room not found");
            return;
        }
        if (room.Phase != GamePhase.Lobby)
        {
            var existingPlayer = room.Players.FirstOrDefault(p => p.Name == playerName);
            if (existingPlayer != null)
            {
                existingPlayer.ConnectionId = Context.ConnectionId;
                existingPlayer.IsConnected = true;
                _connectionToRoom[Context.ConnectionId] = roomId;
                _connectionToPlayer[Context.ConnectionId] = existingPlayer.Id;
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                await Clients.Caller.SendAsync("RoomJoined", roomId, existingPlayer.Id);
                await BroadcastGameState(room, Context.ConnectionId);
                return;
            }
            await Clients.Caller.SendAsync("Error", "Game already in progress");
            return;
        }
        if (room.Players.Count >= 6)
        {
            await Clients.Caller.SendAsync("Error", "Room is full");
            return;
        }
        
        var player = new Player 
        { 
            Id = Guid.NewGuid().ToString(), 
            Name = playerName,
            ConnectionId = Context.ConnectionId
        };
        room.Players.Add(player);
        _connectionToRoom[Context.ConnectionId] = roomId;
        _connectionToPlayer[Context.ConnectionId] = player.Id;
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Caller.SendAsync("RoomJoined", roomId, player.Id);
        await BroadcastGameState(room, Context.ConnectionId);
    }

    public async Task StartGame(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;
        if (room.Players.Count < 2) 
        {
            await Clients.Caller.SendAsync("Error", "Need at least 2 players");
            return;
        }
        
        room.Phase = GamePhase.Playing;
        room.Deck = GameEngine.CreateDeck();
        GameEngine.DealCards(room);
        room.CurrentPlayerIndex = 0;
        room.TableSets.Clear();
        room.DiscardPile.Clear();
        room.HasDrawnThisTurn = false;
        
        await Clients.Group(roomId).SendAsync("GameStarted");
        await BroadcastGameState(room, null);
    }

    public async Task DrawCard()
    {
        if (!GetCurrentRoomAndPlayer(out var room, out var player)) return;
        if (room!.Phase != GamePhase.Playing) return;
        
        var currentPlayer = room.Players[room.CurrentPlayerIndex];
        if (currentPlayer.Id != player!.Id) 
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }
        if (room.HasDrawnThisTurn)
        {
            await Clients.Caller.SendAsync("Error", "Already drawn this turn");
            return;
        }
        
        if (room.Deck.Count == 0)
        {
            room.Deck = room.DiscardPile.OrderBy(_ => Guid.NewGuid()).ToList();
            room.DiscardPile.Clear();
            if (room.Deck.Count == 0)
            {
                await Clients.Caller.SendAsync("Error", "No cards available");
                return;
            }
        }
        
        var card = room.Deck[0];
        room.Deck.RemoveAt(0);
        currentPlayer.Hand.Add(card);
        room.LastDrawPlayerId = currentPlayer.Id;
        room.HasDrawnThisTurn = true;
        
        await BroadcastGameState(room, Context.ConnectionId, card.Id);
    }

    public async Task DiscardCard(string cardId)
    {
        if (!GetCurrentRoomAndPlayer(out var room, out var player)) return;
        if (room!.Phase != GamePhase.Playing) return;
        
        var currentPlayer = room.Players[room.CurrentPlayerIndex];
        if (currentPlayer.Id != player!.Id)
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }
        if (!room.HasDrawnThisTurn)
        {
            await Clients.Caller.SendAsync("Error", "Must draw before discarding");
            return;
        }
        
        var card = currentPlayer.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null)
        {
            await Clients.Caller.SendAsync("Error", "Card not found in hand");
            return;
        }
        
        currentPlayer.Hand.Remove(card);
        room.DiscardPile.Add(card);
        
        if (currentPlayer.Hand.Count == 0)
        {
            await EndRound(room, currentPlayer.Id);
            return;
        }
        
        AdvanceTurn(room);
        await BroadcastGameState(room, null);
    }

    public async Task OpenSet(List<string> cardIds)
    {
        if (!GetCurrentRoomAndPlayer(out var room, out var player)) return;
        if (room!.Phase != GamePhase.Playing) return;
        
        var currentPlayer = room.Players[room.CurrentPlayerIndex];
        if (currentPlayer.Id != player!.Id)
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }
        
        var cards = cardIds
            .Select(id => currentPlayer.Hand.FirstOrDefault(c => c.Id == id))
            .Where(c => c != null)
            .Cast<Card>()
            .ToList();
        
        if (cards.Count != cardIds.Count)
        {
            await Clients.Caller.SendAsync("Error", "Some cards not found in hand");
            return;
        }
        
        if (!GameEngine.IsValidSequence(cards))
        {
            await Clients.Caller.SendAsync("Error", "Invalid sequence");
            return;
        }
        
        var sortedCards = GameEngine.SortSequenceCards(cards);
        
        foreach (var card in cards)
            currentPlayer.Hand.Remove(card);
        
        var sequence = new Sequence
        {
            Id = Guid.NewGuid().ToString(),
            Cards = sortedCards,
            PlacedByPlayerId = currentPlayer.Id
        };
        room.TableSets.Add(sequence);
        
        if (currentPlayer.Hand.Count == 0)
        {
            await EndRound(room, currentPlayer.Id);
            return;
        }
        
        await BroadcastGameState(room, null);
    }

    public async Task AddToSet(string sequenceId, string cardId, bool addToLeft)
    {
        if (!GetCurrentRoomAndPlayer(out var room, out var player)) return;
        if (room!.Phase != GamePhase.Playing) return;
        
        var actingPlayer = room.Players.FirstOrDefault(p => p.Id == player!.Id);
        if (actingPlayer == null) return;
        
        var currentPlayer = room.Players[room.CurrentPlayerIndex];
        if (currentPlayer.Id != player!.Id)
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }
        
        var sequence = room.TableSets.FirstOrDefault(s => s.Id == sequenceId);
        if (sequence == null)
        {
            await Clients.Caller.SendAsync("Error", "Sequence not found");
            return;
        }
        
        var card = actingPlayer.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null)
        {
            await Clients.Caller.SendAsync("Error", "Card not found in hand");
            return;
        }
        
        bool canAdd = addToLeft 
            ? GameEngine.CanAddToLeft(sequence, card) 
            : GameEngine.CanAddToRight(sequence, card);
        
        if (!canAdd)
        {
            await Clients.Caller.SendAsync("Error", "Cannot add card to that position");
            return;
        }
        
        actingPlayer.Hand.Remove(card);
        if (addToLeft)
            sequence.Cards.Insert(0, card);
        else
            sequence.Cards.Add(card);
        
        if (actingPlayer.Hand.Count == 0)
        {
            await EndRound(room, actingPlayer.Id);
            return;
        }
        
        await BroadcastGameState(room, null);
    }

    public async Task EndTurn()
    {
        if (!GetCurrentRoomAndPlayer(out var room, out var player)) return;
        if (room!.Phase != GamePhase.Playing) return;
        
        var currentPlayer = room.Players[room.CurrentPlayerIndex];
        if (currentPlayer.Id != player!.Id)
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }
        if (!room.HasDrawnThisTurn)
        {
            await Clients.Caller.SendAsync("Error", "Must draw a card before ending turn");
            return;
        }
        
        AdvanceTurn(room);
        await BroadcastGameState(room, null);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionToRoom.TryGetValue(Context.ConnectionId, out var roomId) &&
            _connectionToPlayer.TryGetValue(Context.ConnectionId, out var playerId))
        {
            if (_rooms.TryGetValue(roomId, out var room))
            {
                var player = room.Players.FirstOrDefault(p => p.Id == playerId);
                if (player != null)
                    player.IsConnected = false;
                await BroadcastGameState(room, null);
            }
            _connectionToRoom.TryRemove(Context.ConnectionId, out _);
            _connectionToPlayer.TryRemove(Context.ConnectionId, out _);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task EndRound(GameRoom room, string winnerId)
    {
        room.Phase = GamePhase.RoundEnd;
        room.WinnerId = winnerId;
        
        foreach (var p in room.Players.Where(p => !p.IsEliminated))
        {
            if (p.Id != winnerId)
            {
                p.Score += GameEngine.CalculateHandScore(p.Hand);
                if (p.Score >= 100)
                    p.IsEliminated = true;
            }
        }
        
        var activePlayers = room.Players.Where(p => !p.IsEliminated).ToList();
        if (activePlayers.Count <= 1)
        {
            room.Phase = GamePhase.GameOver;
        }
        
        await Clients.Group(room.RoomId).SendAsync("RoundEnded", winnerId);
        if (room.Phase == GamePhase.GameOver)
            await Clients.Group(room.RoomId).SendAsync("PlayerEliminated", room.Players.Where(p => p.IsEliminated).Select(p => p.Id).ToList());
        
        await BroadcastGameState(room, null);
    }

    private void AdvanceTurn(GameRoom room)
    {
        room.HasDrawnThisTurn = false;
        var activePlayers = room.Players.Where(p => !p.IsEliminated).ToList();
        if (activePlayers.Count == 0) return;
        
        var currentIdx = activePlayers.FindIndex(p => p.Id == room.Players[room.CurrentPlayerIndex].Id);
        var nextIdx = (currentIdx + 1) % activePlayers.Count;
        room.CurrentPlayerIndex = room.Players.IndexOf(activePlayers[nextIdx]);
    }

    private bool GetCurrentRoomAndPlayer(out GameRoom? room, out Player? player)
    {
        room = null;
        player = null;
        if (!_connectionToRoom.TryGetValue(Context.ConnectionId, out var roomId)) return false;
        if (!_connectionToPlayer.TryGetValue(Context.ConnectionId, out var playerId)) return false;
        if (!_rooms.TryGetValue(roomId, out room)) return false;
        player = room.Players.FirstOrDefault(p => p.Id == playerId);
        return player != null;
    }

    private async Task BroadcastGameState(GameRoom room, string? callerConnectionId, string? newlyDrawnCardId = null)
    {
        foreach (var p in room.Players.Where(p => p.IsConnected && !string.IsNullOrEmpty(p.ConnectionId)))
        {
            var state = BuildGameState(room, p.Id, newlyDrawnCardId);
            await Clients.Client(p.ConnectionId).SendAsync("GameStateUpdated", state);
        }
    }

    private GameStateDto BuildGameState(GameRoom room, string forPlayerId, string? newlyDrawnCardId = null)
    {
        var currentPlayer = room.Phase == GamePhase.Playing && room.Players.Count > room.CurrentPlayerIndex
            ? room.Players[room.CurrentPlayerIndex]
            : null;
        
        return new GameStateDto
        {
            RoomId = room.RoomId,
            DeckCount = room.Deck.Count,
            TableSets = room.TableSets,
            CurrentPlayerId = currentPlayer?.Id ?? "",
            Phase = room.Phase,
            WinnerId = room.WinnerId,
            HasDrawnThisTurn = room.HasDrawnThisTurn,
            NewlyDrawnCardId = newlyDrawnCardId,
            Players = room.Players.Select(p => new PlayerDto
            {
                Id = p.Id,
                Name = p.Name,
                CardCount = p.Hand.Count,
                Score = p.Score,
                IsEliminated = p.IsEliminated,
                IsConnected = p.IsConnected,
                Hand = p.Id == forPlayerId ? p.Hand : new List<Card>()
            }).ToList()
        };
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
