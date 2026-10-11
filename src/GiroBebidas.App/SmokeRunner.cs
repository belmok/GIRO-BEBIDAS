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
        var window = new MainWindow(service, false); var vm = window.Vm;
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
        string samplePhoto = Path.Combine(output, "foto-de-teste.png");
        var photoVisual = new DrawingVisual(); using (var context = photoVisual.RenderOpen()) context.DrawImage(BeverageArt.Create(vm.Snapshot.Products[0]), new Rect(0, 0, 160, 140));
        var photoBitmap = new RenderTargetBitmap(160, 140, 96, 96, PixelFormats.Pbgra32); photoBitmap.Render(photoVisual);
        var photoEncoder = new PngBitmapEncoder(); photoEncoder.Frames.Add(BitmapFrame.Create(photoBitmap)); using (var stream = File.Create(samplePhoto)) photoEncoder.Save(stream);
        var photoProduct = vm.Snapshot.Products[0]; photoProduct.PhotoBase64 = PhotoTools.Import(samplePhoto); photoProduct.Description = "Cerveja pilsen em lata de 350 ml. Ideal para servir gelada.";
        service.SaveProduct(photoProduct); vm.Refresh();
        if (PhotoTools.Decode(service.Snapshot.Products[0].PhotoBase64) is null) throw new InvalidOperationException("Falha na importação da foto.");
        service.ExportBackup(Path.Combine(output, "backup-com-foto.json"));
        var form = window.CreateProductDialog(photoProduct, false);
        // An unshown owner cannot be assigned to a visible window; clear it for offscreen QA.
        form.Owner = null; form.SizeToContent = SizeToContent.Manual; form.Height = Math.Min(800, form.MaxHeight); Capture(form, Path.Combine(output, "12-cadastro-produto.png"));
        form.ScrollToField("origin"); Capture(form, Path.Combine(output, "19-dados-fiscais-produto.png")); form.Close();
        vm.Page = "Fiscal"; Capture(window, Path.Combine(output, "16-configuracao-fiscal.png"));
        window.FiscalConfigurationPanel.ShowChecks(); Capture(window, Path.Combine(output, "20-conferencia-fiscal.png"));
        var issuerForm = window.FiscalConfigurationPanel.CreateIssuerDialog(); issuerForm.Owner = null; issuerForm.SizeToContent = SizeToContent.Manual; issuerForm.Height = Math.Min(800, issuerForm.MaxHeight);
        Capture(issuerForm, Path.Combine(output, "17-empresa-emitente.png")); issuerForm.Close();
        var fiscalForm = window.FiscalConfigurationPanel.CreateOptionsDialog(); fiscalForm.Owner = null; fiscalForm.SizeToContent = SizeToContent.Manual; fiscalForm.Height = Math.Min(730, fiscalForm.MaxHeight);
        Capture(fiscalForm, Path.Combine(output, "18-parametros-nfce.png")); fiscalForm.Close();
        if (service.Snapshot.Fiscal.Options.Environment != FiscalEnvironment.Homologacao || vm.FiscalChecks.Single(c => c.Item == "Autorização SEFAZ").Complete)
            throw new InvalidOperationException("A configuração inicial não pode simular emissão fiscal autorizada.");
        vm.Page = "Atualizações"; Capture(window, Path.Combine(output, "21-atualizacoes.png"));
        window.Width = 1200; window.Height = 760; vm.Page = "PDV"; Capture(window, Path.Combine(output, "15-pdv-tela-menor.png"));
        if (window.CartItemsViewport.ActualHeight < 230) throw new InvalidOperationException("O carrinho precisa manter espaço suficiente para os produtos na tela menor.");
        MainWindow.ScrollMenu(window.MenuScroll, -20, 3); window.UpdateLayout();
        if (window.MenuScroll.ScrollableHeight > 0 && window.MenuScroll.VerticalOffset <= 0) throw new InvalidOperationException("A rolagem fracionária do menu não respondeu ao gesto do touchpad.");
        MainWindow.ScrollMenu(window.MenuScroll, 120, 3); window.UpdateLayout();
        vm.ClearCart(); window.Close();
        File.WriteAllText(Path.Combine(output, "smoke-ok.txt"), "Login, PDV e dez módulos renderizados. Cadastro completo de produto e campos fiscais renderizados. Configuração fiscal, empresa e parâmetros NFC-e renderizados. Foto importada, persistida e exportada no backup. Layout menor renderizado.\n");
    }
    private static void Capture(Window window, string path)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000; window.ShowInTaskbar = false; window.ShowActivated = false;
        if (!window.IsVisible) window.Show();
        window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var content = (FrameworkElement)window.Content;
        var backdrop = new DrawingVisual(); using (var context = backdrop.RenderOpen()) context.DrawRectangle(window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(backdrop); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
