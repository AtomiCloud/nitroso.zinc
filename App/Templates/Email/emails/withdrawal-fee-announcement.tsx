import { Heading, Section, Text } from '@react-email/components';
import { EmailLayout } from './lib/layout';

interface WithdrawalFeeAnnouncementEmailProps {
  baseUrl: string;
  userName: string;
  userEmail: string;
  whatsappUrl: string;
  telegramUrl: string;
  supportEmail: string;
  feePercent: string;
}

export const WithdrawalFeeAnnouncementEmail = ({
  baseUrl = '{{ baseUrl }}',
  whatsappUrl = '{{ whatsappUrl }}',
  telegramUrl = '{{ telegramUrl }}',
  supportEmail = '{{ supportEmail }}',
  userName = '{{ userName }}',
  userEmail = '{{ userEmail }}',
  feePercent = '{{ feePercent }}',
}: WithdrawalFeeAnnouncementEmailProps) => {
  const subject = `Introducing a ${feePercent}% Withdrawal Fee`;
  const previewText = `Hi ${userName}, an important update about wallet withdrawals on BunnyBooker.`;

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
        An Important Update on Withdrawals, {userName}
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
        We're writing to let you know about a change to how withdrawals from your BunnyBooker wallet work. We don't make
        changes like this lightly, and we want to be upfront about why.
      </Text>

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
        Recently, we've seen widespread abuse of our wallet system, with large sums being deposited and withdrawn purely
        to churn funds through the platform. This activity drives up costs for everyone and puts the smooth, reliable
        service you count on at risk. To protect the platform and the community of genuine travelers who use it, we're
        introducing a small fee on withdrawals.
      </Text>

      <Section
        style={{
          background: 'linear-gradient(to right, #fef7ec, #fef3c7)',
          borderRadius: '16px',
          border: '1px solid #fed7aa',
          padding: '24px 28px',
          margin: '0 0 32px 0',
        }}
      >
        <Heading
          as="h3"
          style={{
            color: '#111827',
            fontSize: '20px',
            fontWeight: '600',
            margin: '0 0 16px 0',
          }}
        >
          What's changing
        </Heading>
        <Text
          style={{
            fontSize: '16px',
            color: '#111827',
            lineHeight: '1.75',
            margin: '0',
            fontWeight: '500',
          }}
        >
          • A <strong>{feePercent}% fee</strong> now applies to all wallet withdrawals
          <br />• The fee is <strong>deducted from the withdrawn amount</strong>
          <br />• <strong>Deposits remain completely free</strong>
          <br />• This change is <strong>effective immediately</strong>
        </Text>
      </Section>

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
        Nothing else changes: your credits, bookings and refunds all work exactly as before, and using your wallet
        balance for bookings stays free.
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
        We're sorry for any inconvenience this causes, especially to those of you who have always used the wallet as
        intended. This step is necessary to keep BunnyBooker sustainable and fair for everyone.
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
        If you have any questions, our support team is happy to help. Thank you for your understanding and for being
        part of BunnyBooker. 🐰
      </Text>
    </EmailLayout>
  );
};

// Preview props for development
WithdrawalFeeAnnouncementEmail.PreviewProps = {
  baseUrl: 'https://bunnybooker.com',
  telegramUrl: 'https://t.me/bunnybooker',
  whatsappUrl: 'https://wa.me/60123456789',
  supportEmail: 'support@bunnybooker.com',
  userName: 'John Doe',
  userEmail: 'john@example.com',
  feePercent: '4',
} as WithdrawalFeeAnnouncementEmailProps;

export default WithdrawalFeeAnnouncementEmail;
