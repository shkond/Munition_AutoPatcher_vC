using System.Windows;

namespace MunitionAutoPatcher.Views;

public partial class InputDialog : Window
{
    public string ResponseText => InputBox.Text;

    public InputDialog(string prompt, string initial = "")
    {
        InitializeComponent();
        PromptText.Text = prompt;
        InputBox.Text = initial;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
