import { Section, Row, Column, Button, Link } from '@react-email/components';

interface CallToActionProps {
  href: string;
  text: string;
  emoji?: string;
  variant?: 'primary' | 'secondary';
}

export const CallToAction = ({ href, text, emoji, variant = 'primary' }: CallToActionProps) => {
  const getButtonStyle = () => {
    return variant === 'primary'
      ? {
          background: 'linear-gradient(135deg, #f7931e 0%, #ff6b35 100%)',
          color: '#ffffff',
          boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)',
        }
      : {
          backgroundColor: '#f3f4f6',
          color: '#111827',
          border: '1px solid #d1d5db',
        };
  };

  return (
    <Section style={{ textAlign: 'center' as const, margin: '32px 0' }}>
      <Row>
        <Column style={{ textAlign: 'center' as const }}>
          <Link
            href={href}
            style={{
              display: 'inline-block',
              padding: '16px 32px',
              borderRadius: '12px',
              fontSize: '18px',
              fontWeight: '600',
              textDecoration: 'none',
              ...getButtonStyle(),
            }}
          >
            {emoji && <span style={{ marginRight: '8px' }}>{emoji}</span>}
            {text}
          </Link>
        </Column>
      </Row>
    </Section>
  );
};

interface TextCallToActionProps {
  href: string;
  text: string;
  emoji?: string;
}

export const TextCallToAction = ({ href, text, emoji }: TextCallToActionProps) => {
  return (
    <Section style={{ textAlign: 'center' as const, margin: '32px 0' }}>
      <Row>
        <Column style={{ textAlign: 'center' as const }}>
          <Link
            href={href}
            style={{
              display: 'inline-block',
              background: 'linear-gradient(135deg, #f7931e 0%, #ff6b35 100%)',
              color: '#ffffff',
              padding: '16px 32px',
              borderRadius: '12px',
              fontSize: '18px',
              fontWeight: '600',
              textDecoration: 'none',
              boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)',
            }}
          >
            {emoji && <span style={{ marginRight: '8px' }}>{emoji}</span>}
            {text}
          </Link>
        </Column>
      </Row>
    </Section>
  );
};
