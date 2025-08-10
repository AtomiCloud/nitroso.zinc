using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Booking;

public class BookingNotificationService(
  IBookingEmailNotifier emailNotifier,
  ILogger<BookingNotificationService> logger
) : IBookingNotificationService
{
  public async Task<Result<Unit>> NotifyBookingCompleted(Booking booking)
  {
    logger.LogInformation("Sending completion email for booking {BookingId} - user {UserId}", booking.Principal.Id, booking.User.Id);
    if (string.IsNullOrWhiteSpace(booking.User.Record.Email))
    {
      logger.LogWarning("Cannot send completion email for booking {BookingId} - user {UserId} has no email address", booking.Principal.Id, booking.User.Id);
      return new Unit();
    }

    var message = new BookingEmailNotificationRequest
    {
      User = booking.User,
      Booking = booking.Principal,
      Type = BookingEmailNotificationType.Completed
    };

    return await emailNotifier.SendNotification(message);
  }

  public async Task<Result<Unit>> NotifyBookingCancelled(Booking booking)
  {
    logger.LogInformation("Sending cancellation email for booking {BookingId} - user {UserId}", booking.Principal.Id, booking.User.Id);
    if (string.IsNullOrWhiteSpace(booking.User.Record.Email))
    {
      logger.LogWarning("Cannot send cancellation email for booking {BookingId} - user {UserId} has no email address", 
        booking.Principal.Id, booking.User.Id);
      return new Unit();
    }

    var message = new BookingEmailNotificationRequest
    {
      User = booking.User,
      Booking = booking.Principal,
      Type = BookingEmailNotificationType.Cancelled
    };

    return await emailNotifier.SendNotification(message);
  }

  public async Task<Result<Unit>> NotifyBookingTerminated(Booking booking)
  {
    logger.LogInformation("Sending termination email for booking {BookingId} - user {UserId}", booking.Principal.Id, booking.User.Id);
    if (string.IsNullOrWhiteSpace(booking.User.Record.Email))
    {
      logger.LogWarning("Cannot send termination email for booking {BookingId} - user {UserId} has no email address", 
        booking.Principal.Id, booking.User.Id);
      return new Unit();
    }
    var message = new BookingEmailNotificationRequest
    {
      User = booking.User,
      Booking = booking.Principal,
      Type = BookingEmailNotificationType.Terminated
    };

    return await emailNotifier.SendNotification(message);
  }

  public async Task<Result<Unit>> NotifyBookingRefunded(Booking booking)
  {
    logger.LogInformation("Sending refund email for booking {BookingId} - user {UserId}", booking.Principal.Id, booking.User.Id);
    if (string.IsNullOrWhiteSpace(booking.User.Record.Email))
    {
      logger.LogWarning("Cannot send refund email for booking {BookingId} - user {UserId} has no email address", 
        booking.Principal.Id, booking.User.Id);
      return new Unit();
    }
    
    var message = new BookingEmailNotificationRequest
    {
      User = booking.User,
      Booking = booking.Principal,
      Type = BookingEmailNotificationType.Refunded
    };

    return await emailNotifier.SendNotification(message);
  }
}
