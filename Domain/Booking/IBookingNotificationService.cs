using CSharp_Result;

namespace Domain.Booking;

public interface IBookingEmailNotifier
{
  Task<Result<Unit>> SendNotification(BookingEmailNotificationRequest request);
}

public interface IBookingNotificationService
{
  Task<Result<Unit>> NotifyBookingCompleted(Booking booking);
  Task<Result<Unit>> NotifyBookingCancelled(Booking booking);
  Task<Result<Unit>> NotifyBookingTerminated(Booking booking);
  Task<Result<Unit>> NotifyBookingRefunded(Booking booking);
}