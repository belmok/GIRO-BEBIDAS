using System.Text.Json;
using System.Text.Json.Nodes;
using GiroBebidas.Core;

namespace GiroBebidas.Tests;

public static class CustomerAccessChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "acessos-clientes", Guid.NewGuid().ToString("N"));
        var service = new StoreService(new JsonStore(directory));
        service.Login("ADMIN", "ADMIN");
        string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
        static string CustomerData(Customer customer) => JsonSerializer.Serialize(customer, JsonStore.JsonOptions);
        void RejectAtomic<T>(Action action, string name) where T : Exception
        {
            string beforeState = State(), beforeFile = File.ReadAllText(service.Store.FilePath);
            T? rejected = null;
            try { action(); } catch (T ex) { rejected = ex; }
            check(rejected is not null, name);
            check(State() == beforeState && File.ReadAllText(service.Store.FilePath) == beforeFile,
                name + ": estado completo e arquivo preservados");
        }
        Customer AddCustomer(string name, decimal limit = 125)
        {
            var customer = new Customer { Name = name, Phone = "81900001234", Address = "Rua dos testes, 42", Document = "Documento de teste", Notes = "Observações preservadas", CreditLimit = limit };
            service.SaveCustomer(customer); return customer;
        }
        void Protected(Guid id, string history)
        {
            string beforeState = State(), beforeFile = File.ReadAllText(service.Store.FilePath);
            check(!StoreService.CanDeleteCustomer(service.Snapshot, id), $"Cliente com {history} não é elegível para exclusão");
            check(State() == beforeState && File.ReadAllText(service.Store.FilePath) == beforeFile,
                $"Consulta de exclusão com {history} preserva estado e arquivo");
            RejectAtomic<InvalidOperationException>(() => service.DeleteCustomer(id), $"Excluir cliente com {history} é recusado");
        }

        var product = new Product { Code = "CLIENTES_TESTE", Name = "Produto de teste de fiados", Category = "Outros", Price = 12.50m, Cost = 6, Stock = 100 };
        service.SaveProduct(product);
        var customer = AddCustomer("Cliente com fiado");
        check(!customer.CreditBlocked && !service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Cliente novo inicia com fiado liberado");
        var cash = service.OpenCash(100);
        var originalDebt = service.CompleteSale(new([new(product.Id, 2)], 0, 0, customer.Id, PaymentMethod.Fiado, 25));
        check(StoreService.Balance(service.Snapshot, customer.Id) == 25, "Fiado anterior ao bloqueio gera dívida de 25 reais");
        string originalCustomer = CustomerData(service.Snapshot.Customers.Single(c => c.Id == customer.Id));
        var beforeBlock = service.Snapshot;
        service.SetCustomerCreditBlocked(customer.Id, true);
        var blocked = service.Snapshot.Customers.Single(c => c.Id == customer.Id);
        check(blocked.CreditBlocked, "Administrador bloqueia novas vendas em fiado");
        blocked.CreditBlocked = false;
        check(CustomerData(blocked) == originalCustomer, "Bloqueio preserva nome, telefone, documento, endereço, observações e limite de crédito");
        check(StoreService.Balance(service.Snapshot, customer.Id) == 25
            && StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected == 100
            && service.Snapshot.Sales.Count == beforeBlock.Sales.Count
            && service.Snapshot.StockMovements.Count == beforeBlock.StockMovements.Count,
            "Bloquear fiado preserva dívida, saldo do caixa, vendas e movimentos de estoque");
        check(service.Snapshot.Audit.Count == beforeBlock.Audit.Count + 1
            && service.Snapshot.Audit[^1].UserName == "ADMIN"
            && service.Snapshot.Audit[^1].Action.Contains(customer.Name),
            "Bloqueio registra cliente e administrador na auditoria");
        check(new JsonStore(directory).Snapshot().Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Bloqueio permanece depois de reabrir os dados");
        check(service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Alterar cópia do cadastro não libera fiado fora de uma operação autorizada");
        long nextSale = service.Snapshot.NextSaleNumber;
        decimal stock = service.Snapshot.Products.Single(p => p.Id == product.Id).Stock;
        RejectAtomic<InvalidOperationException>(() => service.CompleteSale(new([new(product.Id, 1)], 0, 0, customer.Id, PaymentMethod.Fiado, 12.50m)),
            "Venda em fiado de cliente bloqueado é recusada");
        check(service.Snapshot.NextSaleNumber == nextSale && service.Snapshot.Products.Single(p => p.Id == product.Id).Stock == stock
            && StoreService.Balance(service.Snapshot, customer.Id) == 25,
            "Recusa de fiado bloqueado não consome numeração nem estoque e não aumenta dívida");

        var reservedOrder = service.CreateOrder([new(product.Id, 3)], customer.Id, true, customer.Address, "Pedido durante bloqueio", 0, 0);
        check(StoreService.Reserved(service.Snapshot, product.Id) == 3, "Cliente bloqueado ainda pode salvar pedido e reservar seus produtos");
        RejectAtomic<InvalidOperationException>(() => service.CompleteSale(new([], 0, 0, null, PaymentMethod.Fiado, reservedOrder.Total, OrderId: reservedOrder.Id)),
            "Pagamento em fiado do pedido também respeita o bloqueio do cliente");
        check(service.Snapshot.Orders.Single(o => o.Id == reservedOrder.Id).Status == OrderStatus.Pendente
            && StoreService.Reserved(service.Snapshot, product.Id) == 3,
            "Fiado recusado preserva o pedido pendente e sua reserva");
        var cashSale = service.CompleteSale(new([new(product.Id, 1)], 0, 0, customer.Id, PaymentMethod.Dinheiro, 20));
        check(cashSale.Total == 12.50m && cashSale.Change == 7.50m && cashSale.CustomerId == customer.Id,
            "Cliente bloqueado compra em dinheiro com total, troco e identificação corretos");
        var pixSale = service.CompleteSale(new([], 0, 0, null, PaymentMethod.Pix, reservedOrder.Total, OrderId: reservedOrder.Id));
        check(pixSale.Total == 37.50m && pixSale.CustomerId == customer.Id
            && service.Snapshot.Orders.Single(o => o.Id == reservedOrder.Id).Status == OrderStatus.Concluido
            && StoreService.Reserved(service.Snapshot, product.Id) == 0,
            "Pedido de cliente bloqueado pode ser recebido por Pix e libera a reserva uma única vez");
        check(StoreService.Balance(service.Snapshot, customer.Id) == 25
            && service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditLimit == 125,
            "Compras pagas durante bloqueio preservam a dívida anterior e o limite cadastrado");
        service.ReceiveDebt(customer.Id, 10, PaymentMethod.Pix);
        check(StoreService.Balance(service.Snapshot, customer.Id) == 15
            && StoreService.Outstanding(service.Snapshot, originalDebt) == 15
            && service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Cliente bloqueado pode pagar parcialmente a dívida sem liberar novas vendas em fiado");
        check(StoreService.SummarizeCash(service.Snapshot, cash.Id).Pix == 47.50m
            && StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected == 112.50m,
            "Recebimento Pix de cliente bloqueado reconcilia Pix e dinheiro separadamente");
        RejectAtomic<InvalidOperationException>(() => service.ReceiveDebt(customer.Id, 15.01m, PaymentMethod.Dinheiro),
            "Bloqueio não permite recebimento acima do saldo devedor");

        var beforeRelease = service.Snapshot;
        service.SetCustomerCreditBlocked(customer.Id, false);
        check(!service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked
            && CustomerData(service.Snapshot.Customers.Single(c => c.Id == customer.Id)) == originalCustomer,
            "Administrador libera fiado sem alterar os dados e o limite do cadastro");
        check(StoreService.Balance(service.Snapshot, customer.Id) == 15
            && service.Snapshot.Audit.Count == beforeRelease.Audit.Count + 1
            && service.Snapshot.Audit[^1].UserName == "ADMIN"
            && service.Snapshot.Audit[^1].Action.Contains(customer.Name),
            "Liberação preserva dívida e registra cliente e administrador na auditoria");
        check(!new JsonStore(directory).Snapshot().Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Liberação de fiado permanece depois de reabrir os dados");
        var releasedSale = service.CompleteSale(new([new(product.Id, 1)], 0, 0, customer.Id, PaymentMethod.Fiado, 12.50m));
        check(StoreService.Balance(service.Snapshot, customer.Id) == 27.50m && releasedSale.PaymentMethod == PaymentMethod.Fiado,
            "Após liberar, nova venda em fiado utiliza o limite original e soma a dívida corretamente");
        service.ReceiveDebt(customer.Id, 27.50m, PaymentMethod.Dinheiro);
        check(StoreService.Balance(service.Snapshot, customer.Id) == 0 && service.Snapshot.ReceivablePayments.Count == 3,
            "Recebimento integral quita as vendas sem apagar suas parcelas e recibos");
        Protected(customer.Id, "vendas de fiado totalmente quitadas e recebimentos");

        var canceledCustomer = AddCustomer("Cliente de venda cancelada");
        var canceledSale = service.CompleteSale(new([new(product.Id, 1)], 0, 0, canceledCustomer.Id, PaymentMethod.Pix, 12.50m));
        service.CancelSale(canceledSale.Id, "Teste de preservação de histórico");
        check(service.Snapshot.Sales.Single(s => s.Id == canceledSale.Id).Canceled
            && StoreService.Balance(service.Snapshot, canceledCustomer.Id) == 0,
            "Cancelamento deixa saldo zero e mantém o cliente associado à venda");
        Protected(canceledCustomer.Id, "venda cancelada e saldo zero");
        var orderCustomer = AddCustomer("Cliente somente com pedido");
        var orderOnly = service.CreateOrder([new(product.Id, 1)], orderCustomer.Id, false, "", "Histórico sem venda", 0, 0);
        Protected(orderCustomer.Id, "pedido pendente sem vendas");
        service.SetOrderStatus(orderOnly.Id, OrderStatus.Cancelado);
        Protected(orderCustomer.Id, "pedido cancelado sem vendas nem saldo devedor");
        var importedCustomer = AddCustomer("Cliente de pedido concluído importado");
        var importedOrder = service.CreateOrder([new(product.Id, 1)], importedCustomer.Id, false, "", "Pedido de histórico antigo", 0, 0);
        // An imported backup may retain a concluded order without its corresponding sale.
        // That order is independently sufficient to protect the customer it references.
        service.Store.Update(s => { s.Orders.Single(o => o.Id == importedOrder.Id).Status = OrderStatus.Concluido; return 0; });
        check(!service.Snapshot.Sales.Any(s => s.CustomerId == importedCustomer.Id), "Fixture de pedido concluído isola histórico de pedidos sem vínculo de venda");
        Protected(importedCustomer.Id, "pedido concluído importado sem venda associada");

        var unused = AddCustomer("Cadastro sem uso para excluir");
        service.SetCustomerCreditBlocked(unused.Id, true);
        check(StoreService.CanDeleteCustomer(service.Snapshot, unused.Id), "Cliente bloqueado sem vendas, pedidos ou recebimentos pode ser excluído");
        var beforeDelete = service.Snapshot;
        service.DeleteCustomer(unused.Id);
        var afterDelete = service.Snapshot;
        check(!afterDelete.Customers.Any(c => c.Id == unused.Id) && afterDelete.Customers.Count == beforeDelete.Customers.Count - 1,
            "Administrador exclui somente o cadastro sem uso escolhido");
        check(afterDelete.Audit.Count == beforeDelete.Audit.Count + 1 && afterDelete.Audit[^1].UserName == "ADMIN"
            && afterDelete.Audit[^1].Action.Contains(unused.Name),
            "Exclusão registra administrador e cliente na auditoria");
        beforeDelete.Customers.RemoveAll(c => c.Id == unused.Id);
        afterDelete.Audit.RemoveAt(afterDelete.Audit.Count - 1);
        check(JsonSerializer.Serialize(beforeDelete, JsonStore.JsonOptions) == JsonSerializer.Serialize(afterDelete, JsonStore.JsonOptions),
            "Exclusão permitida preserva todos os demais clientes, vendas, pedidos, valores e histórico da loja");
        check(!new JsonStore(directory).Snapshot().Customers.Any(c => c.Id == unused.Id), "Exclusão do cadastro permanece após reabrir os dados");
        var missing = Guid.NewGuid();
        check(!StoreService.CanDeleteCustomer(service.Snapshot, missing), "Cliente inexistente não é elegível para exclusão");
        RejectAtomic<InvalidOperationException>(() => service.DeleteCustomer(missing), "Excluir cliente inexistente é recusado");
        RejectAtomic<InvalidOperationException>(() => service.SetCustomerCreditBlocked(missing, true), "Bloquear cliente inexistente é recusado");

        var operatorTarget = AddCustomer("Cadastro protegido de ações do operador");
        service.CompleteSale(new([new(product.Id, 1)], 0, 0, customer.Id, PaymentMethod.Fiado, 12.50m));
        service.SetCustomerCreditBlocked(customer.Id, true);
        service.SaveUser(new UserAccount { Name = "Operador de clientes", Username = "CLIENTES_OPERADOR", Role = UserRole.Operador }, "1234");
        var restricted = new StoreService(service.Store); restricted.Login("CLIENTES_OPERADOR", "1234");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetCustomerCreditBlocked(operatorTarget.Id, true), "Operador não bloqueia fiado");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetCustomerCreditBlocked(customer.Id, false), "Operador não libera fiado bloqueado");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.DeleteCustomer(operatorTarget.Id), "Operador não exclui cliente sem histórico");
        var edited = service.Snapshot.Customers.Single(c => c.Id == customer.Id);
        edited.Name = "Cliente com telefone atualizado"; edited.Phone = "81911112222"; edited.CreditBlocked = false;
        restricted.SaveCustomer(edited);
        check(service.Snapshot.Customers.Single(c => c.Id == customer.Id).Name == edited.Name
            && service.Snapshot.Customers.Single(c => c.Id == customer.Id).Phone == edited.Phone
            && service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Operador atualiza nome e telefone sem conseguir liberar fiado pelo salvamento do cadastro");
        var operatorCreated = new Customer { Name = "Cliente criado pelo operador", CreditLimit = 500, CreditBlocked = true };
        restricted.SaveCustomer(operatorCreated);
        check(service.Snapshot.Customers.Single(c => c.Id == operatorCreated.Id).CreditLimit == 500
            && !service.Snapshot.Customers.Single(c => c.Id == operatorCreated.Id).CreditBlocked,
            "Cadastro criado por operador inicia liberado e não altera bloqueio por campo enviado");
        RejectAtomic<InvalidOperationException>(() => restricted.CompleteSale(new([new(product.Id, 1)], 0, 0, customer.Id, PaymentMethod.Fiado, 12.50m)),
            "Venda feita por operador também respeita o bloqueio de fiado");
        restricted.ReceiveDebt(customer.Id, 5, PaymentMethod.Dinheiro);
        check(StoreService.Balance(service.Snapshot, customer.Id) == 7.50m
            && service.Snapshot.ReceivablePayments[^1].UserName == "CLIENTES_OPERADOR"
            && service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Operador recebe dívida de cliente bloqueado e registra o recebimento sem liberar o fiado");
        RejectAtomic<InvalidOperationException>(() => restricted.ReceiveDebt(customer.Id, 0, PaymentMethod.Pix), "Recebimento zero de cliente bloqueado é recusado");
        RejectAtomic<InvalidOperationException>(() => restricted.ReceiveDebt(customer.Id, 1, PaymentMethod.Fiado), "Receber dívida em fiado continua sendo recusado");
        var limitChanged = service.Snapshot.Customers.Single(c => c.Id == customer.Id); limitChanged.CreditLimit = 7.49m;
        RejectAtomic<InvalidOperationException>(() => restricted.SaveCustomer(limitChanged), "Bloqueio não permite reduzir o limite abaixo da dívida atual");
        restricted.Logout();
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetCustomerCreditBlocked(customer.Id, false), "Sessão encerrada não libera fiado");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.DeleteCustomer(operatorTarget.Id), "Sessão encerrada não exclui cliente");

        service.CloseCash(StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Conferência de clientes e fiados");
        string backup = Path.Combine(directory, "backup-com-bloqueio.json"); service.ExportBackup(backup);
        service.SetCustomerCreditBlocked(customer.Id, false);
        service.RestoreBackup(backup);
        check(service.CurrentUserId is null && service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditBlocked
            && StoreService.Balance(service.Snapshot, customer.Id) == 7.50m,
            "Restauração recupera bloqueio e dívida e exige novo acesso ao programa");
        check(new JsonStore(directory).Snapshot().Customers.Single(c => c.Id == customer.Id).CreditBlocked,
            "Bloqueio restaurado permanece depois de reabrir os dados");
        var legacy = JsonNode.Parse(File.ReadAllText(backup))!.AsObject();
        foreach (var entry in legacy["Customers"]!.AsArray()) entry!.AsObject().Remove("CreditBlocked");
        string legacyBackup = Path.Combine(directory, "backup-anterior-sem-bloqueio.json"); File.WriteAllText(legacyBackup, legacy.ToJsonString());
        service.Login("ADMIN", "ADMIN"); service.RestoreBackup(legacyBackup);
        check(service.Snapshot.Customers.All(c => !c.CreditBlocked)
            && StoreService.Balance(service.Snapshot, customer.Id) == 7.50m
            && service.Snapshot.Customers.Single(c => c.Id == customer.Id).CreditLimit == 125,
            "Backup antigo sem campo de bloqueio inicia liberado e preserva dívida e limite");
        check(new JsonStore(directory).Snapshot().Customers.All(c => !c.CreditBlocked), "Compatibilidade do cadastro antigo permanece após reabrir os dados");
    }
}
