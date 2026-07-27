using App.Modules.Bookings;
using App.Modules.Bookings.API.V1;
using App.Modules.Bookings.Data;
using App.Modules.Costs.Data;
using App.Modules.Discounts.Data;
using App.Modules.Milestones.Data;
using App.Modules.Passengers.Data;
using App.Modules.Payments;
using App.Modules.Payments.Airwallex;
using App.Modules.Payments.Data;
using App.Modules.Schedules.Data;
using App.Modules.System;
using App.Modules.Timings.Data;
using App.Modules.Transactions.Data;
using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using App.Modules.Withdrawals;
using App.Modules.Withdrawals.API.V1;
using App.Modules.Withdrawals.Data;
using App.StartUp.Services;
using Domain;
using Domain.Admin;
using Domain.Booking;
using Domain.Cost;
using Domain.Discount;
using Domain.Milestone;
using Domain.Passenger;
using Domain.Payment;
using Domain.Schedule;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;

namespace App.Modules;

public static class DomainServices
{
  public static IServiceCollection AddDomainServices(this IServiceCollection s)
  {
    // USER
    s.AddScoped<IUserService, UserService>().AutoTrace<IUserService>();

    s.AddScoped<IUserWipeService, UserWipeService>().AutoTrace<IUserWipeService>();

    s.AddScoped<IUserRepository, UserRepository>().AutoTrace<IUserRepository>();

    s.AddScoped<IPartnerEconomicsRepository, PartnerEconomicsRepository>()
      .AutoTrace<IPartnerEconomicsRepository>();

    // Passenger
    s.AddScoped<IPassengerService, PassengerService>().AutoTrace<IPassengerService>();

    s.AddScoped<IPassengerRepository, PassengerRepository>().AutoTrace<IPassengerRepository>();

    // Schedules
    s.AddScoped<IScheduleService, ScheduleService>().AutoTrace<IScheduleService>();

    s.AddScoped<IScheduleRepository, ScheduleRepository>().AutoTrace<IScheduleRepository>();

    // Timings
    s.AddScoped<ITimingService, TimingService>().AutoTrace<ITimingService>();

    s.AddScoped<ITimingRepository, TimingRepository>().AutoTrace<ITimingRepository>();

    // Bookings
    s.AddScoped<IBookingService, BookingService>().AutoTrace<IBookingService>();

    s.AddScoped<IBookingRepository, BookingRepository>().AutoTrace<IBookingRepository>();

    s.AddScoped<IBookingCdcRepository, BookingCdcRepository>().AutoTrace<IBookingCdcRepository>();

    s.AddScoped<IBookingStorage, BookingStorage>().AutoTrace<IBookingStorage>();

    s.AddScoped<IBookingImageEnricher, BookingImageEnricher>().AutoTrace<IBookingImageEnricher>();

    s.AddScoped<IBookingTerminatorRepository, BookingTerminatorRepository>()
      .AutoTrace<IBookingTerminatorRepository>();

    // Sales/revenue analysis (admin Analysis page)
    s.AddScoped<IBookingAnalysisRepository, BookingAnalysisRepository>()
      .AutoTrace<IBookingAnalysisRepository>();

    // KTMB ticket cost queue (effective-dated per direction, analysis costing)
    s.AddScoped<IKtmbCostRepository, KtmbCostRepository>().AutoTrace<IKtmbCostRepository>();

    // KTMB MYR -> SGD FX rate queue (actual-cost conversion in the analysis)
    s.AddScoped<IKtmbFxRateRepository, KtmbFxRateRepository>()
      .AutoTrace<IKtmbFxRateRepository>();

    // Priority queue
    s.AddScoped<IPrioritySettingsRepository, PrioritySettingsRepository>()
      .AutoTrace<IPrioritySettingsRepository>();

    s.AddScoped<IPriorityAccessRepository, PriorityAccessRepository>()
      .AutoTrace<IPriorityAccessRepository>();

    // Milestones (snatching-algo change markers for the stats page)
    s.AddScoped<IMilestoneRepository, MilestoneRepository>().AutoTrace<IMilestoneRepository>();

    // Transaction
    s.AddScoped<ITransactionService, TransactionService>().AutoTrace<ITimingService>();

    s.AddScoped<ITransactionRepository, TransactionRepository>()
      .AutoTrace<ITransactionRepository>();
    s.AddScoped<ITransactionGenerator, TransactionGenerator>().AutoTrace<ITransactionGenerator>();

    // Wallet
    s.AddScoped<IWalletService, WalletService>().AutoTrace<IWalletService>();

    s.AddScoped<IWalletRepository, WalletRepository>().AutoTrace<IWalletRepository>();

    // Transaction Manager
    s.AddScoped<ITransactionManager, TransactionManager>().AutoTrace<ITransactionManager>();

    // Admin
    s.AddScoped<IAdminService, AdminService>().AutoTrace<IAdminService>();

    // Withdrawal
    s.AddScoped<IWithdrawalService, WithdrawalService>().AutoTrace<IWithdrawalService>();

    s.AddScoped<WithdrawalRepository>();
    s.AddScoped<IWithdrawalRepository>(sp => sp.GetRequiredService<WithdrawalRepository>())
      .AutoTrace<IWithdrawalRepository>();

    s.AddScoped<IWithdrawalExportRepository>(sp => sp.GetRequiredService<WithdrawalRepository>())
      .AutoTrace<IWithdrawalExportRepository>();

    s.AddScoped<IWithdrawalStorage, WithdrawalStorage>().AutoTrace<IWithdrawalStorage>();

    s.AddScoped<IWithdrawalImageEnricher, WithdrawalImageEnricher>()
      .AutoTrace<IWithdrawalImageEnricher>();

    s.AddScoped<IWithdrawalExporter, WithdrawalExporter>().AutoTrace<IWithdrawalExporter>();

    s.AddScoped<IFeeCalculator, FeeCalculator>().AutoTrace<IFeeCalculator>();

    s.AddScoped<IFeeRepository, FeeRepository>().AutoTrace<IFeeRepository>();

    s.AddScoped<IPayoutGateway, AirwallexPayoutGateway>().AutoTrace<IPayoutGateway>();

    s.AddScoped<IWithdrawalRefundRepository, WithdrawalRefundRepository>()
      .AutoTrace<IWithdrawalRefundRepository>();

    s.AddScoped<IRefundGateway, AirwallexRefundGateway>().AutoTrace<IRefundGateway>();

    s.AddScoped<IWithdrawalSettingsRepository, WithdrawalSettingsRepository>()
      .AutoTrace<IWithdrawalSettingsRepository>();

    // Cost
    s.AddScoped<ICostCalculator, CostCalculator>().AutoTrace<ICostCalculator>();

    s.AddScoped<ICostService, CostService>().AutoTrace<ICostService>();

    s.AddScoped<IDiscountService, DiscountService>().AutoTrace<IDiscountService>();

    s.AddScoped<IDiscountMatcher, DiscountMatcher>().AutoTrace<IDiscountMatcher>();

    s.AddScoped<IDiscountCalculator, DiscountCalculator>().AutoTrace<IDiscountCalculator>();

    s.AddScoped<ICostRepository, CostRepository>().AutoTrace<ICostRepository>();

    s.AddScoped<ICostPolicyRepository, CostPolicyRepository>().AutoTrace<ICostPolicyRepository>();

    s.AddScoped<IDiscountRepository, DiscountRepository>().AutoTrace<IDiscountRepository>();

    // payment
    s.AddScoped<IPaymentService, PaymentService>().AutoTrace<IPaymentService>();

    s.AddScoped<IPaymentRepository, PaymentRepository>().AutoTrace<IPaymentRepository>();

    s.AddScoped<IEncryptor, Encryptor>().AutoTrace<IEncryptor>();

    // airwallex
    s.AddScoped<IGatewayAuthenticator, AirwallexAuthenticator>()
      .AutoTrace<IGatewayAuthenticator>();

    s.AddScoped<IPaymentGateway, AirwallexGateway>().AutoTrace<IPaymentGateway>();

    // gateway-fee capture (Airwallex financial transactions -> GatewayFees)
    s.AddScoped<IGatewayFeeRepository, GatewayFeeRepository>().AutoTrace<IGatewayFeeRepository>();

    s.AddScoped<IGatewayFeeSource, AirwallexGatewayFeeSource>().AutoTrace<IGatewayFeeSource>();

    s.AddScoped<IGatewayFeeSyncService, GatewayFeeSyncService>()
      .AutoTrace<IGatewayFeeSyncService>();

    s.AddScoped<GatewayFeeSyncRunner>();

    // account-level FEE sweep (fees billed against no movement we track)
    s.AddScoped<IGatewayAccountFeeSource, AirwallexAccountFeeSource>()
      .AutoTrace<IGatewayAccountFeeSource>();

    s.AddScoped<GatewayAccountFeeSweep>();

    // recurring sync so fee data accrues without the manual endpoint
    s.AddHostedService<GatewayFeeSyncWorker>();

    s.AddScoped<AirWallexClient>();
    s.AddScoped<AirwallexEventAdapter>();
    s.AddScoped<AirwallexHmacCalculator>();
    s.AddScoped<AirwallexWebhookService>();

    // Booking Email Notifications
    s.AddScoped<IBookingEmailNotifier, BookingEmailNotifierAdapter>()
      .AutoTrace<IBookingEmailNotifier>();
    s.AddScoped<IBookingNotificationService, BookingNotificationService>()
      .AutoTrace<IBookingNotificationService>();

    // Announcements
    s.AddScoped<Announcements.IAnnouncementService, Announcements.AnnouncementService>()
      .AutoTrace<Announcements.IAnnouncementService>();

    return s;
  }
}
