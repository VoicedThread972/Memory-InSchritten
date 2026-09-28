using System.Windows;
using System.Windows.Controls;

namespace MemoryInSchritten.Client.Views;

public partial class NameDialog : Window
{
    private NameDialog(string prompt, string initial)
    {
        InitializeComponent();
        Prompt.Text = prompt;
        Input.Text = initial;
        Input.SelectAll();
    }

    /// <returns>The entered name, or <paramref name="fallback"/> if the dialog was cancelled.</returns>
    public static string Ask(Window owner, string prompt, string fallback)
    {
        var dialog = new NameDialog(prompt, fallback) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Input.Text.Trim() : fallback;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        var name = Input.Text.Trim();
        OkButton.IsEnabled = name.Length > 0 && !name.Any(char.IsControl);
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
