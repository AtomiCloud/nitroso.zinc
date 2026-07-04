import { Heading, Text } from '@react-email/components';
import { EmailLayout } from './lib/layout';
import { BookingDetails } from './lib/booking-details';

interface BookingManualInterventionEmailProps {
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
}

export const BookingManualInterventionEmail = ({
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
}: BookingManualInterventionEmailProps) => {
  const subject = 'Your Booking Is Under Review';
  const previewText = `Hi ${userName}, we hit a snag with your booking and our team is looking into it.`;

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
        We're Reviewing Your Booking, {userName}
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
        We hit a snag while finalising this booking, so we've paused it for a manual check by our team. Your money is
        safe — nothing has been lost — and we're sorting it out. We'll email you again as soon as it's resolved.
      </Text>

      <BookingDetails
        bookingId={bookingId}
        status="Under Review"
        direction={direction}
        bookingDate={bookingDate}
        bookingTime={bookingTime}
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
        <strong>Need this sorted urgently, or think it's an error?</strong>
        <br />
        Reply to this email or contact us at {supportEmail} with your Booking ID above and we'll prioritise it.
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
        Thanks for your patience. 🐰
      </Text>
    </EmailLayout>
  );
};

// Preview props for development
BookingManualInterventionEmail.PreviewProps = {
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
} as BookingManualInterventionEmailProps;

export default BookingManualInterventionEmail;
