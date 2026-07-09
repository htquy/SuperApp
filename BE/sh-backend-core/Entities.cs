using System;

namespace sh_backend_core
{
    public class Employee
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty; // JSON representation of the event
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }

    public record EmployeeCreatedEvent(Guid Id, string Name, string Email, string Department);
}
