import { Text, Heading, Section, Row, Column } from '@react-email/components';
import { EmailLayout } from './lib/layout';
import { BookingDetails, RefundInfo } from './lib/booking-details';

interface BookingTerminatedEmailProps {
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
  terminationDate: string;
}

export const BookingTerminatedEmail = ({
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
  terminationDate = '{{ terminationDate }}',
}: BookingTerminatedEmailProps) => {
  const subject = 'Booking Terminated - Partial Refund Processed';
  const previewText = `Hi ${userName}, your booking has been terminated with partial refund.`;

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
        Booking Terminated, {userName}
      </Heading>

      <Text style={emailStyles.message}>
        Your booking has been terminated as requested. According to our cancellation policy, a partial refund has been
        instantly credited to your wallet as BunnyBooker credits.
      </Text>

      <BookingDetails
        bookingId={bookingId}
        status="Terminated"
        direction={direction}
        bookingDate={bookingDate}
        bookingTime={bookingTime}
        terminationDate={terminationDate}
      />

      <RefundInfo
        refundType="Partial Refund (Per Policy)"
        refundStatus="Instant"
        refundMethod="BunnyBooker Credits"
        withdrawalAvailable={true}
        variant="yellow"
      />

      <Section style={emailStyles.infoSection}>
        <Row>
          <Column>
            <Heading as="h3" style={emailStyles.infoHeading}>
              ℹ️ Important Information
            </Heading>
            <Text style={emailStyles.infoText}>
              <strong>Cancellation Policy:</strong> Bookings terminated after confirmation are subject to our
              cancellation policy. The refund amount is calculated based on the time between termination and original
              departure time.{' '}
              <a href="{{BASE_URL}}/cancellation-policy" style={emailStyles.policyLink}>
                Learn more about our policy
              </a>
              .
            </Text>
          </Column>
        </Row>
      </Section>

      <Text style={emailStyles.message}>
        <strong>Need assistance?</strong>
        <br />
        If you have any questions about your termination or refund, our customer support team is ready to help. You can
        also view detailed policy information on our website.
      </Text>

      <Text style={emailStyles.message}>
        We understand plans can change, and we're here to help make it as smooth as possible. Thank you for choosing
        BunnyBooker! 🐰
      </Text>
    </EmailLayout>
  );
};

// Preview props for development
BookingTerminatedEmail.PreviewProps = {
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
  terminationDate: 'Friday, December 22, 2024',
} as BookingTerminatedEmailProps;

export default BookingTerminatedEmail;

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
  infoSection: {
    backgroundColor: '#f8f9fa',
    borderLeft: '4px solid #6c757d',
    padding: '20px',
    margin: '25px 0',
    borderRadius: '0 8px 8px 0',
  },
  infoHeading: {
    color: '#2c3e50',
    margin: '0 0 15px 0',
    fontSize: '18px',
    fontWeight: '600',
  },
  infoText: {
    color: '#555555',
    fontSize: '14px',
    lineHeight: '1.5',
    margin: '0',
  },
  policyLink: {
    color: '#667eea',
  },
};
