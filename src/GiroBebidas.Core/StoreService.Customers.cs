namespace GiroBebidas.Core;

public sealed partial class StoreService
{
    public void SetCustomerCreditBlocked(Guid id, bool blocked)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var customer = s.Customers.FirstOrDefault(c => c.Id == id)
                ?? throw new InvalidOperationException("Cliente não encontrado.");
            customer.CreditBlocked = blocked;
            Log(s, actor, $"Fiado {(blocked ? "bloqueado" : "liberado")}: {customer.Name}");
            return 0;
        });
    }

    public static bool CanDeleteCustomer(StoreState state, Guid id)
        => state.Customers.Any(c => c.Id == id)
            && !state.Sales.Any(s => s.CustomerId == id)
            && !state.Orders.Any(o => o.CustomerId == id);

    public void DeleteCustomer(Guid id)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            var customer = s.Customers.FirstOrDefault(c => c.Id == id)
                ?? throw new InvalidOperationException("Cliente não encontrado.");
            if (!CanDeleteCustomer(s, id))
                throw new InvalidOperationException("Só é possível excluir clientes sem vendas ou pedidos. O histórico e os recebimentos precisam ser preservados.");
            s.Customers.Remove(customer);
            Log(s, actor, $"Cliente excluído: {customer.Name}");
            return 0;
        });
    }
}
