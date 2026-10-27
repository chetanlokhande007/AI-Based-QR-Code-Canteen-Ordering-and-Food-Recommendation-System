using System.Threading.Tasks;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Canteen.DAL.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly CanteenDbContext _context;

    public PaymentRepository(CanteenDbContext context)
    {
        _context = context;
    }

    public async Task<Payment> AddAsync(Payment payment)
    {
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    public async Task<Payment?> GetByRazorpayOrderIdAsync(string razorpayOrderId)
    {
        return await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.RazorpayOrderId == razorpayOrderId);
    }

    public async Task<Payment?> GetByOrderIdAsync(int canteenId, int orderId)
    {
        return await _context.Payments
            .Include(p => p.Order)
            .Where(p => p.CanteenId == canteenId && p.OrderId == orderId)
            .OrderByDescending(p => p.Status == "Paid" ? 1 : 0)
            .ThenByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync();
    }

    public async Task<Payment> UpdateAsync(Payment payment)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    public async Task UpdatePaymentAndOrderAsync(Payment payment, Order order)
    {
        if (_context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory" && _context.Database.IsRelational())
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Payments.Update(payment);
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            _context.Payments.Update(payment);
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> WebhookEventExistsAsync(string eventId)
    {
        return await _context.PaymentWebhookEvents
            .AnyAsync(e => e.EventId == eventId);
    }

    public async Task AddWebhookEventAsync(PaymentWebhookEvent webhookEvent)
    {
        _context.PaymentWebhookEvents.Add(webhookEvent);
        await _context.SaveChangesAsync();
    }
}
