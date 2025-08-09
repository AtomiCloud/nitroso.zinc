import { Section, Row, Column, Text, Heading } from '@react-email/components';

interface BookingDetailsProps {
  bookingId: string;
  status: 'Confirmed' | 'Cancelled' | 'Terminated' | 'Refunded';
  direction: string;
  bookingDate: string;
  bookingTime: string;
  ticketNumber?: string;
  bookingNumber?: string;
  cancellationDate?: string;
  terminationDate?: string;
  refundDate?: string;
}

export const BookingDetails = ({
  bookingId,
  status,
  direction,
  bookingDate,
  bookingTime,
  ticketNumber,
  bookingNumber,
  cancellationDate,
  terminationDate,
  refundDate,
}: BookingDetailsProps) => {
  return (
    <Section className="bg-gray-50 border border-gray-300 rounded-2xl p-8 my-8">
      <Row>
        <Column>
          <Heading as="h3" className="text-gray-900 text-xl font-bold mb-6 m-0">
            📋 {getHeadingTitle(status)} Details
          </Heading>

          <DetailItem label="Booking ID" value={bookingId} isStrong />
          <DetailItem label="Status" value={<StatusBadge status={status} />} />
          <DetailItem label="Direction" value={direction} />
          <DetailItem label={getDateLabel(status)} value={bookingDate} />
          <DetailItem label={getTimeLabel(status)} value={bookingTime} />

          {ticketNumber && <DetailItem label="Ticket Number" value={ticketNumber} isStrong />}
          {bookingNumber && <DetailItem label="Booking Reference" value={bookingNumber} isStrong />}
          {cancellationDate && <DetailItem label="Cancellation Date" value={cancellationDate} />}
          {terminationDate && <DetailItem label="Termination Date" value={terminationDate} />}
          {refundDate && <DetailItem label="Refund Date" value={refundDate} />}
        </Column>
      </Row>
    </Section>
  );
};

interface RefundInfoProps {
  refundType: 'Full Refund' | 'Partial Refund (Per Policy)';
  refundStatus: 'Instant' | 'Processing';
  refundMethod: string;
  withdrawalAvailable: boolean;
  variant?: 'green' | 'blue' | 'yellow';
}

export const RefundInfo = ({
  refundType,
  refundStatus,
  refundMethod,
  withdrawalAvailable,
  variant = 'green',
}: RefundInfoProps) => {
  const getVariantClasses = () => {
    switch (variant) {
      case 'green':
        return 'bg-green-50 border-green-200';
      case 'blue':
        return 'bg-blue-50 border-blue-200';
      case 'yellow':
        return 'bg-yellow-50 border-yellow-200';
      default:
        return 'bg-green-50 border-green-200';
    }
  };

  return (
    <Section className={`${getVariantClasses()} border rounded-2xl p-8 my-8`}>
      <Row>
        <Column>
          <Heading as="h3" className="text-gray-900 text-xl font-semibold mb-6 flex items-center">
            💰 Refund Information
          </Heading>

          <DetailItem label="Refund Type" value={refundType} />
          <DetailItem
            label="Refund Status"
            value={
              <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-green-100 text-green-800">
                {refundStatus}
              </span>
            }
          />
          <DetailItem label="Refund Method" value={refundMethod} />
          <DetailItem label="Withdrawal Available" value={withdrawalAvailable ? 'Yes - Anytime' : 'No'} />
        </Column>
      </Row>
    </Section>
  );
};

interface DetailItemProps {
  label: string;
  value: React.ReactNode;
  isStrong?: boolean;
}

const DetailItem = ({ label, value, isStrong }: DetailItemProps) => (
  <table width="100%" style={{ borderBottom: '1px solid #e5e7eb', padding: '8px 0' }}>
    <tr>
      <td
        style={{
          padding: '8px 0',
          verticalAlign: 'top',
          width: '35%',
        }}
      >
        <Text
          style={{
            color: '#6b7280',
            fontWeight: '500',
            fontSize: '13px',
            margin: '0',
            textTransform: 'uppercase',
            letterSpacing: '0.5px',
          }}
        >
          {label}
        </Text>
      </td>
      <td
        style={{
          padding: '8px 0 8px 12px',
          verticalAlign: 'top',
        }}
      >
        <Text
          style={{
            color: '#111827',
            fontSize: '15px',
            margin: '0',
            fontWeight: isStrong ? '700' : '600',
          }}
        >
          {value}
        </Text>
      </td>
    </tr>
  </table>
);

const StatusBadge = ({ status }: { status: string }) => {
  const getStatusClasses = () => {
    switch (status) {
      case 'Confirmed':
        return 'bg-green-100 text-green-800';
      case 'Cancelled':
        return 'bg-red-100 text-red-800';
      case 'Terminated':
        return 'bg-yellow-100 text-yellow-800';
      case 'Refunded':
        return 'bg-blue-100 text-blue-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <span
      className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium uppercase tracking-wide ${getStatusClasses()}`}
    >
      {status}
    </span>
  );
};

const getHeadingTitle = (status: string) => {
  switch (status) {
    case 'Confirmed':
      return 'Booking Confirmation';
    case 'Cancelled':
      return 'Cancelled Booking';
    case 'Terminated':
      return 'Terminated Booking';
    case 'Refunded':
      return 'Refunded Booking';
    default:
      return 'Booking';
  }
};

const getDateLabel = (status: string) => {
  return status === 'Confirmed' ? 'Travel Date' : 'Original Date';
};

const getTimeLabel = (status: string) => {
  return status === 'Confirmed' ? 'Departure Time' : 'Original Time';
};
