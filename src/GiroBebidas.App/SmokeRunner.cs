using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static class SmokeRunner
{
    public static void Run(StoreService service, string output)
    {
        Directory.CreateDirectory(output);
        var login = new LoginWindow(service); Capture(login, Path.Combine(output, "01-login.png")); login.Close();
        service.Login("ADMIN", "ADMIN");
        if (!service.Snapshot.Customers.Any()) service.SaveCustomer(new() { Name = "Marina Souza", Phone = "(11) 99999-0000", Address = "Rua dos Ipês, 85 • Centro", CreditLimit = 500 });
        if (!service.Snapshot.CashSessions.Any()) service.OpenCash(250);
        if (service.Snapshot.Users.Count == 1) service.SaveUser(new() { Name = "João • balcão", Username = "JOAO", Role = UserRole.Operador }, "1234");
        var window = new MainWindow(service); var vm = window.Vm;
        vm.AddProduct(vm.Snapshot.Products[0], 12); vm.AddProduct(vm.Snapshot.Products[4], 2); vm.AddProduct(vm.Snapshot.Products[12], 1);
        vm.CartCustomer = vm.CustomerOptions.Last(); vm.Discount = 2;
        if (!service.Snapshot.Orders.Any()) service.CreateOrder([new(vm.Snapshot.Products[2].Id, 6)], service.Snapshot.Customers[0].Id, true, "Rua dos Ipês, 85 • Centro", "Entregar após 18h", 0, 5);
        if (!service.Snapshot.Sales.Any()) service.CompleteSale(new([new(vm.Snapshot.Products[0].Id, 2)], 0, 0, null, PaymentMethod.Pix, 9));
        vm.Refresh(); Capture(window, Path.Combine(output, "02-pdv.png"));
        int index = 3;
        foreach (var page in new[] { "Estoque", "Clientes", "Pedidos", "Caixa", "Vendas", "Relatórios", "Usuários", "Equipamentos", "Configurações" })
        {
            vm.Page = page; Capture(window, Path.Combine(output, $"{index++:00}-{page.ToLowerInvariant()}.png"));
        }
        var form = new FormDialog(window, "Cadastrar produto", "Personalize a foto, a descrição e os valores do produto.");
        form.Text("name", "Nome", "Pilsen • lata 350 ml"); form.Text("description", "Descrição", "Cerveja pilsen para servir gelada", true); form.Number("price", "Preço de venda", 4.50m);
        // An unshown owner cannot be assigned to a visible window; clear it for offscreen QA.
        form.Owner = null; form.SizeToContent = SizeToContent.Manual; form.Height = 520; Capture(form, Path.Combine(output, "12-formulario.png")); form.Close();
        vm.ClearCart(); window.Close();
        File.WriteAllText(Path.Combine(output, "smoke-ok.txt"), "Login, PDV e nove módulos renderizados. Formulário renderizado.\n");
    }
    private static void Capture(Window window, string path)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000; window.ShowInTaskbar = false; window.ShowActivated = false;
        if (!window.IsVisible) window.Show();
        window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
