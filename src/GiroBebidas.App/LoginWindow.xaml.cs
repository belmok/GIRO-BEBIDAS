using System.Windows;
using System.Windows.Input;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class LoginWindow : Window
{
    private readonly StoreService service;
    private int failedAttempts;
    public LoginWindow(StoreService service)
    {
        InitializeComponent(); this.service = service;
        Loaded += (_, _) => PasswordInput.Focus();
    }
    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (!LoginButton.IsEnabled) return;
        LoginButton.IsEnabled = false; ErrorText.Text = "";
        string username = UsernameInput.Text, password = PasswordInput.Password;
        try
        {
            if (failedAttempts >= 3) await Task.Delay(1500);
            await Task.Run(() => service.Login(username, password));
            PasswordInput.Clear(); DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
        { failedAttempts++; ErrorText.Text = ex.Message; PasswordInput.Clear(); PasswordInput.Focus(); }
        finally { LoginButton.IsEnabled = true; }
    }
    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; Login_Click(sender, e); }
    }
}
