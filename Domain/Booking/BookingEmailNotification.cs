using Domain.User;

namespace Domain.Booking;

public enum BookingEmailNotificationType
{
  Completed,
  Cancelled,
  Terminated,
  Refunded
}

public record BookingEmailNotificationRequest
{
  public required UserPrincipal User { get; init; }
  public required BookingPrincipal Booking { get; init; }
  public required BookingEmailNotificationType Type { get; init; }
}
