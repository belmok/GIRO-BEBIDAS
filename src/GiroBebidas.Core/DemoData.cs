namespace GiroBebidas.Core;

public static class DemoData
{
    public static StoreState Create()
    {
        StoreState state = new();
        UserAccount admin = new() { Name = "Administrador", Username = "ADMIN", Role = UserRole.Administrador };
        Passwords.Set(admin, "ADMIN");
        state.Users.Add(admin);
        Add("001", "7890000000011", "Pilsen • lata 350 ml", "Cervejas", 4.50m, 2.90m, 144, "Lata", "#D6A23E", 3.99m, 12);
        Add("002", "7890000000028", "Puro malte • 350 ml", "Cervejas", 6.50m, 4.10m, 96, "Lata", "#24734A", 5.90m, 12);
        Add("003", "7890000000035", "Lager • long neck", "Cervejas", 8.90m, 5.60m, 72, "Garrafa", "#427B55", 7.99m, 6);
        Add("004", "7890000000042", "Pilsen • 600 ml", "Cervejas", 9.50m, 6.20m, 48, "Garrafa", "#A76A33", 8.50m, 12);
        Add("005", "7890000000059", "Cola • 2 litros", "Refrigerantes", 11.90m, 7.50m, 40, "Garrafa", "#C14745");
        Add("006", "7890000000066", "Guaraná • 2 litros", "Refrigerantes", 9.90m, 6.40m, 36, "Garrafa", "#478F4E");
        Add("007", "7890000000073", "Cola • lata 350 ml", "Refrigerantes", 4.90m, 2.85m, 96, "Lata", "#C14745");
        Add("008", "7890000000080", "Tônica • lata 350 ml", "Refrigerantes", 5.90m, 3.60m, 24, "Lata", "#5E798F");
        Add("009", "7890000000097", "Vodka • 1 litro", "Destilados", 39.90m, 27m, 18, "Garrafa", "#758BA2");
        Add("010", "7890000000103", "Whisky • 1 litro", "Destilados", 99.90m, 72m, 8, "Garrafa", "#8E6039");
        Add("011", "7890000000110", "Água mineral • 500 ml", "Água e gelo", 2.50m, 1.10m, 120, "Garrafa", "#5DA2BF");
        Add("012", "7890000000127", "Água com gás • 500 ml", "Água e gelo", 3.50m, 1.70m, 60, "Garrafa", "#68A4AB");
        Add("013", "7890000000134", "Gelo • saco 5 kg", "Água e gelo", 15m, 8m, 32, "Pacote", "#76ADC5");
        Add("014", "7890000000141", "Energético • 473 ml", "Outros", 10.90m, 6.80m, 48, "Lata", "#445163");
        Add("015", "7890000000158", "Carvão • pacote 3 kg", "Outros", 19.90m, 12m, 20, "Pacote", "#6E6256");
        Add("016", "7890000000165", "Gelo a granel", "Água e gelo", 3m, 1.20m, 50, "Pacote", "#76ADC5", unit: "kg");
        return state;
        void Add(string code, string barcode, string name, string category, decimal price, decimal cost, decimal stock, string package, string color, decimal wholesale = 0, decimal threshold = 0, string unit = "un")
            => state.Products.Add(new() { Code = code, Barcode = barcode, Name = name, Category = category, Price = price, Cost = cost, Stock = stock, Package = package, Color = color, WholesalePrice = wholesale, WholesaleMinimum = threshold, Unit = unit });
    }
}
