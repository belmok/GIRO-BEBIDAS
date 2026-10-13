using System.Windows;
using System.Windows.Controls;

namespace GiroBebidas.App;

public partial class CustomersPanel : UserControl
{
    public CustomersPanel() => InitializeComponent();
    private MainWindow Host => (MainWindow)Window.GetWindow(this);
    private void New_Click(object sender, RoutedEventArgs e) => Host.CustomerNew(sender, e);
    private void Edit_Click(object sender, RoutedEventArgs e) => Host.CustomerEdit(sender, e);
    private void Receive_Click(object sender, RoutedEventArgs e) => Host.CustomerReceive(sender, e);
    private void Statement_Click(object sender, RoutedEventArgs e) => Host.CustomerStatement(sender, e);
    private void Details_Click(object sender, RoutedEventArgs e) => Host.CustomerDetails(sender, e);
    private void Menu_Click(object sender, RoutedEventArgs e) => Host.CustomerMenu(sender, e);
    private void Duplicate_Click(object sender, RoutedEventArgs e) => Host.CustomerDuplicate(sender, e);
    private void Credit_Click(object sender, RoutedEventArgs e) => Host.CustomerCredit(sender, e);
    private void Delete_Click(object sender, RoutedEventArgs e) => Host.CustomerDelete(sender, e);
}
