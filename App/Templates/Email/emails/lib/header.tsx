import { Section, Row, Column, Heading, Text, Link } from '@react-email/components';

interface HeaderProps {
  baseUrl: string;
}

export const BunnyBookerHeader = ({ baseUrl }: HeaderProps) => {
  return (
    <Section
      style={{
        background: 'linear-gradient(135deg, #f7931e 0%, #ff6b35 100%)',
        padding: '64px 32px',
        textAlign: 'center' as const,
      }}
    >
      <Row>
        <Column>
          <Link
            href={baseUrl}
            style={{
              color: '#ffffff',
              fontSize: '36px',
              fontWeight: 'bold',
              textDecoration: 'none',
              display: 'inline-block',
              marginBottom: '12px',
            }}
          >
            BunnyBooker
          </Link>
          <Text
            style={{
              color: '#ffffff',
              opacity: '0.95',
              fontSize: '18px',
              margin: '0',
              fontWeight: '500',
            }}
          >
            Everyone deserves a comfortable KTMB train ride to JB
          </Text>
        </Column>
      </Row>
    </Section>
  );
};
