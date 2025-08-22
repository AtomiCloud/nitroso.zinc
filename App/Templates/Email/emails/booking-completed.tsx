import { Text, Heading } from '@react-email/components';
import { EmailLayout } from './lib/layout';
import { BookingDetails } from './lib/booking-details';
import { CallToAction } from './lib/call-to-action';

interface BookingCompletedEmailProps {
  userName: string;
  userEmail: string;
  whatsappUrl: string;
  telegramUrl: string;
  supportEmail: string;
  bookingId: string;
  direction: string;
  bookingDate: string;
  bookingTime: string;
  ticketNumber?: string;
  bookingNumber?: string;
  baseUrl: string;
}

export const BookingCompletedEmail = ({
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
  ticketNumber = '{{ ticketNumber }}',
  bookingNumber = '{{ bookingNumber }}',
}: BookingCompletedEmailProps) => {
  const subject = '🎉 Booking Confirmed - Your Journey is Ready!';
  const previewText = `Great news ${userName}! Your booking has been confirmed and you're all set.`;

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
        🎉 Great news, {userName}!
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
        Everyone deserves a comfortable KTMB train ride to JB. We've successfully secured your tickets - no more queue
        and crowd stress!
      </Text>

      <BookingDetails
        bookingId={bookingId}
        status="Confirmed"
        direction={direction}
        bookingDate={bookingDate}
        bookingTime={bookingTime}
        ticketNumber={ticketNumber}
        bookingNumber={bookingNumber}
      />

      <CallToAction href={`${baseUrl}/bookings/${bookingId}`} text="View My Tickets" emoji="🎫" />

      <div
        style={{
          backgroundColor: '#f0fdf4',
          border: '1px solid #bbf7d0',
          borderRadius: '16px',
          padding: '24px',
          marginBottom: '32px',
        }}
      >
        <Heading
          as="h3"
          style={{
            color: '#14532d',
            fontSize: '18px',
            fontWeight: '600',
            marginBottom: '16px',
            margin: '0 0 16px 0',
          }}
        >
          ✅ What's next?
        </Heading>
        <div style={{ marginBottom: '12px' }}>
          <span style={{ color: '#15803d', fontWeight: 'bold', marginRight: '12px', fontSize: '18px' }}>1.</span>
          <Text style={{ color: '#111827', margin: '0', display: 'inline', fontWeight: '500' }}>
            Save this confirmation email for your records
          </Text>
        </div>
        <div style={{ marginBottom: '12px' }}>
          <span style={{ color: '#15803d', fontWeight: 'bold', marginRight: '12px', fontSize: '18px' }}>2.</span>
          <Text style={{ color: '#111827', margin: '0', display: 'inline', fontWeight: '500' }}>
            Arrive at the station 20 minutes before departure
          </Text>
        </div>
        <div>
          <span style={{ color: '#15803d', fontWeight: 'bold', marginRight: '12px', fontSize: '18px' }}>3.</span>
          <Text style={{ color: '#111827', margin: '0', display: 'inline', fontWeight: '500' }}>
            Contact us if you are unsure or worried about anything
          </Text>
        </div>
      </div>
    </EmailLayout>
  );
};

// Preview props for development
BookingCompletedEmail.PreviewProps = {
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
  ticketNumber: 'TKT987654321',
  bookingNumber: 'BK123456789',
} as BookingCompletedEmailProps;

export default BookingCompletedEmail;
