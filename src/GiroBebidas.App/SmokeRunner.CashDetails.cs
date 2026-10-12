using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckCashDetails(string output)
    {
        var store = new JsonStore(Path.Combine(output, "dados-detalhes-caixa-" + Guid.NewGuid().ToString("N")));
        var service = new StoreService(store); service.Login("ADMIN", "ADMIN");
        var cash = service.OpenCash(100);
        service.MoveCash(CashMovementKind.Suprimento, 25, "Complemento do fundo");
        service.MoveCash(CashMovementKind.Sangria, 10, "Retirada conferida");
        var customer = new Customer { Name = "Cliente da conferência do caixa", CreditLimit = 1000 }; service.SaveCustomer(customer);
        var products = service.Snapshot.Products.Take(5).ToArray();
        var methods = new[] { PaymentMethod.Dinheiro, PaymentMethod.Pix, PaymentMethod.Debito, PaymentMethod.Credito, PaymentMethod.Fiado };
        for (int i = 0; i < methods.Length; i++)
            service.CompleteSale(new([new(products[i].Id, 1)], 0, 0, i == 4 ? customer.Id : null, methods[i], i == 4 ? 0 : products[i].Price));
        service.ReceiveDebt(customer.Id, 1, PaymentMethod.Dinheiro); service.ReceiveDebt(customer.Id, 2, PaymentMethod.Pix);
        service.ReceiveDebt(customer.Id, 3, PaymentMethod.Debito); service.ReceiveDebt(customer.Id, 4, PaymentMethod.Credito);
        var canceled = service.CompleteSale(new([new(products[0].Id, 1)], 0, 0, null, PaymentMethod.Dinheiro, products[0].Price));
        service.CancelSale(canceled.Id, "Cancelamento de teste");
        var window = new MainWindow(service, false) { Width = 1200, Height = 760 };
        try
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000;
            window.ShowActivated = false; window.ShowInTaskbar = false; window.Show(); FlushLayout(window);
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static bool Tone(Brush? brush, string color) => brush is SolidColorBrush actual && actual.Color == (Color)ColorConverter.ConvertFromString(color);
            static void Prepare(CashDetailsWindow dialog, double width = 800, double height = 860)
            {
                dialog.Owner = null; dialog.Width = Math.Min(width, dialog.MaxWidth); dialog.Height = Math.Min(height, dialog.MaxHeight);
                dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -10000; dialog.Top = -10000;
                dialog.ShowActivated = false; dialog.ShowInTaskbar = false;
            }
            void Bounds(CashDetailsWindow dialog)
            {
                FlushLayout(dialog); var root = (FrameworkElement)dialog.Content;
                foreach (var control in new FrameworkElement[] { dialog.HeaderTitleLabel, dialog.HeaderCloseButton, dialog.CloseButton })
                {
                    var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
                    if (!control.IsVisible || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -.5 || bounds.Top < -.5 || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5)
                        throw new InvalidOperationException("Detalhes do caixa precisam manter título e botões dentro da janela menor.");
                }
                foreach (var card in dialog.SectionCards)
                    foreach (var label in Descendants<TextBlock>(card).Where(t => t.IsVisible))
                    {
                        var bounds = label.TransformToAncestor(card).TransformBounds(new Rect(label.RenderSize));
                        if (bounds.Left < -.5 || bounds.Width <= 0 || bounds.Right > card.ActualWidth + .5)
                            throw new InvalidOperationException("Textos e valores dos detalhes do caixa não podem sofrer corte horizontal.");
                    }
            }
            void Values(CashDetailsWindow dialog)
            {
                var expected = new Dictionary<string, decimal>
                {
                    ["opening"] = 100, ["cash"] = products[0].Price + 1, ["supplies"] = 25, ["withdrawals"] = 10,
                    ["expected"] = 116 + products[0].Price, ["pix"] = products[1].Price + 2, ["debit"] = products[2].Price + 3,
                    ["credit"] = products[3].Price + 4, ["creditSales"] = products[4].Price, ["total"] = products.Sum(p => p.Price)
                };
                foreach (var (key, amount) in expected)
                    if (dialog.ValueLabels[key].Text != amount.ToString("C2")) throw new InvalidOperationException($"O valor {key} dos detalhes diverge das movimentações reais do caixa.");
                if (dialog.ValueLabels["saleCount"].Text != "5" || dialog.ValueLabels["openedBy"].Text != "ADMIN" || dialog.SectionCards.Count != 3)
                    throw new InvalidOperationException("Os detalhes precisam separar abertura, vendas e fechamento, excluir venda cancelada e preservar o responsável.");
            }
            string before = State();
            var open = window.CreateCashDetailsDialog(new CashSession { Id = cash.Id, OpenedBy = "Linha desatualizada" });
            try
            {
                Prepare(open); Capture(open, Path.Combine(output, "80-detalhes-caixa-aberto.png")); Bounds(open); Values(open);
                if (open.StatusLabel.Text != "Aberto" || !Tone(open.StatusLabel.Foreground, "#187B52") ||
                    open.ValueLabels.ContainsKey("counted") || open.ValueLabels.ContainsKey("difference"))
                    throw new InvalidOperationException("O caixa aberto precisa ficar verde e não mostrar contagem nem diferença antes do fechamento.");
                Click(open.CloseButton); FlushLayout(window);
                if (open.IsVisible || State() != before) throw new InvalidOperationException("Fechar os detalhes não pode encerrar o caixa nem alterar registros.");
            }
            finally { if (open.IsVisible) open.Close(); }

            decimal expectedCash = 116 + products[0].Price;
            var closed = service.CloseCash(expectedCash - 5, "Valores conferidos no encerramento.");
            service.OpenCash(250);
            service.CompleteSale(new([new(products[0].Id, 2)], 0, 0, null, PaymentMethod.Pix, products[0].Price * 2));
            before = State();
            var negative = window.CreateCashDetailsDialog(closed);
            try
            {
                Prepare(negative); Capture(negative, Path.Combine(output, "81-detalhes-caixa-fechado.png")); Bounds(negative); Values(negative);
                if (negative.StatusLabel.Text != "Fechado" || !Tone(negative.StatusLabel.Foreground, "#B4483E") ||
                    negative.ValueLabels["counted"].Text != (expectedCash - 5).ToString("C2") || negative.ValueLabels["difference"].Text != (-5m).ToString("C2") ||
                    !Tone(negative.ValueLabels["difference"].Foreground, "#B4483E"))
                    throw new InvalidOperationException("Caixa fechado deve mostrar o valor contado e a falta em vermelho, sem incluir movimentos de outro caixa.");
                negative.DetailScroll.ScrollToEnd(); FlushLayout(negative); Bounds(negative);
                Capture(negative, Path.Combine(output, "82-detalhes-caixa-fechamento.png"));
                Click(negative.HeaderCloseButton); FlushLayout(window);
                if (negative.IsVisible || State() != before) throw new InvalidOperationException("O X precisa fechar somente a consulta dos detalhes.");
            }
            finally { if (negative.IsVisible) negative.Close(); }
            service.CloseCash(250, "");
            var positive = service.OpenCash(30); positive = service.CloseCash(35, "");
            var positiveDialog = window.CreateCashDetailsDialog(positive);
            try
            {
                Prepare(positiveDialog, 600, 650); Capture(positiveDialog, Path.Combine(output, "83-detalhes-caixa-sobra.png")); Bounds(positiveDialog);
                if (positiveDialog.ValueLabels["difference"].Text != 5m.ToString("C2") || !Tone(positiveDialog.ValueLabels["difference"].Foreground, "#906617") ||
                    positiveDialog.ValueLabels["notes"].Text != "Sem observações do fechamento.") throw new InvalidOperationException("Sobra e fechamento sem observações precisam ser identificados corretamente.");
                before = State(); positiveDialog.RaiseEvent(KeyEvent(positiveDialog, Key.Escape, Keyboard.PreviewKeyDownEvent)); FlushLayout(window);
                if (positiveDialog.IsVisible || State() != before) throw new InvalidOperationException("Esc deve fechar os detalhes sem gravar dados.");
            }
            finally { if (positiveDialog.IsVisible) positiveDialog.Close(); }

            var zero = service.OpenCash(0); zero = service.CloseCash(0, "");
            var zeroDialog = window.CreateCashDetailsDialog(zero);
            if (!Tone(zeroDialog.ValueLabels["difference"].Foreground, "#187B52") || zeroDialog.ValueLabels["total"].Text != 0m.ToString("C2") || zeroDialog.ValueLabels["saleCount"].Text != "0")
                throw new InvalidOperationException("Caixa vazio e conferência correta devem mostrar valores zero e diferença verde.");
            zeroDialog.Close();
            string longNotes = string.Join(" ", Enumerable.Repeat("Observações completas da conferência e das retiradas do caixa.", 35));
            store.Update(state => { state.CashSessions.Single(c => c.Id == closed.Id).ClosingNotes = longNotes; return 0; });
            var longDialog = window.CreateCashDetailsDialog(closed); before = State();
            try
            {
                Prepare(longDialog, 560, 520); Capture(longDialog, Path.Combine(output, "84-detalhes-caixa-tela-menor.png")); Bounds(longDialog);
                if (longDialog.ValueLabels["notes"].Text != longNotes || !ControlledScrolling.GetIsEnabled(longDialog.DetailScroll) || longDialog.DetailScroll.ScrollableHeight < 80)
                    throw new InvalidOperationException("Observações longas devem permanecer completas e acessíveis com rolagem na janela menor.");
                void Wheel(int delta, bool flush = true)
                {
                    var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                    longDialog.DetailScroll.RaiseEvent(args); if (flush) FlushLayout(longDialog);
                    if (!args.Handled) throw new InvalidOperationException("O touchpad deve usar a rolagem controlada nos detalhes do caixa.");
                }
                double distance = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
                longDialog.DetailScroll.ScrollToVerticalOffset(30); FlushLayout(longDialog); Wheel(-20);
                if (Math.Abs(longDialog.DetailScroll.VerticalOffset - (30 + distance / 6)) > .6) throw new InvalidOperationException("Pequenos gestos devem produzir deslocamento proporcional nos detalhes do caixa.");
                longDialog.DetailScroll.ScrollToVerticalOffset(30); FlushLayout(longDialog);
                for (int i = 0; i < 6; i++) Wheel(-20, false); FlushLayout(longDialog);
                if (Math.Abs(longDialog.DetailScroll.VerticalOffset - (30 + distance)) > .6) throw new InvalidOperationException("Gestos consecutivos não podem acelerar ou perder deslocamento nos detalhes do caixa.");
                longDialog.DetailScroll.ScrollToTop(); FlushLayout(longDialog); Wheel(120);
                if (longDialog.DetailScroll.VerticalOffset != 0) throw new InvalidOperationException("A rolagem precisa respeitar o início da área.");
                longDialog.DetailScroll.ScrollToEnd(); FlushLayout(longDialog); Wheel(-120); Bounds(longDialog);
                if (Math.Abs(longDialog.DetailScroll.VerticalOffset - longDialog.DetailScroll.ScrollableHeight) > .5) throw new InvalidOperationException("A rolagem precisa respeitar o fim da área.");
                Capture(longDialog, Path.Combine(output, "85-detalhes-caixa-observacoes.png"));
                longDialog.Close(); if (State() != before) throw new InvalidOperationException("Rolar e fechar observações longas não pode alterar dados.");
            }
            finally { if (longDialog.IsVisible) longDialog.Close(); }
            service.SaveUser(new UserAccount { Name = "Operador de teste", Username = "OPERADOR", Role = UserRole.Operador }, "senha123");
            service.Login("OPERADOR", "senha123"); before = State();
            var operatorDialog = window.CreateCashDetailsDialog(closed); operatorDialog.Close();
            if (State() != before) throw new InvalidOperationException("O operador autenticado precisa consultar o histórico sem alterar permissões ou dados.");
            bool rejected = false; service.Logout();
            try { _ = window.CreateCashDetailsDialog(closed); } catch (UnauthorizedAccessException) { rejected = true; }
            if (!rejected || State() != before) throw new InvalidOperationException("Consultar detalhes exige uma sessão autenticada.");
            service.Login("ADMIN", "ADMIN"); before = State(); rejected = false;
            try { _ = window.CreateCashDetailsDialog(new CashSession()); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || State() != before) throw new InvalidOperationException("Um caixa inexistente deve ser recusado sem gravar dados.");
            File.WriteAllText(Path.Combine(output, "detalhes-caixa-smoke-ok.txt"), "Detalhes do caixa renderizados com cartões para abertura, vendas e fechamento. Valores de dinheiro, suprimento, sangria, Pix, débito, crédito, fiado e recebimentos conferidos; venda cancelada excluída e outro caixa isolado. Situação aberta/fechada e diferenças negativas, positivas e zero conferidas. Fechar, X e Esc preservam os registros. Observações longas, janela menor, pequenos deltas, gestos consecutivos e limites de rolagem conferidos. Consulta por operador permitida; usuário desconectado e caixa inexistente recusados.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
