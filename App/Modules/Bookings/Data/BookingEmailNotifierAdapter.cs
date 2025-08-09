using App.StartUp.Email;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.StartUp.Smtp;
using App.Utility;
using CSharp_Result;
using Domain.Booking;
using Domain.Timings;
using Microsoft.Extensions.Options;

namespace App.Modules.Bookings.Data;

public class BookingEmailNotifierAdapter(
  ISmtpClientFactory smtpClientFactory,
  IOptionsMonitor<DomainOptions> options,
  IEmailRenderer emailRenderer,
  ILogger<BookingEmailNotifierAdapter> logger
) : IBookingEmailNotifier
{
  public async Task<Result<(string, string)>> Rendering(BookingEmailNotificationRequest request)
  {
    logger.LogDebug("Rendering email to send {Request}", request);
    var o = options.CurrentValue;
    var email = request.Type switch
    {
      BookingEmailNotificationType.Cancelled => emailRenderer.RenderEmail("booking-cancelled", new
      {
        baseUrl = o.BaseUrl,
        whatsappUrl = o.WhatsAppUrl,
        telegramUrl = o.TelegramUrl,
        supportEmail = o.SupportEmail,
        userName = request.User.Record.Username.CapitalizeUsername(),
        userEmail = request.User.Record.Email,
        bookingId = request.Booking.Id,
        direction = request.Booking.Record.Direction == TrainDirection.JToW ? "Johor Bahru → Singapore" : "Singapore → Johor Bahru",
        bookingDate = request.Booking.Record.Date.ToString("ddd, MMM dd yyyy"),
        bookingTime = request.Booking.Record.Time.ToString("HH:mm"),
        cancellationDate = request.Booking.Status.CompletedAt?.ToString("ddd, MMM dd yyyy") ?? "Not Applicable", 
      }),
      BookingEmailNotificationType.Completed => emailRenderer.RenderEmail("booking-completed", new { 
        baseUrl = o.BaseUrl,
        whatsappUrl = o.WhatsAppUrl,
        telegramUrl = o.TelegramUrl,
        supportEmail = o.SupportEmail,
        userName = request.User.Record.Username.CapitalizeUsername(),
        userEmail = request.User.Record.Email,
        bookingId = request.Booking.Id,
        direction = request.Booking.Record.Direction == TrainDirection.JToW ? "Johor Bahru → Singapore" : "Singapore → Johor Bahru",
        bookingDate = request.Booking.Record.Date.ToString("ddd, MMM dd yyyy"),
        bookingTime = request.Booking.Record.Time.ToString("HH:mm"),
        ticketNumber = request.Booking.Complete.TicketNumber,
        bookingNumber = request.Booking.Complete.BookingNumber,
      }),
      BookingEmailNotificationType.Refunded => emailRenderer.RenderEmail("booking-refunded", new
      {
        baseUrl = o.BaseUrl,
        userName = request.User.Record.Username.CapitalizeUsername(),
        userEmail = request.User.Record.Email,
        whatsappUrl = o.WhatsAppUrl,
        telegramUrl = o.TelegramUrl,
        supportEmail = o.SupportEmail,
        bookingId = request.Booking.Id,
        direction = request.Booking.Record.Direction == TrainDirection.JToW ? "Johor Bahru → Singapore" : "Singapore → Johor Bahru",
        bookingDate = request.Booking.Record.Date.ToString("ddd, MMM dd yyyy"),
        bookingTime = request.Booking.Record.Time.ToString("HH:mm"),
        refundDate = request.Booking.Status.CompletedAt?.ToString("ddd, MMM dd yyyy") ?? "Not Applicable", 
      }),
      BookingEmailNotificationType.Terminated => emailRenderer.RenderEmail("booking-terminated", new
      {
        baseUrl = o.BaseUrl,
        userName = request.User.Record.Username.CapitalizeUsername(),
        userEmail = request.User.Record.Email,
        whatsappUrl = o.WhatsAppUrl,
        telegramUrl = o.TelegramUrl,
        supportEmail = o.SupportEmail,
        bookingId = request.Booking.Id,
        direction = request.Booking.Record.Direction == TrainDirection.JToW ? "Johor Bahru → Singapore" : "Singapore → Johor Bahru",
        bookingDate = request.Booking.Record.Date.ToString("ddd, MMM dd yyyy"),
        bookingTime = request.Booking.Record.Time.ToString("HH:mm"),
        terminationDate = request.Booking.Status.CompletedAt?.ToString("ddd, MMM dd yyyy") ?? "Not Applicable",
      }),
      _ => throw new ArgumentOutOfRangeException(nameof(request.Type), request.Type, null)
    };

    
    
    return await email.Then(x =>
    {
      var subject = (request.Type) switch
      {
        BookingEmailNotificationType.Cancelled =>  "BunnyBooker - Cancelled Booking",
        BookingEmailNotificationType.Completed => "BunnyBooker - Confirmation",
        BookingEmailNotificationType.Refunded => "BunnyBooker - Refund",
        BookingEmailNotificationType.Terminated => "BunnyBooker - Terminated",
        _ => throw new ArgumentOutOfRangeException(nameof(request.Type), request.Type, null)
      };
      return (subject, x);
    }, Errors.MapNone);
  }

  public async Task<Result<Unit>> SendNotification(BookingEmailNotificationRequest request)
  {
    logger.LogDebug("SMTP Sender trigger, sending SMTP email with {Request}", request);
    if (string.IsNullOrWhiteSpace(request.User.Record.Email))
    {
      logger.LogWarning("Cannot send {NotificationType} email - user {UserId} has no email address",
        request.Type, request.User.Id);
      return new Unit();
    }

    try
    {
      var smtpClient = smtpClientFactory.Get(SmtpProviders.Transactional);

      // Generate email content using template service
      return await this.Rendering(request)
        .ThenAwait(async r =>
        {
          var (subject, html) = r;
          var smtpMessage = new SmtpEmailMessage
          {
            To = request.User.Record.Email, 
            Subject = subject, 
            Body = html, 
            IsHtml = true
          };
          logger.LogInformation("Sending {NotificationType} email notification to user {UserId} ({Email})",
            request.Type, request.User.Id, request.User.Record.Email);
          return await smtpClient.SendAsync(smtpMessage);
        }, Errors.MapNone)
        .Then(x => new Unit(), Errors.MapNone);
     
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to send {NotificationType} email notification to user {UserId}",
        request.Type, request.User.Id);
      return ex;
    }
  }
}
