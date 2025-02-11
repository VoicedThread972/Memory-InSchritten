﻿using Memory_InSchritten.UserControls;
using Microsoft.VisualBasic;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Memory_InSchritten
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string cardPath = "";

        private ImageBrush Covered = new ImageBrush(new BitmapImage(new Uri(Directory.GetCurrentDirectory() + @"\bilder\starsolid.gif")));

        private List<string> Cards = [];
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SetNames()
        {
            var p1name = Interaction.InputBox("Spieler 1 Name", "Memory", "Player 1");
            while (p1name.Length is > 10 or 0)
            {
                MessageBox.Show("Ungültiger Name", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                p1name = Interaction.InputBox("Spieler 1 Name", "Memory", "Player 1");
            }

            var p2name = Interaction.InputBox("Spieler 2 Name", "Memory", "Player 2");
            while (p2name.Length is > 10 or 0)
            {
                MessageBox.Show("Ungültiger Name", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                p2name = Interaction.InputBox("Spieler 2 Name", "Memory", "Player 1");
            }

            Player1.PlayerName.Text = p1name;
            Player2.PlayerName.Text = p2name;
        }

        private void SetCards()
        {
            var msg = new CustomMessageBox();
            if (msg.ShowDialog() == true)
            {
                cardPath += msg.Result switch
                {
                    "Comics" => "comics",
                    "Harry Potter" => "harrypotter",
                    "Popstars" => "popstars",
                    _ => ""
                };
            }
        }

        private void Shuffle()
        {
            var rnd = new Random();
            for (var i = 0; i < Cards.Count; i++)
            {
                var index = rnd.Next(Cards.Count);
                (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
            }
        }

        private void LoadCards()
        {
            var index = 0;
            foreach (var file in Directory.GetFiles(cardPath))
            {
                if (++index > 10) break;
                Cards.Add(file);
                Cards.Add(file);
            }
        }

        private void PlaceCards()
        {
            var index = -1;
            for (var i = 1; i < Grid.ColumnDefinitions.Count - 1; i++)
            {
                for (var j = 0; j < Grid.RowDefinitions.Count; j++)
                {
                    if (++index >= Cards.Count) return;

                    var btn = new Button
                    {
                        Content = Cards[index],
                        Background = Covered,
                        Foreground = Brushes.Transparent,
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(1),
                    };

                    btn.SetValue(Grid.ColumnProperty, i);
                    btn.SetValue(Grid.RowProperty, j);

                    btn.Click += ShowCard;

                    Grid.Children.Add(btn);
                }
            }
        }

        private void ShowCard(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn is null) return;

            btn.Background = new ImageBrush(new BitmapImage(new Uri(btn.Content.ToString() ?? "")));
            btn.IsHitTestVisible = false;
            btn.Focusable = false;
        }

        private void Reset()
        {
            Player1.Score.Content = "0";
            Player2.Score.Content = "0";

            cardPath = Directory.GetCurrentDirectory() + @"\bilder\";
            Cards = [];

            SetNames();

            SetCards();

            LoadCards();

            Shuffle();

            PlaceCards();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Reset();
        }
    }
}