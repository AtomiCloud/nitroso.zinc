import { Html, Head, Preview, Body, Container, Section, Row, Column, Tailwind } from '@react-email/components';
import { BunnyBookerHeader } from './header';
import { BunnyBookerFooter } from './footer';
import { TrustBanner } from './trust-banner';

export interface EmailLayoutProps {
  baseUrl: string;
  whatsappUrl: string;
  telegramUrl: string;
  supportEmail: string;
  userEmail: string;
  subject: string;
  previewText: string;
  children: React.ReactNode;
}

export const EmailLayout = ({
  baseUrl,
  whatsappUrl,
  telegramUrl,
  supportEmail,
  subject,
  previewText,
  userEmail,
  children,
}: EmailLayoutProps) => {
  return (
    <Html lang="en">
      <Head>
        <title>{subject}</title>
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <style>{`
          @media only screen and (max-width: 600px) {
            .mobile-padding {
              padding-left: 16px !important;
              padding-right: 16px !important;
            }
            .mobile-text {
              font-size: 14px !important;
            }
          }
          @media only screen and (max-width: 320px) {
            .contact-button {
              width: 36px !important;
              height: 36px !important;
              font-size: 10px !important;
            }
          }
        `}</style>
      </Head>
      <Preview>{previewText}</Preview>
      <Tailwind
        config={{
          theme: {
            extend: {
              colors: {
                'brand-orange': '#ff6b35',
                'brand-yellow': '#f7931e',
              },
              fontFamily: {
                sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
              },
            },
          },
        }}
      >
        <Body
          style={{
            backgroundColor: '#f9fafb',
            fontFamily: 'Inter, system-ui, -apple-system, sans-serif',
            margin: '0',
            padding: '0',
          }}
        >
          <Container
            style={{
              maxWidth: '600px',
              width: '100%',
              margin: '0 auto',
              backgroundColor: '#ffffff',
            }}
          >
            <BunnyBookerHeader baseUrl={baseUrl} />

            <Section
              style={{
                padding: '48px 32px',
              }}
              className="mobile-padding"
            >
              <Row>
                <Column>
                  <div
                    style={{
                      fontSize: '16px',
                      lineHeight: '1.6',
                      color: '#111827',
                    }}
                  >
                    {children}
                    <TrustBanner />
                  </div>
                </Column>
              </Row>
            </Section>

            <BunnyBookerFooter
              userEmail={userEmail}
              baseUrl={baseUrl}
              whatsappUrl={whatsappUrl}
              telegramUrl={telegramUrl}
              supportEmail={supportEmail}
            />
          </Container>
        </Body>
      </Tailwind>
    </Html>
  );
};
