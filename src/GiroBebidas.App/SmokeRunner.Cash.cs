using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckCashDashboard(string output)
    {
        static string CurrencyText(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (System.Globalization.CultureInfo.CurrentCulture.Name != "pt-BR" || System.Globalization.CultureInfo.CurrentUICulture.Name != "pt-BR")
            throw new InvalidOperationException("O aplicativo precisa usar português brasileiro também em Windows com outro idioma.");
        var store = new JsonStore(Path.Combine(output, "dados-painel-caixa-" + Guid.NewGuid().ToString("N")));
        var service = new StoreService(store); service.Login("ADMIN", "ADMIN");
        foreach (int days in new[] { 40, 30, 29, 7, 6 })
        {
            var cash = service.OpenCash(100);
            service.CloseCash(days == 40 ? 95 : days == 30 ? 105 : 100, days == 40 ? "=2+2; \"conferência\"" : "Conferido");
            store.Update(state =>
            {
                var row = state.CashSessions.Single(c => c.Id == cash.Id);
                row.OpenedAt = new DateTimeOffset(DateTime.Today.AddDays(-days).AddHours(9)); row.ClosedAt = row.OpenedAt.AddHours(8);
                return 0;
            });
        }
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        try
        {
            var vm = window.Vm; vm.Page = "Caixa";
            Capture(window, Path.Combine(output, "48-controle-caixa-fechado.png"));
            // WPF can constrain the first Show to the runner's small desktop.
            // Apply the requested QA size after the native window is created.
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            if (new[] { window.CashMoneyValue, window.CashPixValue, window.CashDebitValue, window.CashCreditValue }.Any(value => CurrencyText(value.Text) != CurrencyText(0m.ToString("C2"))))
                throw new InvalidOperationException("Sem caixa aberto, os cartões precisam mostrar zero.");
            if (!window.CashOpenButton.IsEnabled || window.CashMoveButton.IsEnabled || window.CashCloseButton.IsEnabled)
                throw new InvalidOperationException("Caixa fechado deve permitir abertura e desativar movimentação/fechamento.");
            var current = service.OpenCash(100);
            service.MoveCash(CashMovementKind.Suprimento, 25, "Fundo complementar");
            service.MoveCash(CashMovementKind.Sangria, 10, "Retirada para depósito");
            var products = service.Snapshot.Products.Take(5).ToArray();
            var methods = new[] { PaymentMethod.Dinheiro, PaymentMethod.Pix, PaymentMethod.Debito, PaymentMethod.Credito, PaymentMethod.Fiado };
            var customer = new Customer { Name = "Cliente do teste do caixa", CreditLimit = 1000 }; service.SaveCustomer(customer);
            for (int i = 0; i < methods.Length; i++)
                service.CompleteSale(new([new(products[i].Id, 1)], 0, 0, i == 4 ? customer.Id : null, methods[i], i == 4 ? 0 : products[i].Price));
            service.ReceiveDebt(customer.Id, 3, PaymentMethod.Dinheiro); service.ReceiveDebt(customer.Id, 2, PaymentMethod.Pix);
            vm.Refresh(); FlushLayout(window);
            void AssertTotals()
            {
                var totals = vm.CashSummary ?? throw new InvalidOperationException("O caixa aberto precisa mostrar seus totais.");
                if (totals.Expected != 118 + products[0].Price || totals.Pix != products[1].Price + 2 || totals.Debit != products[2].Price ||
                    totals.Credit != products[3].Price || totals.CreditSales != products[4].Price || totals.SaleCount != 5 || totals.TotalSales != products.Sum(p => p.Price))
                    throw new InvalidOperationException("Os cartões devem somar vendas, recebimentos, fundo, suprimentos e sangrias por forma de pagamento.");
                var values = new[] { window.CashMoneyValue, window.CashPixValue, window.CashDebitValue, window.CashCreditValue };
                var expected = new[] { totals.Expected, totals.Pix, totals.Debit, totals.Credit };
                for (int i = 0; i < values.Length; i++)
                    if (CurrencyText(values[i].Text) != CurrencyText(expected[i].ToString("C2"))) throw new InvalidOperationException("O valor renderizado no cartão diverge do saldo do caixa.");
            }
            AssertTotals();
            if (window.CashOpenButton.IsEnabled || !window.CashMoveButton.IsEnabled || !window.CashCloseButton.IsEnabled)
                throw new InvalidOperationException("Caixa aberto deve permitir movimentação/fechamento e impedir outra abertura.");
            foreach (var (period, count) in new[] { ("Hoje", 1), ("Últimos 7 dias", 2), ("Últimos 30 dias", 4), ("Todo o período", 6) })
            {
                window.CashPeriodFilter.SelectedItem = period; FlushLayout(window); AssertTotals();
                if (vm.CashSessions.Count != count || window.CashHistoryGrid.Items.Count != count || vm.CashMovements.Count != 2)
                    throw new InvalidOperationException($"O período {period} não respeitou os limites das datas do histórico.");
            }
            var closed = vm.CashSessions.Single(c => c.OpenedAt.Date == DateTime.Today.AddDays(-40));
            window.CashHistoryGrid.SelectedItem = closed; FlushLayout(window);
            if (!window.CashHistoryPrintButton.IsEnabled) throw new InvalidOperationException("Um fechamento selecionado precisa permitir impressão.");
            vm.Refresh(); FlushLayout(window);
            if (vm.SelectedCash?.Id != closed.Id || !vm.CashSessions.Contains(vm.SelectedCash)) throw new InvalidOperationException("A seleção do histórico precisa sobreviver à atualização dos dados.");
            window.CashPeriodFilter.SelectedItem = "Hoje"; FlushLayout(window);
            if (vm.SelectedCash is not null || window.CashHistoryPrintButton.IsEnabled) throw new InvalidOperationException("Um caixa ocultado pelo filtro não pode permanecer selecionado para impressão.");
            var open = vm.CashSessions.Single(); window.CashHistoryGrid.SelectedItem = open; FlushLayout(window);
            if (window.CashHistoryPrintButton.IsEnabled) throw new InvalidOperationException("O caixa em aberto não pode imprimir um fechamento ainda não conferido.");
            window.CashHistoryGrid.ScrollIntoView(open, window.CashHistoryGrid.Columns.Last()); FlushLayout(window);
            var more = Descendants<Button>(window.CashHistoryGrid).Single(b => b.Name == "CashRowMoreButton" && b.DataContext is CashSession c && c.Id == open.Id);
            ((IInvokeProvider)new ButtonAutomationPeer(more).GetPattern(PatternInterface.Invoke)).Invoke(); FlushLayout(window);
            var menu = more.ContextMenu ?? throw new InvalidOperationException("O histórico precisa oferecer o menu de ações.");
            if (!menu.IsOpen || menu.DataContext is not CashSession menuCash || menuCash.Id != open.Id || menu.Items.Count != 3 ||
                ((MenuItem)menu.Items[1]).IsEnabled || vm.SelectedCash?.Id != open.Id || !vm.CashSessions.Contains(vm.SelectedCash))
                throw new InvalidOperationException("O menu do caixa precisa consultar a linha correta e desativar impressão enquanto aberto.");
            menu.IsOpen = false;
            window.CashPeriodFilter.SelectedItem = "Todo o período"; FlushLayout(window);
            string before = JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            string openCsv = window.CreateCashReportCsv(open), closedCsv = window.CreateCashReportCsv(closed);
            if (openCsv.Contains("Dinheiro contado", StringComparison.Ordinal) || openCsv.Contains("Diferença", StringComparison.Ordinal) ||
                !closedCsv.Contains("\"Diferença\";\"-5,00\"", StringComparison.Ordinal) || !closedCsv.Contains("'=2+2; \"\"conferência\"\"", StringComparison.Ordinal) ||
                !openCsv.Contains("Fundo complementar", StringComparison.Ordinal) || closedCsv.Contains("Fundo complementar", StringComparison.Ordinal))
                throw new InvalidOperationException("O CSV deve preservar valores, escapar texto e limitar os movimentos ao caixa consultado.");
            File.WriteAllText(Path.Combine(output, "relatorio-caixa-aberto.csv"), openCsv, new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(output, "relatorio-caixa-fechado.csv"), closedCsv, new UTF8Encoding(true));
            var details = window.CreateCashDetailsDialog(closed);
            try { details.Owner = null; details.SizeToContent = SizeToContent.Manual; details.Height = Math.Min(720, details.MaxHeight); Capture(details, Path.Combine(output, "51-detalhes-caixa.png")); }
            finally { details.Close(); }
            if (JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions) != before) throw new InvalidOperationException("Consultar detalhes e exportar relatório não pode modificar os registros do caixa.");
            var converter = new CashDifferenceToneConverter();
            foreach (var (amount, color) in new[] { (-5m, "#B4483E"), (5m, "#906617"), (0m, "#187B52") })
                if (((SolidColorBrush)converter.Convert(amount, typeof(Brush), "Foreground", System.Globalization.CultureInfo.CurrentCulture)).Color != (Color)ColorConverter.ConvertFromString(color))
                    throw new InvalidOperationException("Falta, sobra e conferência correta precisam ter cores diferentes.");
            Capture(window, Path.Combine(output, "49-controle-caixa-colorido.png"));
            var columns = new CashCardColumnsConverter();
            foreach (var (width, expectedColumns) in new[] { (1200d, 2), (1349d, 2), (1350d, 4), (1480d, 4) })
                if ((int)columns.Convert(width, typeof(int), "", System.Globalization.CultureInfo.CurrentCulture) != expectedColumns)
                    throw new InvalidOperationException("Os cartões precisam mudar entre duas e quatro colunas no limite de largura definido.");
            AssertCashCardBounds(window);
            window.Width = 1200; window.Height = 760; FlushLayout(window); AssertCashCardBounds(window);
            Capture(window, Path.Combine(output, "50-controle-caixa-tela-menor.png"));
            window.CashPageScroll.ScrollToEnd(); FlushLayout(window);
            Capture(window, Path.Combine(output, "52-caixa-movimentacoes.png"));
            store.Update(state => state.CashSessions.Single(c => c.Id == current.Id).OpenedAt = new DateTimeOffset(DateTime.Today.AddDays(-60).AddHours(9)));
            vm.Refresh(); window.CashPeriodFilter.SelectedItem = "Hoje"; FlushLayout(window); AssertTotals();
            if (vm.CashSessions.Count != 0 || vm.CashHistoryEmptyVisibility != "Visible" || !vm.CashIsOpen)
                throw new InvalidOperationException("Um histórico sem resultados precisa mostrar o estado vazio sem ocultar os totais do caixa aberto.");
            store.Update(state => { state.CashMovements.ForEach(m => m.Date = m.Date.AddDays(-60)); return 0; }); vm.Refresh(); FlushLayout(window);
            if (vm.CashMovements.Count != 0 || vm.CashMovementsEmptyVisibility != "Visible") throw new InvalidOperationException("O filtro deve mostrar a mensagem de movimentações vazias.");
            File.WriteAllText(Path.Combine(output, "painel-caixa-smoke-ok.txt"), "Cartões coloridos renderizados em quatro e duas colunas. Totais de vendas e recebimentos por pagamento conferidos; filtros e limites de datas independentes dos saldos. Estados vazios, seleção e menu nativos conferidos. Detalhes e CSV preservam registros; caixas abertos não apresentam contagem/diferença e não imprimem fechamento. Relatórios escapam textos e preservam números negativos.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
    private static void AssertCashCardBounds(MainWindow window)
    {
        var cards = new[] { window.CashMoneyCard, window.CashPixCard, window.CashDebitCard, window.CashCreditCard };
        var values = new[] { window.CashMoneyValue, window.CashPixValue, window.CashDebitValue, window.CashCreditValue };
        int expectedColumns = window.ActualWidth >= 1350 ? 4 : 2;
        if (cards[0].Parent is not UniformGrid grid || grid.Columns != expectedColumns) throw new InvalidOperationException($"Os cartões precisam adaptar as colunas à largura real da janela ({window.ActualWidth:0.##}).");
        for (int i = 0; i < cards.Length; i++)
        {
            var point = values[i].TranslatePoint(new Point(), cards[i]);
            var valueBounds = values[i].TransformToAncestor(cards[i]).TransformBounds(new Rect(0, 0, values[i].ActualWidth, values[i].ActualHeight));
            if (cards[i].ActualWidth < 200 || point.X < 0 || point.Y < 0 || valueBounds.Right > cards[i].ActualWidth + .5 || valueBounds.Bottom > cards[i].ActualHeight + .5)
                throw new InvalidOperationException("Os valores dos cartões não podem ficar cortados na janela menor.");
        }
    }
}
