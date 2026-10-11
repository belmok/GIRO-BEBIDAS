namespace GiroBebidas.Core;

public enum UserRole { Administrador, Operador }
public enum PaymentMethod { Dinheiro, Pix, Debito, Credito, Fiado }
public enum OrderStatus { Pendente, Preparando, EmEntrega, Concluido, Cancelado }
public enum CashMovementKind { Suprimento, Sangria }

public class StoreState
{
    public int SchemaVersion { get; set; } = 1;
    public long NextSaleNumber { get; set; } = 1;
    public long NextOrderNumber { get; set; } = 1;
    public StoreSettings Settings { get; set; } = new();
    public List<UserAccount> Users { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<CashSession> CashSessions { get; set; } = [];
    public List<CashMovement> CashMovements { get; set; } = [];
    public List<ReceivablePayment> ReceivablePayments { get; set; } = [];
    public List<StockMovement> StockMovements { get; set; } = [];
    public List<AuditEntry> Audit { get; set; } = [];
    public List<PrinterProfile> Printers { get; set; } = [];
    public List<ScaleProfile> Scales { get; set; } = [];
}

public class StoreSettings
{
    public string Name { get; set; } = "Giro Bebidas";
    public string Address { get; set; } = "Rua das Palmeiras, 120 • Centro (fictício)";
    public string Phone { get; set; } = "";
    public string Document { get; set; } = "";
    public string ReceiptFooter { get; set; } = "Obrigado pela preferência. Volte sempre!";
    public bool AutoPrint { get; set; }
    public Guid? DefaultPrinterId { get; set; }
    public Guid? DefaultScaleId { get; set; }
}

public class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public int PasswordIterations { get; set; } = Passwords.Iterations;
    public UserRole Role { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset? LastLogin { get; set; }
    public string RoleLabel => Role.ToString();
    public string StatusLabel => Active ? "Ativo" : "Inativo";
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Cervejas";
    public string Unit { get; set; } = "un";
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; } = 10;
    public decimal WholesalePrice { get; set; }
    public decimal WholesaleMinimum { get; set; }
    public bool Active { get; set; } = true;
    public string Color { get; set; } = "#15803D";
    public string Package { get; set; } = "Lata";
    public bool IsWeighed => Unit == "kg";
    public string StockLabel => $"{Stock:0.###} {Unit}";
    public string StockStatus => Stock <= 0 ? "Sem estoque" : Stock <= MinimumStock ? "Estoque baixo" : "Disponível";
    public decimal UnitPrice(decimal quantity) => WholesaleMinimum > 0 && WholesalePrice > 0 && quantity >= WholesaleMinimum ? WholesalePrice : Price;
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Document { get; set; } = "";
    public string Address { get; set; } = "";
    public string Notes { get; set; } = "";
    public decimal CreditLimit { get; set; } = 500;
}

public class SaleLine
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Unit { get; set; } = "un";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal GrossTotal => Money.Round(Quantity * UnitPrice);
    public decimal Profit => NetRevenue - Money.Round(Quantity * UnitCost);
}

public class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Number { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public Guid CashSessionId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Consumidor final";
    public string CustomerPhone { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
    public string Notes { get; set; } = "";
    public Guid? OrderId { get; set; }
    public List<SaleLine> Lines { get; set; } = [];
    public decimal Discount { get; set; }
    public decimal DeliveryFee { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Tendered { get; set; }
    public bool Canceled { get; set; }
    public string CancelReason { get; set; } = "";
    public decimal Subtotal => Lines.Sum(l => l.GrossTotal);
    public decimal Total => Subtotal - Discount + DeliveryFee;
    public decimal Profit => Lines.Sum(l => l.Profit);
    public decimal Change => PaymentMethod == PaymentMethod.Dinheiro ? Math.Max(0, Tendered - Total) : 0;
    public string NumberLabel => $"#{Number:000000}";
    public string StatusLabel => Canceled ? "Cancelada" : "Concluída";
    public string PaymentLabel => Labels.Payment(PaymentMethod);
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Number { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Delivery { get; set; }
    public string Notes { get; set; } = "";
    public List<SaleLine> Lines { get; set; } = [];
    public decimal Discount { get; set; }
    public decimal DeliveryFee { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total => Lines.Sum(l => l.GrossTotal) - Discount + DeliveryFee;
    public string NumberLabel => $"P{Number:00000}";
    public string StatusLabel => Labels.Order(Status);
    public string TypeLabel => Delivery ? "Entrega" : "Retirada";
    public bool IsOpen => Status is not (OrderStatus.Concluido or OrderStatus.Cancelado);
}

public class CashSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ClosedAt { get; set; }
    public string OpenedBy { get; set; } = "";
    public string ClosedBy { get; set; } = "";
    public decimal OpeningAmount { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public string ClosingNotes { get; set; } = "";
    public decimal Difference => CountedCash - ExpectedCash;
    public string StatusLabel => ClosedAt is null ? "Aberto" : "Fechado";
}
public class CashMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public CashMovementKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string Notes { get; set; } = "";
    public string UserName { get; set; } = "";
    public string KindLabel => Kind.ToString();
}
public class ReceivablePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Guid CashSessionId { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string UserName { get; set; } = "";
}
public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = "";
    public string UserName { get; set; } = "";
}
public class AuditEntry
{
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
}
public class PrinterProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string QueueName { get; set; } = "";
    public string Paper { get; set; } = "80 mm";
}
public class ScaleProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Port { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public string Protocol { get; set; } = "Texto em kg";
    public string CommandHex { get; set; } = "";
}
public record DraftItem(Guid ProductId, decimal Quantity);
public record SaleRequest(IReadOnlyList<DraftItem> Items, decimal Discount, decimal DeliveryFee,
    Guid? CustomerId, PaymentMethod Method, decimal Tendered, string Notes = "", string Address = "", Guid? OrderId = null);
public record CashSummary(decimal Opening, decimal CashSales, decimal Pix, decimal Debit, decimal Credit,
    decimal CreditSales, decimal Supplies, decimal Withdrawals, decimal Expected, decimal TotalSales, int SaleCount);
public record ProfitRow(string Name, decimal Quantity, decimal Revenue, decimal Cost, decimal Profit)
{
    public decimal Margin => Revenue == 0 ? 0 : Profit / Revenue;
}
public static class Money { public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero); }
public static class Labels
{
    public static string Payment(PaymentMethod method) => method switch { PaymentMethod.Debito => "Cartão de débito", PaymentMethod.Credito => "Cartão de crédito", _ => method.ToString() };
    public static string Order(OrderStatus status) => status switch { OrderStatus.EmEntrega => "Em entrega", OrderStatus.Concluido => "Concluído", _ => status.ToString() };
}
