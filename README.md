# Pokemon Team Builder

A WPF application for building and analyzing Pokémon teams, supporting multiple game generations and leveraging a local SQLite database for Pokémon data.

## Features

- Build and customize Pokémon teams for different games and generations.
- View detailed information for each Pokémon.
- Analyze team composition, including type effectiveness and coverage.
- Supports exclusivity groups and Pokémon availability per game.
- Uses a local SQLite database for fast, offline access to Pokémon data.
- .NET 9 and WPF architecture.

## Installation

1. **Requirements:**
   - [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
   - Windows 10 (version 17763.0 or later)

2. **Clone the repository:**


3. **Build the project:**
   Open the solution in Visual Studio 2022 and build.

   
4. **Run the application:**


## Download the Latest Build
Alterbatively you can download the latest build from the [Releases](https://github.com/Burnpng/PokemonTeamBuilder/releases) page.



   
## Usage

- Launch the application.
- Select a game.
- Add Pokémon to your team from the list.
- Click Suggest Team button to get team compositions.

## Dependencies

- [Microsoft.EntityFrameworkCore 9.0.5](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore)
- [Microsoft.EntityFrameworkCore.Sqlite 9.0.5](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite)
- [Microsoft.EntityFrameworkCore.Tools 9.0.5](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Tools)

## Database

- The application uses a local `pokemon.db` SQLite database.
- Pokémon sprites and type icons are included in the `sprites` directory.

*Built with .NET 9 and WPF.*

   
   
