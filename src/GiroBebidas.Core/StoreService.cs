using System.Text.RegularExpressions;

namespace GiroBebidas.Core;

public sealed partial class StoreService(JsonStore store)
{
    public JsonStore Store { get; } = store;
    public Guid? CurrentUserId { get; private set; }
    public StoreState Snapshot => Store.Snapshot();
    public UserAccount? CurrentUser => Snapshot.Users.FirstOrDefault(u => u.Id == CurrentUserId && u.Active);
    public bool IsAdmin => CurrentUser?.Role == UserRole.Administrador;
    public UserAccount Login(string username, string password)
    {
        var candidate = Snapshot.Users.FirstOrDefault(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (candidate is null || !candidate.Active || !Passwords.Verify(candidate, password))
            throw new InvalidOperationException("Usuário ou senha incorretos.");
        UserAccount user = Store.Update(s =>
        {
            UserAccount actual = s.Users.Single(u => u.Id == candidate.Id);
            actual.LastLogin = DateTimeOffset.Now;
            Log(s, actual, "Acesso ao sistema");
            return actual;
        });
        CurrentUserId = user.Id;
        return user;
    }
    public void Logout() => CurrentUserId = null;
    private UserAccount Actor(StoreState s, bool admin = false)
    {
        UserAccount? actor = s.Users.FirstOrDefault(u => u.Id == CurrentUserId && u.Active);
        if (actor is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        if (admin && actor.Role != UserRole.Administrador) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
        return actor;
    }
    private static void Log(StoreState s, UserAccount actor, string action) => s.Audit.Add(new() { UserName = actor.Username, Action = action });
    private static string Required(string value, string field, int max = 180)
    {
        string result = value.Trim();
        if (result.Length == 0 || result.Length > max) throw new InvalidOperationException($"Informe {field} (até {max} caracteres).");
        return result;
    }
    private static void Currency(decimal value, string field, bool positive = false)
    {
        if (value < 0 || value > 999_999_999 || value != Money.Round(value) || (positive && value == 0))
            throw new InvalidOperationException($"{field}: informe um valor {(positive ? "maior que zero" : "não negativo")} com até 2 casas decimais.");
    }
    private static void Quantity(decimal value, Product p)
    {
        if (value <= 0 || value > 1_000_000 || decimal.Round(value, 3) != value || (!p.IsWeighed && decimal.Truncate(value) != value))
            throw new InvalidOperationException($"Quantidade inválida para {p.Name}. {(p.IsWeighed ? "Use até 3 casas decimais." : "Use unidades inteiras.")}");
    }
    public static decimal Reserved(StoreState s, Guid productId, Guid? excludeOrder = null)
        => s.Orders.Where(o => o.IsOpen && o.Id != excludeOrder).SelectMany(o => o.Lines).Where(l => l.ProductId == productId).Sum(l => l.Quantity);
    public static decimal Available(StoreState s, Product p, Guid? excludeOrder = null) => p.Stock - Reserved(s, p.Id, excludeOrder);
    public decimal Available(Guid productId, Guid? excludeOrder = null)
    {
        var s = Snapshot;
        return Available(s, s.Products.Single(p => p.Id == productId), excludeOrder);
    }

    public void SaveUser(UserAccount input, string? password)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            input.Name = Required(input.Name, "o nome", 80);
            input.Username = Required(input.Username, "o usuário", 30).ToUpperInvariant();
            if (!Regex.IsMatch(input.Username, "^[A-Z0-9_.-]+$")) throw new InvalidOperationException("Use letras, números, ponto, hífen ou sublinhado no usuário.");
            if (!Enum.IsDefined(input.Role)) throw new InvalidOperationException("Perfil inválido.");
            if (s.Users.Any(u => u.Id != input.Id && u.Username.Equals(input.Username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Este usuário já existe.");
            var existing = s.Users.FirstOrDefault(u => u.Id == input.Id);
            if (existing is null)
            {
                Passwords.Set(input, password ?? "");
                s.Users.Add(input);
            }
            else
            {
                if (existing.Id == actor.Id && !input.Active) throw new InvalidOperationException("Você não pode desativar sua própria conta.");
                if (existing.Active && existing.Role == UserRole.Administrador && (!input.Active || input.Role != UserRole.Administrador)
                    && s.Users.Count(u => u.Active && u.Role == UserRole.Administrador) <= 1)
                    throw new InvalidOperationException("Mantenha pelo menos um administrador ativo.");
                existing.Name = input.Name; existing.Username = input.Username; existing.Role = input.Role; existing.Active = input.Active;
                if (!string.IsNullOrEmpty(password)) Passwords.Set(existing, password);
            }
            Log(s, actor, $"Usuário salvo: {input.Username}");
            return 0;
        });
    }
    public void ChangePassword(string oldPassword, string newPassword)
    {
        Store.Update(s =>
        {
            var actor = Actor(s);
            if (!Passwords.Verify(actor, oldPassword)) throw new InvalidOperationException("A senha atual está incorreta.");
            Passwords.Set(actor, newPassword);
            Log(s, actor, "Alteração da própria senha");
            return 0;
        });
    }
    public void SaveProduct(Product input)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            input.Name = Required(input.Name, "o nome do produto", 100);
            if (input.Description.Length > 2000) throw new InvalidOperationException("Use até 2000 caracteres na descrição.");
            if (input.PhotoBase64.Length > 2_000_000) throw new InvalidOperationException("A foto está muito grande.");
            if (input.PhotoBase64.Length > 0) { try { _ = Convert.FromBase64String(input.PhotoBase64); } catch (FormatException) { throw new InvalidOperationException("Foto inválida."); } }
            input.Code = Required(input.Code, "o código", 30);
            input.Barcode = input.Barcode.Trim();
            input.Category = Required(input.Category, "a categoria", 40);
            if (input.Unit is not ("un" or "cx" or "kg")) throw new InvalidOperationException("Unidade inválida.");
            FiscalValidation.ValidateProduct(input.Tax);
            Currency(input.Price, "Preço", true); Currency(input.Cost, "Custo"); Currency(input.WholesalePrice, "Preço de atacado");
            if (input.Stock < 0 || input.MinimumStock < 0 || input.WholesaleMinimum < 0) throw new InvalidOperationException("Estoque e quantidades não podem ser negativos.");
            if (input.Stock > 0) Quantity(input.Stock, input);
            if (input.MinimumStock > 0) Quantity(input.MinimumStock, input);
            if (input.WholesaleMinimum > 0) Quantity(input.WholesaleMinimum, input);
            if ((input.WholesalePrice == 0) != (input.WholesaleMinimum == 0)) throw new InvalidOperationException("Informe preço e quantidade mínima de atacado juntos, ou deixe ambos em zero.");
            if (s.Products.Any(p => p.Id != input.Id && (p.Code.Equals(input.Code, StringComparison.OrdinalIgnoreCase)
                || (input.Barcode.Length > 0 && p.Barcode == input.Barcode)))) throw new InvalidOperationException("Código ou código de barras já cadastrado.");
            var existing = s.Products.FirstOrDefault(p => p.Id == input.Id);
            if (existing is not null)
            {
                if (Reserved(s, input.Id) > 0 && (!input.Active || input.Unit != existing.Unit)) throw new InvalidOperationException("Produto reservado em pedido: mantenha ativo e preserve a unidade.");
                if (input.Unit != existing.Unit && (existing.Stock > 0 || s.Sales.Any(v => v.Lines.Any(l => l.ProductId == input.Id)) || s.Orders.Any(o => o.Lines.Any(l => l.ProductId == input.Id))))
                    throw new InvalidOperationException("Um produto com estoque ou histórico não pode trocar de unidade. Cadastre outro produto.");
                input.Stock = existing.Stock;
                s.Products[s.Products.IndexOf(existing)] = input;
            }
            else { s.Products.Add(input); if (input.Stock > 0) StockLog(s, actor, input, input.Stock, "Estoque inicial"); }
            Log(s, actor, $"Produto salvo: {input.Code}");
            return 0;
        });
    }
    public void SetProductActive(Guid id, bool active)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var product = s.Products.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Produto não encontrado.");
            if (!active && Reserved(s, id) > 0) throw new InvalidOperationException("Produto reservado em pedido: mantenha ativo.");
            product.Active = active;
            Log(s, actor, $"Produto {(active ? "ativado" : "desativado")}: {product.Code}");
            return 0;
        });
    }
    public static bool CanDeleteProduct(StoreState s, Guid id)
    {
        var product = s.Products.FirstOrDefault(p => p.Id == id);
        return product is not null && product.Stock == 0 && Reserved(s, id) == 0
            && !s.Sales.Any(sale => sale.Lines.Any(line => line.ProductId == id))
            && !s.Orders.Any(order => order.Lines.Any(line => line.ProductId == id))
            && !s.StockMovements.Any(movement => movement.ProductId == id);
    }
    public void DeleteProduct(Guid id)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var product = s.Products.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Produto não encontrado.");
            if (!CanDeleteProduct(s, id))
                throw new InvalidOperationException("Só é possível excluir produtos sem estoque, reservas ou histórico. Desative o produto para preservar os registros.");
            s.Products.Remove(product);
            Log(s, actor, $"Produto excluído: {product.Code} • {product.Name}");
            return 0;
        });
    }
    private static void StockLog(StoreState s, UserAccount actor, Product p, decimal quantity, string reason)
        => s.StockMovements.Add(new() { ProductId = p.Id, ProductName = p.Name, Quantity = quantity, Reason = reason, UserName = actor.Username });
    public void AdjustStock(Guid id, decimal delta, string reason, decimal? newCost = null)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var p = s.Products.Single(p => p.Id == id);
            Quantity(Math.Abs(delta), p);
            reason = Required(reason, "o motivo");
            if (Available(s, p) + delta < 0) throw new InvalidOperationException("O ajuste deixaria o estoque negativo ou abaixo do reservado em pedidos.");
            if (newCost is not null) { Currency(newCost.Value, "Custo"); p.Cost = newCost.Value; }
            p.Stock += delta;
            StockLog(s, actor, p, delta, reason);
            Log(s, actor, $"Estoque {p.Code}: {delta:+0.###;-0.###}");
            return 0;
        });
    }
    public void SaveCustomer(Customer input)
    {
        Store.Update(s =>
        {
            var actor = Actor(s);
            input.Name = Required(input.Name, "o nome do cliente", 100);
            Currency(input.CreditLimit, "Limite de fiado");
            if (Balance(s, input.Id) > input.CreditLimit) throw new InvalidOperationException("O limite não pode ser menor que o saldo devedor atual.");
            var existing = s.Customers.FirstOrDefault(c => c.Id == input.Id);
            // Blocking credit requires its own administrative action, even with a stale form.
            input.CreditBlocked = existing?.CreditBlocked ?? false;
            if (existing is null) s.Customers.Add(input); else s.Customers[s.Customers.IndexOf(existing)] = input;
            Log(s, actor, $"Cliente salvo: {input.Name}"); return 0;
        });
    }

    private static List<SaleLine> MakeLines(StoreState s, IReadOnlyList<DraftItem> items, Guid? orderId = null)
    {
        if (items.Count == 0) throw new InvalidOperationException("Adicione produtos à venda.");
        foreach (var item in items) Quantity(item.Quantity, s.Products.FirstOrDefault(p => p.Id == item.ProductId) ?? throw new InvalidOperationException("Produto não encontrado."));
        List<SaleLine> lines = [];
        foreach (var item in items.GroupBy(i => i.ProductId).Select(g => new DraftItem(g.Key, g.Sum(x => x.Quantity))))
        {
            var p = s.Products.FirstOrDefault(p => p.Id == item.ProductId && p.Active) ?? throw new InvalidOperationException("Produto indisponível.");
            Quantity(item.Quantity, p);
            if (Available(s, p, orderId) < item.Quantity) throw new InvalidOperationException($"Estoque insuficiente para {p.Name}.");
            lines.Add(new() { ProductId = p.Id, ProductName = p.Name, Unit = p.Unit, Quantity = item.Quantity, UnitPrice = p.UnitPrice(item.Quantity), UnitCost = p.Cost });
        }
        return lines;
    }
    private static decimal ApplyDiscount(List<SaleLine> lines, decimal discount, decimal deliveryFee)
    {
        Currency(discount, "Desconto"); Currency(deliveryFee, "Taxa de entrega");
        decimal subtotal = lines.Sum(l => l.GrossTotal);
        if (subtotal <= 0) throw new InvalidOperationException("O valor dos produtos deve ser maior que zero.");
        if (discount > subtotal) throw new InvalidOperationException("O desconto não pode superar o valor dos produtos.");
        decimal allocated = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            decimal share = i == lines.Count - 1 ? discount - allocated : Math.Min(discount - allocated, Math.Min(lines[i].GrossTotal, Money.Round(discount * lines[i].GrossTotal / subtotal)));
            share = Math.Max(0, Math.Min(share, lines[i].GrossTotal));
            lines[i].NetRevenue = lines[i].GrossTotal - share;
            allocated += share;
        }
        // Allocate any rounding remainder to lines with capacity; totals always reconcile to cents.
        decimal remaining = discount - allocated;
        foreach (var line in lines)
        {
            if (remaining == 0) break;
            decimal take = Math.Min(remaining, line.NetRevenue); line.NetRevenue -= take; remaining -= take;
        }
        return subtotal - discount + deliveryFee;
    }
    public Sale CompleteSale(SaleRequest request)
    {
        return Store.Update(s =>
        {
            var actor = Actor(s);
            var cash = OpenSession(s);
            Order? order = request.OrderId is null ? null : s.Orders.FirstOrDefault(o => o.Id == request.OrderId && o.IsOpen)
                ?? throw new InvalidOperationException("Pedido já concluído ou cancelado.");
            var lines = MakeLines(s, order is null ? request.Items : order.Lines.Select(l => new DraftItem(l.ProductId, l.Quantity)).ToList(), order?.Id);
            if (order is not null)
                foreach (var line in lines) line.UnitPrice = order.Lines.Single(l => l.ProductId == line.ProductId).UnitPrice;
            decimal discount = order?.Discount ?? request.Discount, fee = order?.DeliveryFee ?? request.DeliveryFee;
            decimal total = ApplyDiscount(lines, discount, fee);
            if (total <= 0) throw new InvalidOperationException("O total da venda deve ser maior que zero.");
            if (!Enum.IsDefined(request.Method)) throw new InvalidOperationException("Forma de pagamento inválida.");
            Currency(request.Tendered, "Valor recebido");
            if (request.Method == PaymentMethod.Dinheiro && request.Tendered < total) throw new InvalidOperationException("O valor recebido é menor que o total da venda.");
            Guid? customerId = order?.CustomerId ?? request.CustomerId;
            Customer? customer = customerId is null ? null : s.Customers.FirstOrDefault(c => c.Id == customerId) ?? throw new InvalidOperationException("Cliente não encontrado.");
            if (request.Method == PaymentMethod.Fiado)
            {
                if (customer is null) throw new InvalidOperationException("Selecione um cliente para vender fiado.");
                if (customer.CreditBlocked) throw new InvalidOperationException("O fiado deste cliente está bloqueado. Escolha outra forma de pagamento.");
                if (Balance(s, customer.Id) + total > customer.CreditLimit) throw new InvalidOperationException("Esta venda ultrapassa o limite de fiado do cliente.");
            }
            Sale sale = new()
            {
                Number = s.NextSaleNumber++, UserId = actor.Id, UserName = actor.Username, CashSessionId = cash.Id, CustomerId = customer?.Id,
                CustomerName = customer?.Name ?? "Consumidor final", CustomerPhone = customer?.Phone ?? "", Lines = lines, Discount = discount, DeliveryFee = fee,
                PaymentMethod = request.Method, Tendered = request.Method == PaymentMethod.Dinheiro ? request.Tendered : total,
                DeliveryAddress = order?.Address ?? request.Address, Notes = order?.Notes ?? request.Notes, OrderId = order?.Id
            };
            foreach (var line in lines)
            {
                var p = s.Products.Single(p => p.Id == line.ProductId); p.Stock -= line.Quantity;
                StockLog(s, actor, p, -line.Quantity, $"Venda {sale.NumberLabel}");
            }
            s.Sales.Add(sale);
            if (order is not null) order.Status = OrderStatus.Concluido;
            Log(s, actor, $"Venda {sale.NumberLabel}: {sale.Total:0.00}");
            return sale;
        });
    }
    public void CancelSale(Guid saleId, string reason)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true); var cash = OpenSession(s);
            var sale = s.Sales.Single(x => x.Id == saleId);
            if (sale.Canceled) throw new InvalidOperationException("Esta venda já foi cancelada.");
            if (sale.CashSessionId != cash.Id) throw new InvalidOperationException("Cancelamentos são permitidos apenas no caixa de origem ainda aberto.");
            if (s.ReceivablePayments.Any(p => p.SaleId == sale.Id)) throw new InvalidOperationException("Esta venda já possui recebimentos de fiado e não pode ser cancelada.");
            if (sale.PaymentMethod == PaymentMethod.Dinheiro && SummarizeCash(s, cash.Id).Expected < sale.Total)
                throw new InvalidOperationException("O caixa não tem dinheiro suficiente para o estorno. Registre um suprimento antes de cancelar.");
            sale.CancelReason = Required(reason, "o motivo do cancelamento"); sale.Canceled = true;
            foreach (var line in sale.Lines)
            {
                var p = s.Products.Single(p => p.Id == line.ProductId); p.Stock += line.Quantity;
                StockLog(s, actor, p, line.Quantity, $"Cancelamento {sale.NumberLabel}: {reason}");
            }
            if (sale.OrderId is not null) s.Orders.Single(o => o.Id == sale.OrderId).Status = OrderStatus.Cancelado;
            Log(s, actor, $"Venda cancelada: {sale.NumberLabel} • {reason}"); return 0;
        });
    }

    public Order CreateOrder(IReadOnlyList<DraftItem> items, Guid customerId, bool delivery, string address, string notes, decimal discount, decimal fee)
    {
        return Store.Update(s =>
        {
            var actor = Actor(s);
            var customer = s.Customers.SingleOrDefault(c => c.Id == customerId) ?? throw new InvalidOperationException("Selecione o cliente do pedido.");
            var lines = MakeLines(s, items);
            if (ApplyDiscount(lines, discount, fee) <= 0) throw new InvalidOperationException("O pedido deve ter total maior que zero.");
            if (delivery) address = Required(address, "o endereço de entrega", 300);
            Order order = new() { Number = s.NextOrderNumber++, CustomerId = customer.Id, CustomerName = customer.Name, Phone = customer.Phone,
                Delivery = delivery, Address = delivery ? address : "", Notes = notes, Discount = discount, DeliveryFee = fee, Lines = lines };
            s.Orders.Add(order); Log(s, actor, $"Pedido criado: {order.NumberLabel}"); return order;
        });
    }
    public void SetOrderStatus(Guid id, OrderStatus status)
    {
        Store.Update(s =>
        {
            var actor = Actor(s); var order = s.Orders.Single(o => o.Id == id);
            if (!order.IsOpen) throw new InvalidOperationException("Este pedido está encerrado.");
            if (status is not (OrderStatus.Pendente or OrderStatus.Preparando or OrderStatus.EmEntrega or OrderStatus.Cancelado))
                throw new InvalidOperationException("Para concluir o pedido, receba o pagamento no PDV.");
            if (!order.Delivery && status == OrderStatus.EmEntrega) throw new InvalidOperationException("Este pedido é de retirada.");
            order.Status = status; Log(s, actor, $"Pedido {order.NumberLabel}: {Labels.Order(status)}"); return 0;
        });
    }
    private static CashSession OpenSession(StoreState s) => s.CashSessions.SingleOrDefault(c => c.ClosedAt is null)
        ?? throw new InvalidOperationException("Abra o caixa antes de registrar esta movimentação.");
    public CashSession OpenCash(decimal opening)
    {
        return Store.Update(s =>
        {
            var actor = Actor(s); Currency(opening, "Fundo de troco");
            if (s.CashSessions.Any(c => c.ClosedAt is null)) throw new InvalidOperationException("Já existe um caixa aberto.");
            CashSession session = new() { OpeningAmount = opening, OpenedBy = actor.Username };
            s.CashSessions.Add(session); Log(s, actor, $"Caixa aberto: {opening:0.00}"); return session;
        });
    }
    public void MoveCash(CashMovementKind kind, decimal amount, string notes)
    {
        Store.Update(s =>
        {
            var actor = Actor(s); var cash = OpenSession(s); Currency(amount, "Valor", true);
            if (!Enum.IsDefined(kind)) throw new InvalidOperationException("Tipo de movimentação inválido.");
            if (kind == CashMovementKind.Sangria && amount > SummarizeCash(s, cash.Id).Expected) throw new InvalidOperationException("A sangria supera o dinheiro disponível no caixa.");
            s.CashMovements.Add(new() { SessionId = cash.Id, Kind = kind, Amount = amount, Notes = Required(notes, "o motivo"), UserName = actor.Username });
            Log(s, actor, $"{kind}: {amount:0.00}"); return 0;
        });
    }
    public CashSession CloseCash(decimal counted, string notes)
    {
        return Store.Update(s =>
        {
            var actor = Actor(s); var cash = OpenSession(s); Currency(counted, "Dinheiro contado");
            cash.ExpectedCash = SummarizeCash(s, cash.Id).Expected; cash.CountedCash = counted; cash.ClosedAt = DateTimeOffset.Now;
            cash.ClosedBy = actor.Username; cash.ClosingNotes = notes;
            Log(s, actor, $"Caixa fechado: contado {counted:0.00}, diferença {cash.Difference:0.00}"); return cash;
        });
    }
    public static CashSummary SummarizeCash(StoreState s, Guid sessionId)
    {
        var session = s.CashSessions.Single(c => c.Id == sessionId);
        var sales = s.Sales.Where(v => v.CashSessionId == sessionId && !v.Canceled).ToList();
        var receipts = s.ReceivablePayments.Where(p => p.CashSessionId == sessionId).ToList();
        decimal Payment(PaymentMethod method) => sales.Where(v => v.PaymentMethod == method).Sum(v => v.Total) + receipts.Where(v => v.Method == method).Sum(v => v.Amount);
        var movements = s.CashMovements.Where(m => m.SessionId == sessionId).ToList();
        decimal supplies = movements.Where(m => m.Kind == CashMovementKind.Suprimento).Sum(m => m.Amount);
        decimal withdrawals = movements.Where(m => m.Kind == CashMovementKind.Sangria).Sum(m => m.Amount);
        decimal cash = Payment(PaymentMethod.Dinheiro);
        return new(session.OpeningAmount, cash, Payment(PaymentMethod.Pix), Payment(PaymentMethod.Debito), Payment(PaymentMethod.Credito),
            sales.Where(v => v.PaymentMethod == PaymentMethod.Fiado).Sum(v => v.Total), supplies, withdrawals, session.OpeningAmount + cash + supplies - withdrawals,
            sales.Sum(v => v.Total), sales.Count);
    }
    public static decimal Outstanding(StoreState s, Sale sale) => sale.Canceled || sale.PaymentMethod != PaymentMethod.Fiado ? 0
        : sale.Total - s.ReceivablePayments.Where(p => p.SaleId == sale.Id).Sum(p => p.Amount);
    public static decimal Balance(StoreState s, Guid customerId) => s.Sales.Where(v => v.CustomerId == customerId).Sum(v => Outstanding(s, v));
    public void ReceiveDebt(Guid customerId, decimal amount, PaymentMethod method)
    {
        Store.Update(s =>
        {
            var actor = Actor(s); var cash = OpenSession(s); Currency(amount, "Valor recebido", true);
            if (!Enum.IsDefined(method) || method == PaymentMethod.Fiado) throw new InvalidOperationException("Escolha dinheiro, Pix ou cartão.");
            if (!s.Customers.Any(c => c.Id == customerId)) throw new InvalidOperationException("Cliente não encontrado.");
            if (amount > Balance(s, customerId)) throw new InvalidOperationException("O valor supera o saldo devedor.");
            decimal remaining = amount;
            foreach (var sale in s.Sales.Where(v => v.CustomerId == customerId && !v.Canceled && v.PaymentMethod == PaymentMethod.Fiado).OrderBy(v => v.Date))
            {
                decimal payment = Math.Min(Outstanding(s, sale), remaining);
                if (payment > 0) s.ReceivablePayments.Add(new() { SaleId = sale.Id, CashSessionId = cash.Id, Amount = payment, Method = method, UserName = actor.Username });
                remaining -= payment;
                if (remaining == 0) break;
            }
            Log(s, actor, $"Recebimento de fiado: {amount:0.00}"); return 0;
        });
    }
    public static List<ProfitRow> ProfitReport(StoreState s, DateTimeOffset? from = null, DateTimeOffset? until = null)
        => s.Sales.Where(v => !v.Canceled && (from is null || v.Date >= from) && (until is null || v.Date < until))
            .SelectMany(v => v.Lines).GroupBy(l => l.ProductId)
            .Select(g => new ProfitRow(g.Last().ProductName, g.Sum(l => l.Quantity), g.Sum(l => l.NetRevenue),
                g.Sum(l => Money.Round(l.Quantity * l.UnitCost)), g.Sum(l => l.Profit)) { ProductId = g.Key }).OrderByDescending(r => r.Profit).ToList();
    internal static void ValidateCashOpeningDefault(decimal value) => Currency(value, "Fundo de troco sugerido");
    public void SaveSettings(StoreSettings settings)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true); settings.Name = Required(settings.Name, "o nome do depósito", 80);
            ValidateCashOpeningDefault(settings.CashOpeningDefault);
            settings.GitHubUpdates = ValidateUpdateSettings(settings.GitHubUpdates);
            if (settings.DefaultPrinterId is not null && !s.Printers.Any(p => p.Id == settings.DefaultPrinterId)) settings.DefaultPrinterId = null;
            if (settings.DefaultScaleId is not null && !s.Scales.Any(p => p.Id == settings.DefaultScaleId)) settings.DefaultScaleId = null;
            s.Settings = settings; Log(s, actor, "Configurações atualizadas"); return 0;
        });
    }
    public void SavePrinter(PrinterProfile profile, bool makeDefault)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true); profile.Name = Required(profile.Name, "o nome da impressora", 80);
            profile.QueueName = Required(profile.QueueName, "a impressora instalada no Windows");
            if (profile.Paper is not ("58 mm" or "80 mm" or "A4")) throw new InvalidOperationException("Formato de papel inválido.");
            s.Printers.RemoveAll(p => p.Id == profile.Id); s.Printers.Add(profile);
            if (makeDefault || s.Settings.DefaultPrinterId is null) s.Settings.DefaultPrinterId = profile.Id;
            Log(s, actor, $"Impressora cadastrada: {profile.Name}"); return 0;
        });
    }
    public void SaveScale(ScaleProfile profile, bool makeDefault)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true); profile.Name = Required(profile.Name, "o nome da balança", 80); profile.Port = profile.Port.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(profile.Port, "^COM[1-9][0-9]{0,3}$")) throw new InvalidOperationException("Informe uma porta como COM1 ou COM3.");
            if (profile.BaudRate is not (1200 or 2400 or 4800 or 9600 or 19200 or 38400 or 57600 or 115200)) throw new InvalidOperationException("Velocidade serial inválida.");
            if (profile.Protocol is not ("Texto em kg" or "Inteiro em gramas")) throw new InvalidOperationException("Protocolo inválido.");
            profile.CommandHex = profile.CommandHex.Replace(" ", "").Trim();
            try { if (Convert.FromHexString(profile.CommandHex).Length > 64) throw new InvalidOperationException("Comando muito longo."); }
            catch (FormatException) { throw new InvalidOperationException("Use bytes hexadecimais no comando, por exemplo 05 ou 0D0A."); }
            s.Scales.RemoveAll(p => p.Id == profile.Id); s.Scales.Add(profile);
            if (makeDefault || s.Settings.DefaultScaleId is null) s.Settings.DefaultScaleId = profile.Id;
            Log(s, actor, $"Balança cadastrada: {profile.Name}"); return 0;
        });
    }
    public void DeleteDevice(Guid id, bool printer)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            if (printer)
            {
                if (s.Printers.RemoveAll(p => p.Id == id) == 0) throw new InvalidOperationException("Impressora não encontrada.");
                if (s.Settings.DefaultPrinterId == id) s.Settings.DefaultPrinterId = null;
                if (s.Fiscal.Options.PrinterId == id) s.Fiscal.Options.PrinterId = null;
            }
            else
            {
                if (s.Scales.RemoveAll(p => p.Id == id) == 0) throw new InvalidOperationException("Balança não encontrada.");
                if (s.Settings.DefaultScaleId == id) s.Settings.DefaultScaleId = null;
            }
            Log(s, actor, "Equipamento removido"); return 0;
        });
    }
    public void ExportBackup(string target)
    {
        Actor(Snapshot, true);
        if (Path.GetFullPath(target).Equals(Store.FilePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Escolha um arquivo diferente do banco de dados atual.");
        Store.ExportBackup(target);
    }
    public void RestoreBackup(string source)
    {
        var s = Snapshot; Actor(s, true);
        if (s.CashSessions.Any(c => c.ClosedAt is null)) throw new InvalidOperationException("Feche o caixa antes de restaurar um backup.");
        Store.RestoreBackup(source); Logout();
    }
}
