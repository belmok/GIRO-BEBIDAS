using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GiroBebidas.Core;
using Calendar = System.Windows.Controls.Calendar;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckSalesCalendarUi(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-calendario-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN"); service.OpenCash(0);
        var product = service.Snapshot.Products[0]; DateTime day = new(2026, 10, 12);
        var dates = new[] { day.AddTicks(-1), day, day.AddDays(1).AddTicks(-1), day.AddDays(1) };
        var sales = dates.Select(_ => service.CompleteSale(new([new(product.Id, 1)], 0, 0, null, PaymentMethod.Pix, product.Price))).ToArray();
        service.Store.Update(state => { for (int index = 0; index < dates.Length; index++) state.Sales.Single(s => s.Id == sales[index].Id).Date = new DateTimeOffset(dates[index]); return 0; });
        string stable = JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        DatePicker? opened = null;
        try
        {
            var vm = window.Vm; vm.Page = "Vendas";
            Capture(window, Path.Combine(output, "130-datas-sem-filtro.png")); FlushLayout(window);
            var from = (DatePicker)window.FindName("SalesFromPicker"); var until = (DatePicker)window.FindName("SalesUntilPicker");
            var apply = (Button)window.FindName("SalesApplyButton"); var clear = (Button)window.FindName("SalesClearButton");
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static DatePickerTextBox Input(DatePicker picker) => picker.Template.FindName("PART_TextBox", picker) as DatePickerTextBox
                ?? throw new InvalidOperationException("A data deve manter seu campo de digitação nativo.");
            Calendar Open(DatePicker picker)
            {
                picker.BringIntoView(); FlushLayout(window); picker.ApplyTemplate();
                if (opened is not null) { opened.IsDropDownOpen = false; FlushLayout(window); }
                Click((Button)picker.Template.FindName("PART_Button", picker)); FlushLayout(window); opened = picker;
                var popup = picker.Template.FindName("PART_Popup", picker) as Popup;
                if (!picker.IsDropDownOpen || popup?.Child is not Calendar calendar || calendar.ActualWidth <= 0 || calendar.ActualHeight <= 0)
                    throw new InvalidOperationException("O botão da data deve abrir um calendário nativo visível.");
                return calendar;
            }
            static CalendarItem Item(Calendar calendar) => calendar.Template.FindName("PART_CalendarItem", calendar) as CalendarItem
                ?? throw new InvalidOperationException("O calendário deve manter navegação e grade nativas.");
            static Button Part(Calendar calendar, string name) => (Button)Item(calendar).Template.FindName(name, Item(calendar));
            void Select(Calendar calendar, DateTime selected)
            {
                var button = Descendants<CalendarDayButton>(calendar).Single(b => b.DataContext is DateTime date && date.Date == selected.Date);
                Click(button); FlushLayout(window);
                if (calendar.SelectedDate != selected.Date) throw new InvalidOperationException("Selecionar um dia nativo precisa escolher a data exata.");
            }
            void CloseByKey(Calendar calendar, Key key)
            {
                var button = Descendants<CalendarDayButton>(calendar).First(b => b.IsSelected && b.IsVisible);
                button.RaiseEvent(KeyEvent(window, key, Keyboard.PreviewKeyDownEvent)); FlushLayout(window);
                if (opened?.IsDropDownOpen == true) throw new InvalidOperationException("Enter e Esc devem fechar o calendário pela navegação nativa.");
            }
            void Type(DatePicker picker, string text)
            {
                var input = Input(picker); ((IValueProvider)new TextBoxAutomationPeer(input).GetPattern(PatternInterface.Value)).SetValue(text);
                input.RaiseEvent(KeyEvent(window, Key.Enter, Keyboard.KeyDownEvent)); FlushLayout(window);
            }
            void CheckPlaceholder(DatePicker picker)
            {
                var input = Input(picker); input.ApplyTemplate();
                var watermark = input.Template.FindName("PART_Watermark", input) as ContentControl;
                if (picker.SelectedDate is not null || watermark is null || watermark.Opacity < .99
                    || !Descendants<TextBlock>(watermark).Any(t => t.Text == "Selecione uma data" && t.IsVisible))
                    throw new InvalidOperationException("Data vazia deve exibir a orientação em português sem impor filtro.");
            }
            CheckPlaceholder(from); CheckPlaceholder(until);
            if (vm.SalesFrom is not null || vm.SalesUntil is not null || vm.SalesFilteredCount != 4)
                throw new InvalidOperationException("Datas vazias precisam preservar todas as vendas e os filtros ainda não aplicados.");
            from.DisplayDate = day; var calendar = Open(from);
            string header = Part(calendar, "PART_HeaderButton").Content?.ToString() ?? "";
            var month = (Grid)Item(calendar).Template.FindName("PART_MonthView", Item(calendar));
            var weekdayTitles = month.Children.Cast<FrameworkElement>().Where(e => Grid.GetRow(e) == 0).Select(e => e.DataContext?.ToString()).ToArray();
            var expectedTitles = CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.ShortestDayNames;
            if (!header.Equals(day.ToString("Y", CultureInfo.GetCultureInfo("pt-BR")), StringComparison.CurrentCultureIgnoreCase)
                || !weekdayTitles.SequenceEqual(expectedTitles) || calendar.FirstDayOfWeek != DayOfWeek.Sunday)
                throw new InvalidOperationException("Mês, ano e cabeçalhos dos dias devem aparecer em português, começando no domingo.");
            CaptureElement(calendar, Path.Combine(output, "131-calendario-em-portugues.png"));
            Click(Part(calendar, "PART_PreviousButton")); FlushLayout(window);
            if (calendar.DisplayDate.Month != 9 || calendar.DisplayDate.Year != 2026 || vm.SalesFrom is not null || vm.SalesFilteredCount != 4)
                throw new InvalidOperationException("Navegar para o mês anterior não deve escolher data nem aplicar o filtro.");
            Click(Part(calendar, "PART_NextButton")); FlushLayout(window);
            if (calendar.DisplayDate.Month != 10 || calendar.DisplayDate.Year != 2026) throw new InvalidOperationException("Próximo mês deve retornar corretamente ao mês exibido.");
            Click(Part(calendar, "PART_HeaderButton")); FlushLayout(window);
            if (calendar.DisplayMode != CalendarMode.Year) throw new InvalidOperationException("Cabeçalho deve abrir a escolha de mês.");
            CaptureElement(calendar, Path.Combine(output, "132-escolher-mes.png"));
            Click(Part(calendar, "PART_HeaderButton")); FlushLayout(window);
            if (calendar.DisplayMode != CalendarMode.Decade) throw new InvalidOperationException("Segundo acionamento do cabeçalho deve abrir a escolha de ano.");
            CaptureElement(calendar, Path.Combine(output, "133-escolher-ano.png"));
            var year = Descendants<CalendarButton>(calendar).Single(b => b.DataContext is DateTime date && date.Year == 2026);
            Click(year); FlushLayout(window);
            if (calendar.DisplayMode != CalendarMode.Year) throw new InvalidOperationException("Escolher um ano deve mostrar seus meses.");
            var october = Descendants<CalendarButton>(calendar).Single(b => b.DataContext is DateTime date && date.Month == 10 && date.Year == 2026);
            Click(october); FlushLayout(window);
            if (calendar.DisplayMode != CalendarMode.Month || calendar.DisplayDate.Month != 10) throw new InvalidOperationException("Escolher outubro deve voltar aos dias do mês correto.");
            Select(calendar, day);
            var selectedDay = Descendants<CalendarDayButton>(calendar).Single(b => b.IsSelected);
            if (from.SelectedDate != day || vm.SalesFrom != day || vm.SalesFilteredCount != 4
                || selectedDay.Background is not SolidColorBrush selectedBrush || selectedBrush.Color != (Color)ColorConverter.ConvertFromString("#187B52")
                || selectedDay.Foreground is not SolidColorBrush selectedInk || selectedInk.Color != Colors.White)
                throw new InvalidOperationException("Dia selecionado deve ficar verde com número branco e mudar somente o filtro em preparação.");
            CaptureElement(calendar, Path.Combine(output, "134-dia-selecionado-verde.png"));
            CloseByKey(calendar, Key.Enter);
            until.DisplayDate = day; calendar = Open(until); Select(calendar, day); CloseByKey(calendar, Key.Enter);
            if (vm.SalesUntil != day || vm.SalesFilteredCount != 4) throw new InvalidOperationException("Data final precisa ficar em preparação até aplicar.");
            Click(apply); FlushLayout(window);
            var matching = new[] { sales[1].Id, sales[2].Id };
            if (vm.SalesFilteredCount != 2 || !vm.SalesFilteredItems.Select(s => s.Id).ToHashSet().SetEquals(matching))
                throw new InvalidOperationException("Aplicar o dia completo deve incluir zero hora e o último instante, excluindo os dias vizinhos.");
            calendar = Open(from); Select(calendar, day.AddDays(1)); CloseByKey(calendar, Key.Escape);
            if (from.SelectedDate != day || vm.SalesFrom != day || vm.SalesFilteredCount != 2)
                throw new InvalidOperationException("Esc deve restaurar a data original sem alterar resultados aplicados.");

            Type(from, "11/10/2026");
            if (from.SelectedDate != day.AddDays(-1) || vm.SalesFrom != day.AddDays(-1) || vm.SalesFilteredCount != 2)
                throw new InvalidOperationException("Digitar dia/mês/ano deve manter formato brasileiro e filtro em preparação.");
            int invalidCalls = 0; from.DateValidationError += (_, _) => invalidCalls++;
            Type(from, "31/02/2026");
            if (invalidCalls != 1 || from.SelectedDate != day.AddDays(-1) || vm.SalesFrom != day.AddDays(-1) || vm.SalesFilteredCount != 2)
                throw new InvalidOperationException("Data inválida deve ser recusada mantendo a última data válida e os resultados aplicados.");
            Type(from, ""); CheckPlaceholder(from);
            if (vm.SalesFrom is not null || vm.SalesFilteredCount != 2) throw new InvalidOperationException("Apagar a data remove somente o limite em preparação.");
            Click(clear); FlushLayout(window); CheckPlaceholder(from); CheckPlaceholder(until);
            if (vm.SalesFilteredCount != 4 || vm.SalesUntil is not null) throw new InvalidOperationException("Limpar deve remover as datas e recuperar todas as vendas.");
            Type(until, "31/12/9999"); Click(apply); FlushLayout(window);
            if (vm.SalesUntil != DateTime.MaxValue.Date || vm.SalesFilteredCount != 4) throw new InvalidOperationException("Data final no limite máximo deve incluir as vendas sem ultrapassar o calendário.");
            Type(from, "01/01/0001"); Click(apply); FlushLayout(window);
            if (vm.SalesFrom != DateTime.MinValue || vm.SalesFilteredCount != 4) throw new InvalidOperationException("Data inicial no limite mínimo deve permanecer válida sem perder registros.");
            Click(clear); FlushLayout(window);
            from.BlackoutDates.Add(new CalendarDateRange(day.AddDays(-1))); from.DisplayDate = day; calendar = Open(from);
            var blackedOut = Descendants<CalendarDayButton>(calendar).Single(b => b.DataContext is DateTime date && date == day.AddDays(-1));
            Click(blackedOut); FlushLayout(window);
            if (!blackedOut.IsBlackedOut || from.SelectedDate is not null || vm.SalesFrom is not null) throw new InvalidOperationException("Dia indisponível nativo deve permanecer sem seleção.");
            from.IsDropDownOpen = false; from.BlackoutDates.Clear(); FlushLayout(window);
            from.IsEnabled = false; from.IsDropDownOpen = true; FlushLayout(window);
            if (from.IsDropDownOpen || vm.SalesFrom is not null) throw new InvalidOperationException("Campo de data desabilitado não deve abrir calendário nem alterar filtro.");
            from.IsEnabled = true;

            window.Width = 1200; window.Height = 760; from.DisplayDate = day; calendar = Open(from);
            CaptureElement(calendar, Path.Combine(output, "135-calendario-tela-menor.png"));
            var visibleDays = Descendants<CalendarDayButton>(calendar).Where(b => b.IsVisible).ToArray();
            double availableWidth = Math.Min(window.ActualWidth - 40, SystemParameters.WorkArea.Width - 24);
            double availableHeight = Math.Min(window.ActualHeight - 40, SystemParameters.WorkArea.Height - 24);
            if (calendar.ActualWidth > availableWidth || calendar.ActualHeight > availableHeight || calendar.ActualWidth < 280
                || visibleDays.Length != 42 || visibleDays.Any(b => b.ActualWidth < 30 || b.ActualHeight < 30))
                throw new InvalidOperationException($"Calendário deve caber na janela menor com dias legíveis e áreas de seleção suficientes. "
                    + $"Janela {window.ActualWidth:0.##} × {window.ActualHeight:0.##}; calendário {calendar.ActualWidth:0.##} × {calendar.ActualHeight:0.##}; "
                    + $"área disponível {availableWidth:0.##} × {availableHeight:0.##}; "
                    + $"dias visíveis {visibleDays.Length}; menor dia {visibleDays.Select(b => b.ActualWidth).DefaultIfEmpty(0).Min():0.##} × {visibleDays.Select(b => b.ActualHeight).DefaultIfEmpty(0).Min():0.##}.");
            foreach (var picker in new[] { from, until })
            {
                var input = Input(picker); input.ApplyTemplate(); var host = input.Template.FindName("PART_ContentHost", input) as ScrollViewer;
                if (picker.ActualWidth < 140 || host is null || host.ViewportWidth <= 0 || host.ViewportHeight + .5 < input.FontSize * input.FontFamily.LineSpacing)
                    throw new InvalidOperationException("Campos de data devem preservar a linha de texto inteira na janela menor.");
            }
            from.IsDropDownOpen = false; FlushLayout(window); Capture(window, Path.Combine(output, "136-campos-datas-tela-menor.png"));
            if (JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions) != stable)
                throw new InvalidOperationException("Navegar, selecionar, digitar, recusar datas e filtrar não deve modificar os dados do depósito.");
        }
        finally { if (opened is not null) opened.IsDropDownOpen = false; window.Close(); }
        File.WriteAllText(Path.Combine(output, "calendario-smoke-ok.txt"), "Calendário nativo branco e arredondado em português; seleção verde; navegação mês/ano/década; datas em preparação até aplicar; limites inclusivos dos dias; Esc restaura seleção; digitação brasileira, data inválida, campos vazios, limites mínimo e máximo, dia indisponível e desabilitado; janela 1200 x 760 e preservação integral dos dados conferidos.\n");
    }
}
