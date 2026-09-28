using System.Windows;

namespace MemoryInSchritten.Client.Views;

public partial class CardSetDialog : Window
{
    // Closing the dialog without a choice deliberately picks this unlisted set (kept from the original game).
    private const string HiddenSet = "markus";

    private string? chosen;

    private CardSetDialog() => InitializeComponent();

    public static string Choose(Window owner)
    {
        var dialog = new CardSetDialog { Owner = owner };
        dialog.ShowDialog();
        return dialog.chosen ?? HiddenSet;
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        chosen = (string)((FrameworkElement)sender).Tag;
        DialogResult = true;
    }
}
