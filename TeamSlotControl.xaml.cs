using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokemonTeamBuilder
{
    public partial class TeamSlotControl : System.Windows.Controls.UserControl
    {
        public int SlotIndex { get; set; }

        public TeamSlotControl()
        {
            InitializeComponent();
            this.PreviewMouseLeftButtonDown += Slot_Clicked;
        }

        public void SetPokemon(Pokemon? pokemon)
        {
            if (pokemon != null)
            {
                SpriteImage.Source = new BitmapImage(new Uri(GetPokemonSpritePath(pokemon)));
                NameDexText.Text = $"{pokemon.Name} / #{pokemon.PokemonId}";
                TypeIconPanel.Children.Clear();

                if (!string.IsNullOrEmpty(pokemon.Type1))
                    TypeIconPanel.Children.Add(CreateTypeIcon(pokemon.Type1));
                if (!string.IsNullOrEmpty(pokemon.Type2))
                    TypeIconPanel.Children.Add(CreateTypeIcon(pokemon.Type2));
            }
            else
            {
                SpriteImage.Source = new BitmapImage(new Uri(GetPokemonSpritePath(null)));
                NameDexText.Text = "Empty Slot";
                TypeIconPanel.Children.Clear();
            }
        }



        private void Slot_Clicked(object sender, MouseButtonEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.OnTeamSlotClicked(SlotIndex);
        }

        private void Remove_Pokemon(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow)?.OnTeamSlotClicked(SlotIndex);
            (Window.GetWindow(this) as MainWindow)?.RemovePokemonButton_Click(this, e);
        }

        private static string GetPokemonSpritePath(Pokemon? pokemon)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = pokemon == null ? "0.png" : $"{pokemon.PokemonId}.png";
            return System.IO.Path.Combine(baseDir, "sprites", "pokemon", fileName);
        }

        private static System.Windows.Controls.Image CreateTypeIcon(string type)
        {
            return new System.Windows.Controls.Image
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(3, 0, 3, 0),
                Stretch = Stretch.Uniform,
                Source = new BitmapImage(new Uri($"Sprites/types/{type}.png", UriKind.RelativeOrAbsolute))
            };
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                SlotBorder.BorderBrush = value ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Gray;
                SlotBorder.Background = value ? System.Windows.Media.Brushes.LightBlue : System.Windows.Media.Brushes.WhiteSmoke;
            }
        }


    }
}
