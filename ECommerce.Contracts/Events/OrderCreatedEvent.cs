// ============================================================
// ECommerce.Contracts/Events/OrderCreatedEvent.cs
//
// EL ÚNICO CÓDIGO QUE DOS CONTEXTOS COMPARTEN: el contrato del evento.
//
// Pedidos lo PUBLICA (no espera respuesta) y Notificaciones lo CONSUME.
// MassTransit identifica un mensaje por su nombre completo, espacio de
// nombres incluido: publicador y consumidor deben usar exactamente esta
// clase. Por eso vive en un proyecto aparte y no se copia a mano.
//
// Lo que NO se comparte: las tablas. Notificaciones no puede leer OrderDB.
// Todo lo que necesita saber de un pedido viaja dentro del evento.
// ============================================================

namespace ECommerce.Contracts.Events
{
    public class OrderCreatedEvent
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<OrderCreatedItem> Items { get; set; } = new();
    }

    public class OrderCreatedItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
