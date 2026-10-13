namespace GiroBebidas.Core;

public sealed partial class StoreService
{
    public void SetDefaultPrinter(Guid id)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var printer = s.Printers.FirstOrDefault(p => p.Id == id)
                ?? throw new InvalidOperationException("Impressora não encontrada.");
            s.Settings.DefaultPrinterId = printer.Id;
            Log(s, actor, $"Impressora padrão definida: {printer.Name}");
            return 0;
        });
    }

    public void SetDefaultScale(Guid id)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var scale = s.Scales.FirstOrDefault(p => p.Id == id)
                ?? throw new InvalidOperationException("Balança não encontrada.");
            s.Settings.DefaultScaleId = scale.Id;
            Log(s, actor, $"Balança padrão definida: {scale.Name}");
            return 0;
        });
    }
}
