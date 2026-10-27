using System.Threading.Tasks;
using Canteen.Model.Entities;

namespace Canteen.DAL.Interfaces;

public interface IPaymentRepository
{
    Task<Payment> AddAsync(Payment payment);
    Task<Payment?> GetByRazorpayOrderIdAsync(string razorpayOrderId);
    Task<Payment?> GetByOrderIdAsync(int canteenId, int orderId);
    Task<Payment> UpdateAsync(Payment payment);
    Task UpdatePaymentAndOrderAsync(Payment payment, Order order);
    Task<bool> WebhookEventExistsAsync(string eventId);
    Task AddWebhookEventAsync(PaymentWebhookEvent webhookEvent);
}
