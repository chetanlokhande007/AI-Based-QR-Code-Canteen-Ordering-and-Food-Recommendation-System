using System;

namespace Canteen.Model.Entities;

public class PaymentWebhookEvent
{
    public int Id { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public string? Payload { get; set; }
}
