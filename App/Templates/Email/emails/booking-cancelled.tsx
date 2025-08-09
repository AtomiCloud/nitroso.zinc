import { Heading, Text } from '@react-email/components';
import { EmailLayout } from './lib/layout';
import { BookingDetails, RefundInfo } from './lib/booking-details';

interface BookingCancelledEmailProps {
  baseUrl: string;
  userName: string;
  userEmail: string;
  whatsappUrl: string;
  telegramUrl: string;
  supportEmail: string;
  bookingId: string;
  direction: string;
  bookingDate: string;
  bookingTime: string;
  cancellationDate: string;
}

export const BookingCancelledEmail = ({
  baseUrl = '{{ baseUrl }}',
  whatsappUrl = '{{ whatsappUrl }}',
  telegramUrl = '{{ telegramUrl }}',
  supportEmail = '{{ supportEmail }}',
  userName = '{{ userName }}',
  userEmail = '{{ userEmail }}',
  bookingId = '{{ bookingId }}',
  direction = '{{ direction }}',
  bookingDate = '{{ bookingDate }}',
  bookingTime = '{{ bookingTime }}',
  cancellationDate = '{{ cancellationDate }}',
}: BookingCancelledEmailProps) => {
  const subject = 'Booking Cancelled - Refund Processed';
  const previewText = `Hi ${userName}, your booking has been cancelled and refund processed.`;

  return (
    <EmailLayout
      baseUrl={baseUrl}
      supportEmail={supportEmail}
      whatsappUrl={whatsappUrl}
      telegramUrl={telegramUrl}
      subject={subject}
      previewText={previewText}
      userEmail={userEmail}
    >
      <Heading
        as="h1"
        style={{
          fontSize: '28px',
          fontWeight: 'bold',
          color: '#111827',
          margin: '0 0 24px 0',
          lineHeight: '1.2',
        }}
      >
        Booking Cancelled, {userName}
      </Heading>

      <Text
        style={{
          fontSize: '18px',
          color: '#111827',
          lineHeight: '1.75',
          marginBottom: '32px',
          margin: '0 0 32px 0',
          fontWeight: '500',
        }}
      >
        We've successfully cancelled your booking as requested. Your refund has been instantly credited to your wallet
        as BunnyBooker credits.
      </Text>

      <BookingDetails
        bookingId={bookingId}
        status="Cancelled"
        direction={direction}
        bookingDate={bookingDate}
        bookingTime={bookingTime}
        cancellationDate={cancellationDate}
      />

      <RefundInfo
        refundType="Full Refund"
        refundStatus="Instant"
        refundMethod="BunnyBooker Credits"
        withdrawalAvailable={true}
        variant="green"
      />

      <Text
        style={{
          fontSize: '18px',
          color: '#111827',
          lineHeight: '1.75',
          marginBottom: '24px',
          margin: '0 0 24px 0',
          fontWeight: '500',
        }}
      >
        <strong>Your credits are ready!</strong>
        <br />
        • Use credits for future bookings instantly
        <br />
        • Withdraw credits to your bank account anytime
        <br />• Credits never expire - use them when you're ready
      </Text>

      <Text
        style={{
          fontSize: '18px',
          color: '#111827',
          lineHeight: '1.75',
          marginBottom: '24px',
          margin: '0 0 24px 0',
          fontWeight: '500',
        }}
      >
        Thanks for using BunnyBooker. We hope to serve you again soon! 🐰
      </Text>
    </EmailLayout>
  );
};

// Preview props for development
BookingCancelledEmail.PreviewProps = {
  baseUrl: 'https://bunnybooker.com',
  telegramUrl: 'https://t.me/bunnybooker',
  whatsappUrl: 'https://wa.me/60123456789',
  supportEmail: 'support@bunnybooker.com',
  userName: 'John Doe',
  userEmail: 'john@example.com',
  bookingId: 'BB123456789',
  direction: 'Singapore → Johor Bahru',
  bookingDate: 'Saturday, December 23, 2024',
  bookingTime: '08:30',
  cancellationDate: 'Friday, December 22, 2024',
} as BookingCancelledEmailProps;

export default BookingCancelledEmail;
