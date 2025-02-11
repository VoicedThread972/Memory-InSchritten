using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Memory_InSchritten.UserControls
{
    /// <summary>
    /// Interaktionslogik für Player.xaml
    /// </summary>
    public partial class Player : UserControl
    {
        public Player()
        {
            InitializeComponent();
        }

        private double CalcFontSize(FrameworkElement Object)
        {
            if (Object is TextBox textBox)
            {
                var s = textBox.Text;
                var ff = textBox.FontFamily;
                var fs = textBox.FontStyle;
                var fw = textBox.FontWeight;
                var fstr = textBox.FontStretch;
                var typeFont = new Typeface(ff, fs, fw, fstr);
                var maxWidth = Rect.ActualWidth - (Panel.Margin.Left + Panel.Margin.Right + Border.BorderThickness.Left + Border.BorderThickness.Right);
                var maxHeight = (Rect.ActualHeight - (Panel.Margin.Top + Panel.Margin.Bottom + Border.BorderThickness.Top + Border.BorderThickness.Bottom)) / 3.0;
                return CalcFontSize(s, maxWidth, maxHeight, typeFont); // PlayerName
            }
            if (Object is Label label)
            {
                var s = label.Content?.ToString() ?? "";
                var ff = label.FontFamily;
                var fs = label.FontStyle;
                var fw = label.FontWeight;
                var fstr = label.FontStretch;
                var typeFont = new Typeface(ff, fs, fw, fstr);
                var maxWidth = Rect.ActualWidth - (Border.BorderThickness.Left + Border.BorderThickness.Right + 20);
                var maxHeight = (Rect.ActualHeight - (Panel.Margin.Top + Panel.Margin.Bottom + Border.BorderThickness.Top + Border.BorderThickness.Bottom)) / 3.0;
                return CalcFontSize(s, maxWidth, maxHeight, typeFont); // Title, Score
            }
            return 20;
        }

        private void CalcFontSize()
        {
            PlayerName.FontSize = CalcFontSize(PlayerName);

            Title.FontSize = CalcFontSize(Title);

            Score.FontSize = CalcFontSize(Score);
        }

        private static double CalcFontSize(string s, double maxWidth, double maxHeight, Typeface typeFont)
        {
            double minFontSize = 1;
            double maxFontSize = Math.Max(Math.Min(maxWidth * 2, maxHeight * 2), 1);
            double midFontSize;
            FormattedText formattedText;

            if (double.IsInfinity(maxFontSize) || maxFontSize is double.NaN || maxHeight is double.NaN || double.IsInfinity(maxHeight) || maxWidth is double.NaN || double.IsInfinity(maxWidth) || string.IsNullOrEmpty(s))
            {
                return 20;
            }

            while (minFontSize < maxFontSize - 0.1)
            {
                midFontSize = (maxFontSize + minFontSize) / 2;
                formattedText = new FormattedText(
                    s,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeFont,
                    midFontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    1);

                if (formattedText.Width < maxWidth && formattedText.Height < maxHeight)
                {
                    minFontSize = midFontSize;
                }
                else
                {
                    maxFontSize = midFontSize;
                }
            }

            return Math.Max(minFontSize - 2, 1);
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalcFontSize();
        }

        private void PlayerName_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalcFontSize();
        }
    }
}
