using System.Text.Json;
using GiroBebidas.Core;

namespace GiroBebidas.Tests;

public static class ProductActionsChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "acoes-produtos", Guid.NewGuid().ToString("N"));
        var service = new StoreService(new JsonStore(directory));
        var admin = service.Login("ADMIN", "ADMIN");
        string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
        static string ProductData(Product product) => JsonSerializer.Serialize(product, JsonStore.JsonOptions);
        void RejectAtomic<T>(Action action, string name, bool deletionHint = false) where T : Exception
        {
            string beforeState = State(), beforeFile = File.ReadAllText(service.Store.FilePath);
            T? rejected = null;
            try { action(); } catch (T ex) { rejected = ex; }
            check(rejected is not null, name);
            check(State() == beforeState && File.ReadAllText(service.Store.FilePath) == beforeFile,
                name + ": estado completo e arquivo preservados");
            if (deletionHint) check(rejected!.Message.Contains("Desative", StringComparison.OrdinalIgnoreCase),
                name + ": mensagem orienta desativar para preservar registros");
        }
        Product AddProduct(string code, decimal stock = 0)
        {
            var product = new Product { Code = code, Name = "Produto " + code, Category = "Outros", Price = 12.99m, Cost = 7.21m, Stock = stock, MinimumStock = 0 };
            service.SaveProduct(product); return product;
        }

        var detailed = new Product
        {
            Code = "ACAO_PERFIL", Barcode = "7891234567890", Name = "Produto com cadastro completo", Description = "Descrição, foto e tributos preservados ao alterar status.",
            Category = "Cervejas", Unit = "un", Stock = 24, MinimumStock = 5, Price = 12.99m, Cost = 7.21m, WholesalePrice = 10.49m, WholesaleMinimum = 12,
            PhotoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }), Color = "#426B52", Package = "Garrafa",
            Tax = new() { Ncm = "22030000", Cest = "0302100", Cfop = "5102", Csosn = "102", Origin = 0 }
        };
        service.SaveProduct(detailed);
        string originalProduct = ProductData(service.Snapshot.Products.Single(p => p.Id == detailed.Id));
        string stockHistory = JsonSerializer.Serialize(service.Snapshot.StockMovements, JsonStore.JsonOptions);
        service.SetProductActive(detailed.Id, false);
        var inactive = service.Snapshot.Products.Single(p => p.Id == detailed.Id);
        string inactiveData = ProductData(inactive);
        check(!inactive.Active, "Desativar altera o status do produto");
        inactive.Active = true;
        check(ProductData(inactive) == originalProduct, "Desativar preserva foto, descrição, tributos, preços, estoque e todos os demais campos");
        check(JsonSerializer.Serialize(service.Snapshot.StockMovements, JsonStore.JsonOptions) == stockHistory,
            "Alterar status não cria nem remove movimentos de estoque");
        check(ProductData(new JsonStore(directory).Snapshot().Products.Single(p => p.Id == detailed.Id)) == inactiveData,
            "Produto desativado e cadastro completo permanecem após reabrir os dados");
        service.SetProductActive(detailed.Id, true);
        check(ProductData(service.Snapshot.Products.Single(p => p.Id == detailed.Id)) == originalProduct,
            "Reativar recupera o status sem modificar o cadastro");
        check(service.Snapshot.Audit.TakeLast(2).All(a => a.UserName == "ADMIN" && a.Action.Contains(detailed.Code))
            && service.Snapshot.Audit[^2].Action.StartsWith("Produto desativado:") && service.Snapshot.Audit[^1].Action.StartsWith("Produto ativado:"),
            "Ativação e desativação registram administrador e produto na auditoria");
        check(!StoreService.CanDeleteProduct(service.Snapshot, detailed.Id), "Produto com estoque não é elegível para exclusão");
        RejectAtomic<InvalidOperationException>(() => service.DeleteProduct(detailed.Id), "Excluir produto com estoque é recusado", true);
        var seededStock = service.Snapshot.Products.First(p => p.Code == "001");
        check(!StoreService.CanDeleteProduct(service.Snapshot, seededStock.Id), "Estoque positivo impede exclusão mesmo sem vendas, pedidos ou movimentos registrados");
        RejectAtomic<InvalidOperationException>(() => service.DeleteProduct(seededStock.Id), "Excluir produto com estoque e sem histórico é recusado", true);

        var unused = AddProduct("ACAO_LIVRE");
        check(StoreService.CanDeleteProduct(service.Snapshot, unused.Id), "Produto sem estoque e sem histórico pode ser excluído");
        var beforeDelete = service.Snapshot;
        service.DeleteProduct(unused.Id);
        var afterDelete = service.Snapshot;
        check(!afterDelete.Products.Any(p => p.Id == unused.Id) && afterDelete.Products.Count == beforeDelete.Products.Count - 1,
            "Administrador exclui somente o cadastro sem uso escolhido");
        check(JsonSerializer.Serialize(afterDelete.Products, JsonStore.JsonOptions) == JsonSerializer.Serialize(beforeDelete.Products.Where(p => p.Id != unused.Id), JsonStore.JsonOptions)
            && JsonSerializer.Serialize(afterDelete.Sales, JsonStore.JsonOptions) == JsonSerializer.Serialize(beforeDelete.Sales, JsonStore.JsonOptions)
            && JsonSerializer.Serialize(afterDelete.Orders, JsonStore.JsonOptions) == JsonSerializer.Serialize(beforeDelete.Orders, JsonStore.JsonOptions)
            && JsonSerializer.Serialize(afterDelete.StockMovements, JsonStore.JsonOptions) == JsonSerializer.Serialize(beforeDelete.StockMovements, JsonStore.JsonOptions),
            "Exclusão permitida preserva os demais produtos e todo o histórico da loja");
        check(afterDelete.Audit.Count == beforeDelete.Audit.Count + 1 && afterDelete.Audit[^1].UserName == "ADMIN"
            && afterDelete.Audit[^1].Action.Contains("Produto excluído") && afterDelete.Audit[^1].Action.Contains(unused.Code),
            "Exclusão permitida gera um registro de auditoria com produto e administrador");
        check(!new JsonStore(directory).Snapshot().Products.Any(p => p.Id == unused.Id), "Exclusão permanece após reabrir os dados");
        Guid missing = Guid.NewGuid();
        check(!StoreService.CanDeleteProduct(service.Snapshot, missing), "Identificador inexistente não é elegível para exclusão");
        RejectAtomic<InvalidOperationException>(() => service.DeleteProduct(missing), "Excluir produto inexistente é recusado");
        RejectAtomic<InvalidOperationException>(() => service.SetProductActive(missing, false), "Alterar status de produto inexistente é recusado");

        var customer = new Customer { Name = "Cliente de ações de produtos" }; service.SaveCustomer(customer);
        var cash = service.OpenCash(0);
        var retainedProducts = new List<Product>();
        // Legacy/imported history may contain a sale or order without matching movement records.
        // Keep each source isolated to verify that none can lose its referenced product.
        (string Code, string Label, Action<StoreState, Product> Attach)[] histories =
        [
            ("ACAO_VENDA", "venda concluída", (s, p) => s.Sales.Add(new Sale { Number = s.NextSaleNumber++, CashSessionId = cash.Id, UserId = admin.Id, UserName = admin.Username, PaymentMethod = PaymentMethod.Pix, Tendered = p.Price,
                Lines = [new() { ProductId = p.Id, ProductName = p.Name, Quantity = 1, UnitPrice = p.Price, UnitCost = p.Cost, NetRevenue = p.Price }] })),
            ("ACAO_ESTORNO", "venda cancelada", (s, p) => s.Sales.Add(new Sale { Number = s.NextSaleNumber++, CashSessionId = cash.Id, UserId = admin.Id, UserName = admin.Username, PaymentMethod = PaymentMethod.Pix, Tendered = p.Price, Canceled = true, CancelReason = "Histórico de teste",
                Lines = [new() { ProductId = p.Id, ProductName = p.Name, Quantity = 1, UnitPrice = p.Price, UnitCost = p.Cost, NetRevenue = p.Price }] })),
            ("ACAO_PED_CANCEL", "pedido cancelado", (s, p) => s.Orders.Add(new Order { Number = s.NextOrderNumber++, CustomerId = customer.Id, CustomerName = customer.Name, Status = OrderStatus.Cancelado,
                Lines = [new() { ProductId = p.Id, ProductName = p.Name, Quantity = 1, UnitPrice = p.Price, UnitCost = p.Cost, NetRevenue = p.Price }] })),
            ("ACAO_PED_FIM", "pedido concluído", (s, p) => s.Orders.Add(new Order { Number = s.NextOrderNumber++, CustomerId = customer.Id, CustomerName = customer.Name, Status = OrderStatus.Concluido,
                Lines = [new() { ProductId = p.Id, ProductName = p.Name, Quantity = 1, UnitPrice = p.Price, UnitCost = p.Cost, NetRevenue = p.Price }] })),
            ("ACAO_MOVIMENTO", "movimento de estoque", (s, p) => s.StockMovements.Add(new StockMovement { ProductId = p.Id, ProductName = p.Name, Quantity = 1, Reason = "Histórico de teste", UserName = admin.Username }))
        ];
        foreach (var history in histories)
        {
            var product = AddProduct(history.Code); retainedProducts.Add(product);
            service.Store.Update(s => { history.Attach(s, product); return 0; });
            string queryState = State(), queryFile = File.ReadAllText(service.Store.FilePath);
            check(!StoreService.CanDeleteProduct(service.Snapshot, product.Id), $"Produto com {history.Label}, estoque zero e sem reserva não pode ser excluído");
            check(State() == queryState && File.ReadAllText(service.Store.FilePath) == queryFile, $"Consultar exclusão com {history.Label} não modifica dados nem arquivo");
            RejectAtomic<InvalidOperationException>(() => service.DeleteProduct(product.Id), $"Excluir produto com {history.Label} é recusado", true);
            service.SetProductActive(product.Id, false);
            check(!service.Snapshot.Products.Single(p => p.Id == product.Id).Active && !StoreService.CanDeleteProduct(service.Snapshot, product.Id),
                $"Desativar produto com {history.Label} preserva cadastro e proteção do histórico");
        }

        var reserved = AddProduct("ACAO_RESERVA", 3);
        var order = service.CreateOrder([new(reserved.Id, 2)], customer.Id, false, "", "Reserva de teste", 0, 0);
        check(StoreService.Reserved(service.Snapshot, reserved.Id) == 2 && !StoreService.CanDeleteProduct(service.Snapshot, reserved.Id),
            "Pedido aberto reserva o produto e impede exclusão");
        RejectAtomic<InvalidOperationException>(() => service.SetProductActive(reserved.Id, false), "Desativar produto reservado é recusado");
        RejectAtomic<InvalidOperationException>(() => service.DeleteProduct(reserved.Id), "Excluir produto reservado é recusado", true);
        service.SetProductActive(reserved.Id, true);
        check(service.Snapshot.Products.Single(p => p.Id == reserved.Id).Active && StoreService.Reserved(service.Snapshot, reserved.Id) == 2,
            "Manter produto reservado ativo preserva a reserva");
        service.SetOrderStatus(order.Id, OrderStatus.Cancelado); service.AdjustStock(reserved.Id, -3, "Zerar após cancelamento do pedido");
        service.SetProductActive(reserved.Id, false);
        check(service.Snapshot.Products.Single(p => p.Id == reserved.Id).Stock == 0 && !service.Snapshot.Products.Single(p => p.Id == reserved.Id).Active
            && StoreService.Reserved(service.Snapshot, reserved.Id) == 0 && service.Snapshot.Orders.Any(o => o.Id == order.Id),
            "Após cancelar a reserva, desativação é permitida e mantém o histórico do pedido");
        RejectAtomic<InvalidOperationException>(() => service.DeleteProduct(reserved.Id), "Produto com pedido cancelado e estoque zerado continua protegido", true);

        var operatorTarget = AddProduct("ACAO_OPERADOR");
        service.SaveUser(new UserAccount { Name = "Operador de produtos", Username = "PRODUTOS_OPERADOR", Role = UserRole.Operador }, "1234");
        var restricted = new StoreService(service.Store); restricted.Login("PRODUTOS_OPERADOR", "1234");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetProductActive(detailed.Id, false), "Operador não desativa produto");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetProductActive(retainedProducts[0].Id, true), "Operador não reativa produto");
        check(StoreService.CanDeleteProduct(service.Snapshot, operatorTarget.Id), "Produto sem uso permanece elegível mesmo quando operador não tem permissão de excluir");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.DeleteProduct(operatorTarget.Id), "Operador não exclui produto sem estoque ou histórico");
    }
}
