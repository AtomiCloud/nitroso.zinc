import { Text, Heading } from '@react-email/components';
import { EmailLayout } from './lib/layout';
import { BookingDetails, RefundInfo } from './lib/booking-details';

interface BookingRefundedEmailProps {
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
  refundDate: string;
}

export const BookingRefundedEmail = ({
  baseUrl = '{{ baseUrl }}',
  userName = '{{ userName }}',
  userEmail = '{{ userEmail }}',
  whatsappUrl = '{{ whatsappUrl }}',
  telegramUrl = '{{ telegramUrl }}',
  supportEmail = '{{ supportEmail }}',
  bookingId = '{{ bookingId }}',
  direction = '{{ direction }}',
  bookingDate = '{{ bookingDate }}',
  bookingTime = '{{ bookingTime }}',
  refundDate = '{{ refundDate }}',
}: BookingRefundedEmailProps) => {
  const subject = 'Booking Refunded - Payment Returned';
  const previewText = `Hi ${userName}, we've processed a full refund for your booking.`;

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
        Refund Processed, {userName}
      </Heading>

      <Text style={emailStyles.message}>
        We've processed a full refund for your booking due to system processing issues. Your refund has been instantly
        credited to your wallet as BunnyBooker credits. We apologize for any inconvenience this may have caused.
      </Text>

      <BookingDetails
        bookingId={bookingId}
        status="Refunded"
        direction={direction}
        bookingDate={bookingDate}
        bookingTime={bookingTime}
        refundDate={refundDate}
      />

      <RefundInfo
        refundType="Full Refund"
        refundStatus="Instant"
        refundMethod="BunnyBooker Credits"
        withdrawalAvailable={true}
        variant="blue"
      />

      <Text style={emailStyles.message}>
        <strong>Your credits are ready!</strong>
        <br />
        • Use credits for future bookings instantly
        <br />
        • Withdraw credits to your bank account anytime
        <br />• Credits never expire - use them when you're ready
      </Text>

      <Text style={emailStyles.message}>
        <strong>Our commitment:</strong>
        <br />
        We're continuously improving our platform to prevent such issues. Your satisfaction is our priority, and we
        appreciate your patience and understanding.
      </Text>

      <Text style={emailStyles.message}>
        Thank you for giving us the opportunity to make this right. We look forward to serving you better! 🐰💙
      </Text>
    </EmailLayout>
  );
};

// Preview props for development
BookingRefundedEmail.PreviewProps = {
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
  refundDate: 'Friday, December 22, 2024',
} as BookingRefundedEmailProps;

export default BookingRefundedEmail;

const emailStyles = {
  greeting: {
    fontSize: '24px',
    color: '#2c3e50',
    margin: '0 0 20px 0',
    fontWeight: '600',
  },
  message: {
    fontSize: '16px',
    lineHeight: '1.6',
    color: '#555555',
    margin: '0 0 25px 0',
  },
};
