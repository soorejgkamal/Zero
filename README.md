# Zero Card Game

A complete multiplayer card game web application built with .NET 10, Blazor WebAssembly, and SignalR.

## About the Game

Zero is a card game for 2-6 players where the goal is to be the last player remaining by keeping your cumulative score below 100 points across multiple rounds.

### Game Rules

- **Deck**: 2 standard 52-card decks + 2 jokers (106 cards total)
- **Players**: 2 to 6 players per game room
- **Starting Hand**: 10 cards per player
- **Turn Structure**:
  1. Discard 1 card face-down
  2. (Optional) Open a new sequence or add cards to existing sequences
  3. Draw 1 card from the deck
- **Sequences**: Minimum 3 cards of the same suit in consecutive rank order
- **Jokers**: Act as wildcards (worth 10 points)
- **Round Ends**: When a player has 0 cards in hand
- **Scoring**: Face cards & Aces = 10 points, Number cards = face value, Jokers = 10 points
- **Elimination**: Players with 100+ cumulative points are eliminated
- **Winner**: Last player remaining wins the game

### Special Rules

- Discarded cards are NOT visible to other players
- Any player can extend any sequence on the table (during their turn)
- When the deck is empty, the discard pile is shuffled to create a new deck
- Sequences can be extended on either end (lower or higher rank)

## Project Structure

```
Zero/
├── Zero.Shared/         # Shared models and DTOs
│   ├── Models/          # Card, Player, GameRoom, Sequence
│   └── DTOs/            # GameStateDto
├── Zero.Server/         # ASP.NET Core server
│   ├── Hubs/            # SignalR GameHub
│   └── Services/        # GameEngine logic
└── Zero.Client/         # Blazor WebAssembly client
    ├── Pages/           # Index (lobby) & Game pages
    ├── Shared/          # Reusable components
    ├── Services/        # GameService (SignalR client)
    └── Helpers/         # CardSorter utilities
```

## Technologies Used

- **.NET 10**: Latest .NET framework
- **Blazor WebAssembly**: Client-side SPA framework
- **SignalR**: Real-time communication
- **ASP.NET Core**: Server hosting

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Running the Application

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd Zero
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the solution**:
   ```bash
   dotnet build
   ```

4. **Run the server**:
   ```bash
   cd Zero.Server
   dotnet run
   ```

5. **Open in browser**:
   - Navigate to `http://localhost:5049` (or the URL shown in the console)
   - Create a room or join an existing room
   - Share the room code with other players
   - Start playing!

### Running Multiple Clients Locally

To test multiplayer functionality locally:

1. Start the server in one terminal:
   ```bash
   cd Zero.Server
   dotnet run
   ```

2. Open multiple browser windows/tabs to `http://localhost:5049`
3. Each window represents a different player

## How to Play

1. **Lobby**:
   - Enter your name
   - Create a new room (you'll get a 6-character room code)
   - Or join an existing room with a room code
   - Wait for 2-6 players to join
   - Host clicks "Start Game"

2. **During Your Turn**:
   - Select a card and click "Discard" to discard it
   - Select cards in your hand to form sequences (min 3 cards, same suit, consecutive)
   - Click "Open Set" to place a new sequence on the table
   - Or select a single card and use the "+" buttons on existing sequences to extend them
   - Click "Draw Card" to draw from the deck and end your turn

3. **Card Selection**:
   - Click cards in your hand to select/deselect them
   - Selected cards are highlighted
   - Newly drawn cards pulse briefly

4. **Round End**:
   - When a player empties their hand, the round ends
   - All other players' remaining cards are scored
   - Players with 100+ points are eliminated
   - Continue to next round or end game if only 1 player remains

## Features

- **Real-time Multiplayer**: Multiple players can join and play simultaneously
- **Responsive Design**: Works on desktop and mobile devices
- **Visual Feedback**: Card animations, turn indicators, score tracking
- **Auto-reconnection**: Disconnected players can rejoin with their name
- **Game State Management**: Server-authoritative game logic prevents cheating
- **Mobile-First UI**: Touch-friendly interface with smooth animations

## Development

### Project Commands

- **Build**: `dotnet build`
- **Run Server**: `cd Zero.Server && dotnet run`
- **Run Tests**: `dotnet test` (if tests are added)
- **Clean**: `dotnet clean`

### Architecture

- **Client-Server Model**: Server maintains game state, clients send actions
- **SignalR Hub**: Real-time bidirectional communication
- **Game Engine**: Server-side validation of all game actions
- **DTOs**: Personalized game state sent to each player (only shows their hand)

## License

This project is provided as-is for educational and entertainment purposes.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.
