using PokemonTeamBuilder;
using System;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;

namespace PokemonTeamBuilder
{
    public partial class MainWindow : Window
    {
        private readonly PokemonDbContext _dbContext;
        private ICollectionView _pokemonCollectionView = null!;
        private readonly ObservableCollection<Pokemon> Team = [];
        private readonly List<Pokemon?> CurrentTeam = [.. new Pokemon[6]];
        private int SelectedTeamIndex = -1;


        public MainWindow()
        {
            InitializeComponent();
            IncludeLegendaries.Checked += IncludeLegendaries_CheckedChanged;
            IncludeLegendaries.Unchecked += IncludeLegendaries_CheckedChanged;
            IncludeMythicals.Checked += IncludeMythicals_CheckedChanged;
            IncludeMythicals.Unchecked += IncludeMythicals_CheckedChanged;
            AddPokemonList.SelectionChanged += AddPokemonList_SelectionChanged;

            _dbContext = new PokemonDbContext();
            LoadGames();

            // Initialize 6 empty team slots
            for (int i = 0; i < 6; i++)
            {
                var slot = new TeamSlotControl { SlotIndex = i };
                slot.SetPokemon(null);
                CurrentTeamGrid.Children.Add(slot);
            }
        }

        private void LoadGames()
        {
            var games = _dbContext.Games
                .OrderBy(g => g.GameId)
                .ToList();

            GameSelector.ItemsSource = games;
            GameSelector.DisplayMemberPath = "Name";
            GameSelector.SelectedValuePath = "GameId";

            if (games.Count > 0)
                GameSelector.SelectedIndex = 0;
        }

        private void LoadAvailablePokemon()
        {
            var selectedGame = GameSelector.SelectedItem as Game;
            if (selectedGame == null)
                return;

            var includeLegendaries = IncludeLegendaries.IsChecked ?? false;
            var includeMythicals = IncludeMythicals.IsChecked ?? false;

            var availablePokemon = _dbContext.PokemonAvailability
                .Where(pa => pa.GameId == selectedGame.GameId)
                .Select(pa => pa.Pokemon)
                .Where(p => p != null &&
                           (includeLegendaries || !p.IsLegendary) &&
                           (includeMythicals || !p.IsMythical))
                .OrderBy(p => p.DexID)
                .ToList();

            _pokemonCollectionView = CollectionViewSource.GetDefaultView(availablePokemon);
            AddPokemonList.ItemsSource = _pokemonCollectionView;
            FilterMutuallyExclusivePokemon();
            ClearUnavailableTeamPokemon();

        }

        private void AddPokemonList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddPokemonList.SelectedItem is Pokemon selectedPokemon)
            {
                PokemonNameAndDex.Text = $"{selectedPokemon.Name} / #{selectedPokemon.DexID}";
                PokemonPreviewImage.Source = new BitmapImage(new Uri(GetPokemonSpritePath(selectedPokemon)));

                PokemonTypeIcons.Children.Clear();
                if (!string.IsNullOrEmpty(selectedPokemon.Type1))
                    PokemonTypeIcons.Children.Add(CreateTypeIcon(selectedPokemon.Type1));
                if (!string.IsNullOrEmpty(selectedPokemon.Type2))
                    PokemonTypeIcons.Children.Add(CreateTypeIcon(selectedPokemon.Type2));
            }
            else
            {
                PokemonNameAndDex.Text = "";
                PokemonPreviewImage.Source = null;
                PokemonTypeIcons.Children.Clear();
            }

            // Clear team slot visual highlights
            foreach (var child in CurrentTeamGrid.Children)
            {
                if (child is TeamSlotControl slot)
                    slot.IsSelected = false;
            }

            // Reset selected team index to prevent unintended replaces
            SelectedTeamIndex = -1;

            // Enable Add only if a Pokémon is selected and there's room
            AddPokemonButton.IsEnabled = AddPokemonList.SelectedItem != null
                                         && CurrentTeam.Any(p => p == null);

            RemovePokemonButton.IsEnabled = false;
        }


        private static System.Windows.Controls.Image CreateTypeIcon(string type) => new()
        {
            Margin = new Thickness(3, 0, 3, 0),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Source = new BitmapImage(new Uri(GetTypeIconPath(type)))
        };

        private static string GetPokemonSpritePath(Pokemon pokemon)
        {
            string fileName = $"{pokemon.PokemonId}.png";
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites", "pokemon", fileName);

            return File.Exists(path)
                ? path
                : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites", "pokemon", "0.png");
        }

        private static string GetTypeIconPath(string typeName)
        {
            string fileName = $"{typeName}.png";
            string typeIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites", "types", fileName);

            return File.Exists(typeIconPath)
                ? typeIconPath
                : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites", "pokemon", "0.png");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_pokemonCollectionView == null)
                return;

            string filter = SearchBox.Text.Trim().ToLower();
            _pokemonCollectionView.Filter = string.IsNullOrEmpty(filter)
                ? null
                : obj => obj is Pokemon p && p.Name.Contains(filter, global::System.StringComparison.CurrentCultureIgnoreCase);

            _pokemonCollectionView.Refresh();
        }

        private void GameSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedGame = GameSelector.SelectedItem as Game;
            if (selectedGame == null)
                return;

            LoadAvailablePokemon();
            PokemonStatAverages.PrecalculateForGame(_dbContext, selectedGame);
        }

        private void IncludeLegendaries_CheckedChanged(object sender, RoutedEventArgs e)
        {
            LoadAvailablePokemon();
        }

        private void IncludeMythicals_CheckedChanged(object sender, RoutedEventArgs e)
        {
            LoadAvailablePokemon();
        }

        private void AddPokemonButton_Click(object sender, RoutedEventArgs e)
        {
            if (AddPokemonList.SelectedItem is not Pokemon selectedPokemon)
                return;

            // Check for duplicate
            if (CurrentTeam.Any(p => p != null && p.PokemonId == selectedPokemon.PokemonId))
            {
                // Use fully qualified name to resolve ambiguity
                System.Windows.MessageBox.Show($"{selectedPokemon.Name} is already on your team.", "Duplicate Pokémon", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // If we're replacing a selected slot
            if (SelectedTeamIndex >= 0)
            {
                CurrentTeam[SelectedTeamIndex] = selectedPokemon;
                UpdateTeamSlot(SelectedTeamIndex, selectedPokemon);
                SelectedTeamIndex = -1;
                AddPokemonButton.IsEnabled = false;
                return;
            }

            // Check if team is already full
            int currentCount = CurrentTeam.Count(p => p != null);
            if (currentCount >= 5)
            {
                System.Windows.MessageBox.Show("Limit Reached", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int nextSlot = CurrentTeam.ToList().FindIndex(p => p == null);


            // Add to first available slot
            CurrentTeam[nextSlot] = selectedPokemon;
            UpdateTeamSlot(nextSlot, selectedPokemon);

            // Disable Add button again if team is now full
            if (!CurrentTeam.Any(p => p == null))
                AddPokemonButton.IsEnabled = false;

            FilterMutuallyExclusivePokemon();

        }


        public void RemovePokemonButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTeamIndex != -1)
            {
                CurrentTeam[SelectedTeamIndex] = null;
                if (CurrentTeamGrid.Children[SelectedTeamIndex] is TeamSlotControl slot)
                {
                    slot.SetPokemon(null);
                }

                SelectedTeamIndex = -1;
                RemovePokemonButton.IsEnabled = false;
                AddPokemonButton.IsEnabled = AddPokemonList.SelectedItem != null;
            }

            FilterMutuallyExclusivePokemon();

        }

        public void OnTeamSlotClicked(int index)
        {
            SelectedTeamIndex = index;

            for (int i = 0; i < 6; i++)
            {
                if (CurrentTeamGrid.Children[i] is TeamSlotControl slot)
                {
                    slot.IsSelected = (i == index);
                }
            }

            RemovePokemonButton.IsEnabled = CurrentTeam[index] != null;
            AddPokemonButton.IsEnabled = false;
        }

        private void UpdateTeamSlot(int index, Pokemon? pokemon)
        {
            if (index < 0 || index >= CurrentTeamGrid.Children.Count)
                return;

            if (CurrentTeamGrid.Children[index] is TeamSlotControl slot)
            {
                slot.SetPokemon(pokemon);
            }
        }


        private void FilterMutuallyExclusivePokemon()
        {
            if (_pokemonCollectionView == null)
                return;

            var selectedGame = GameSelector.SelectedItem as Game;
            if (selectedGame == null)
                return;

            var teamPokemonIds = CurrentTeam
                .Where(p => p != null)
                .Select(p => p!.PokemonId)
                .ToList();

            var mutuallyExclusiveIds = new HashSet<int>();

            if (teamPokemonIds.Count != 0)
            {
                // Step 1: Get group IDs for current team within the selected game
                var groupIds = _dbContext.ExclusivityGroupMembers
                    .Where(m => teamPokemonIds.Contains(m.PokemonId))
                    .Join(_dbContext.ExclusivityGroups,
                          m => m.GroupId,
                          g => g.GroupId,
                          (m, g) => new { m.PokemonId, g.GroupId, g.GameId })
                    .Where(x => x.GameId == selectedGame.GameId)
                    .Select(x => x.GroupId)
                    .Distinct()
                    .ToList();

                // Step 2: Get all other Pokémon IDs in those groups (excluding those already in team)
                if (groupIds.Count != 0)
                {
                    mutuallyExclusiveIds = [.. _dbContext.ExclusivityGroupMembers
                        .Where(m => groupIds.Contains(m.GroupId) && !teamPokemonIds.Contains(m.PokemonId))
                        .Join(_dbContext.ExclusivityGroups,
                              m => m.GroupId,
                              g => g.GroupId,
                              (m, g) => new { m.PokemonId, g.GameId })
                        .Where(x => x.GameId == selectedGame.GameId)
                        .Select(x => x.PokemonId)];
                }
            }

            // Step 3: Apply filter to CollectionView
            _pokemonCollectionView.Filter = item =>
            {
                if (item is not Pokemon p) return false;
                return !mutuallyExclusiveIds.Contains(p.PokemonId);
            };

            _pokemonCollectionView.Refresh();
        }


        private void ClearUnavailableTeamPokemon()
        {
            var selectedGame = GameSelector.SelectedItem as Game;
            if (selectedGame == null)
                return;

            var includeLegendaries = IncludeLegendaries.IsChecked ?? false;
            var includeMythicals = IncludeMythicals.IsChecked ?? false;

            // Get valid Pokémon IDs for current game
            var availableIds = _dbContext.PokemonAvailability
                .Where(pa => pa.GameId == selectedGame.GameId)
                .Select(pa => pa.PokemonId)
                .ToHashSet();

            for (int i = 0; i < CurrentTeam.Count; i++)
            {
                var teamMember = CurrentTeam[i];
                if (teamMember == null)
                    continue;

                bool notInGame = !availableIds.Contains(teamMember.PokemonId);
                bool isExcludedLegendary = teamMember.IsLegendary && !includeLegendaries;
                bool isExcludedMythical = teamMember.IsMythical && !includeMythicals;

                if (notInGame || isExcludedLegendary || isExcludedMythical)
                {
                    CurrentTeam[i] = null;
                    UpdateTeamSlot(i, null);
                }
            }
        }

        private void SuggestTeamButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedGame = GameSelector.SelectedItem as Game;
            if (selectedGame == null)
                return;

            var engine = new TeamSuggestionEngine(_dbContext, selectedGame, CurrentTeam, IncludeLegendaries.IsChecked ?? false, IncludeMythicals.IsChecked ?? false);
            var suggestedTeam = engine.SuggestTeam();

            for (int i = 0; i < 6; i++)
            {
                // var slotControl = TeamSlots[i]; // Remove this line
                var pokemon = i < suggestedTeam.Count ? suggestedTeam[i] : null;
                if (CurrentTeamGrid.Children[i] is TeamSlotControl slotControl)
                    slotControl.SetPokemon(pokemon);
                CurrentTeam[i] = pokemon; // Keep CurrentTeam in sync
            }


            // Optionally reset selection state
            SelectedTeamIndex = -1;
        }

        private void AddPokemonList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AddPokemonButton_Click(sender, e);
        }
    }
}
