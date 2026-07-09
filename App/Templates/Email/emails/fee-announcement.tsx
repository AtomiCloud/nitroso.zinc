import { Heading, Section, Text } from '@react-email/components';
import { EmailLayout } from './lib/layout';

// Generalized fee announcement: works for withdrawal AND deposit fees,
// flat + percentage, scheduled or immediate, introduction or removal.
// The C# service composes the full sentences (changeLine/deductLine/
// effectiveLine/reasoning) so this template needs no conditionals.
interface FeeAnnouncementEmailProps {
  baseUrl: string;
  userName: string;
  userEmail: string;
  whatsappUrl: string;
  telegramUrl: string;
  supportEmail: string;
  // "withdrawal" | "deposit"
  feeKind: string;
  // why the fee is changing — customizable by the admin sending it
  reasoning: string;
  // e.g. "A 4% + SGD 1.00 fee will apply to all wallet withdrawals"
  changeLine: string;
  // e.g. "The fee is deducted from the withdrawn amount"
  deductLine: string;
  // e.g. "This change is effective immediately" / "takes effect on ..."
  effectiveLine: string;
}

export const FeeAnnouncementEmail = ({
  baseUrl = '{{ baseUrl }}',
  whatsappUrl = '{{ whatsappUrl }}',
  telegramUrl = '{{ telegramUrl }}',
  supportEmail = '{{ supportEmail }}',
  userName = '{{ userName }}',
  userEmail = '{{ userEmail }}',
  feeKind = '{{ feeKind }}',
  reasoning = '{{ reasoning }}',
  changeLine = '{{ changeLine }}',
  deductLine = '{{ deductLine }}',
  effectiveLine = '{{ effectiveLine }}',
}: FeeAnnouncementEmailProps) => {
  const subject = `An Important Update on ${feeKind} fees`;
  const previewText = `Hi ${userName}, an important update about your BunnyBooker wallet.`;

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
        An Important Update on your Wallet, {userName}
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
        We're writing to let you know about a change to the {feeKind} fees on your BunnyBooker wallet. We don't make
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
        {reasoning}
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
          • <strong>{changeLine}</strong>
          <br />• {deductLine}
          <br />• {effectiveLine}
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
        We're sorry for any inconvenience this causes. This step is necessary to keep BunnyBooker sustainable and fair
        for everyone.
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
FeeAnnouncementEmail.PreviewProps = {
  baseUrl: 'https://bunnybooker.com',
  telegramUrl: 'https://t.me/bunnybooker',
  whatsappUrl: 'https://wa.me/60123456789',
  supportEmail: 'support@bunnybooker.com',
  userName: 'John Doe',
  userEmail: 'john@example.com',
  feeKind: 'withdrawal',
  reasoning:
    "Recently, we've seen widespread abuse of our wallet system, with large sums being deposited and withdrawn purely to churn funds through the platform. To protect the platform and the community of genuine travelers who use it, we're introducing a small fee on withdrawals.",
  changeLine: 'A 4% + SGD 1.00 fee will apply to all wallet withdrawals',
  deductLine: 'The fee is deducted from the withdrawn amount',
  effectiveLine: 'This change takes effect on 1 August 2026, 00:00 UTC',
} as FeeAnnouncementEmailProps;

export default FeeAnnouncementEmail;
