import { Heading, Text } from '@react-email/components';
import { EmailLayout } from './lib/layout';
import { BookingDetails, RefundInfo } from './lib/booking-details';

interface BookingDuplicateEmailProps {
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
  duplicateDate: string;
}

export const BookingDuplicateEmail = ({
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
  duplicateDate = '{{ duplicateDate }}',
}: BookingDuplicateEmailProps) => {
  const subject = 'Duplicate Booking Refunded';
  const previewText = `Hi ${userName}, we found a duplicate ticket for this trip and refunded this booking.`;

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
        Duplicate Booking Refunded, {userName}
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
        While processing this booking we found that you already have a ticket for the same trip — booked through another
        channel or as a duplicate request. To avoid charging you twice, we've cancelled this booking and refunded it in
        full to your wallet as BunnyBooker credits.
      </Text>

      <BookingDetails
        bookingId={bookingId}
        status="Duplicate"
        direction={direction}
        bookingDate={bookingDate}
        bookingTime={bookingTime}
        refundDate={duplicateDate}
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
        <strong>Think this is a mistake?</strong>
        <br />
        If you did <em>not</em> already have a ticket for this trip, please contact us at {supportEmail} and we'll make
        it right straight away.
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
        Thanks for using BunnyBooker. 🐰
      </Text>
    </EmailLayout>
  );
};

// Preview props for development
BookingDuplicateEmail.PreviewProps = {
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
  duplicateDate: 'Friday, December 22, 2024',
} as BookingDuplicateEmailProps;

export default BookingDuplicateEmail;
