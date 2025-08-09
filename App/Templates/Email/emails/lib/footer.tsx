import { Section, Row, Column, Text, Link, Heading, Container, Button, Hr } from '@react-email/components';

interface BunnyBookerFooterProps {
  supportEmail?: string;
  whatsappUrl?: string;
  telegramUrl?: string;
  baseUrl?: string;
  userEmail?: string;
}

export const BunnyBookerFooter = ({
  supportEmail,
  whatsappUrl,
  telegramUrl,
  baseUrl,
  userEmail,
}: BunnyBookerFooterProps = {}) => {
  return (
    <>
      <Section
        style={{
          backgroundColor: '#0f0f0f',
          padding: '0',
        }}
      >
        <Container
          style={{
            maxWidth: '600px',
            margin: '0 auto',
            padding: '48px 20px',
          }}
        >
          <Row>
            <Column align="center">
              <Heading
                as="h2"
                style={{
                  color: '#ffffff',
                  fontSize: '20px',
                  fontWeight: '700',
                  margin: '0 0 8px 0',
                  letterSpacing: '-0.5px',
                }}
              >
                Need Help?
              </Heading>

              <Text
                style={{
                  color: '#6b7280',
                  fontSize: '14px',
                  lineHeight: '20px',
                  margin: '0 0 32px 0',
                }}
              >
                Our team is here to assist you
              </Text>

              <Container
                style={{
                  backgroundColor: '#1a1a1a',
                  borderRadius: '12px',
                  padding: '24px',
                  marginBottom: '32px',
                  maxWidth: '380px',
                }}
              >
                <Row>
                  <Column align="center">
                    <div style={{ marginBottom: '24px' }}>
                      <Text
                        style={{
                          color: '#9ca3af',
                          fontSize: '12px',
                          fontWeight: '600',
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          margin: '0 0 16px 0',
                        }}
                      >
                        Instant Support
                      </Text>

                      <div style={{ marginBottom: '8px' }}>
                        <Button
                          href={whatsappUrl}
                          style={{
                            backgroundColor: '#25d366',
                            color: '#ffffff',
                            padding: '8px',
                            borderRadius: '6px',
                            fontSize: '13px',
                            fontWeight: '600',
                            textDecoration: 'none',
                            display: 'inline-block',
                            width: '100%',
                            textAlign: 'center',
                          }}
                        >
                          💬 WhatsApp Us
                        </Button>
                      </div>

                      <div>
                        <Button
                          href={telegramUrl}
                          style={{
                            backgroundColor: '#0088cc',
                            color: '#ffffff',
                            padding: '8px',
                            borderRadius: '6px',
                            fontSize: '13px',
                            fontWeight: '600',
                            textDecoration: 'none',
                            display: 'inline-block',
                            width: '100%',
                            textAlign: 'center',
                          }}
                        >
                          ✈️ Telegram
                        </Button>
                      </div>

                      <Text
                        style={{
                          color: '#6b7280',
                          fontSize: '12px',
                          margin: '12px 0 0 0',
                          textAlign: 'center',
                        }}
                      >
                        Available 24/7
                      </Text>
                    </div>

                    <Hr
                      style={{
                        borderColor: '#2a2a2a',
                        margin: '20px 0',
                      }}
                    />

                    <div>
                      <Text
                        style={{
                          color: '#9ca3af',
                          fontSize: '12px',
                          fontWeight: '600',
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          margin: '0 0 8px 0',
                        }}
                      >
                        Email Support
                      </Text>
                      <Link
                        href={`mailto:${supportEmail}`}
                        style={{
                          color: '#ffffff',
                          textDecoration: 'none',
                          fontSize: '15px',
                          fontWeight: '500',
                        }}
                      >
                        {supportEmail}
                      </Link>
                      <Text
                        style={{
                          color: '#6b7280',
                          fontSize: '12px',
                          margin: '4px 0 0 0',
                        }}
                      >
                        We'll get back to you soon
                      </Text>
                    </div>
                  </Column>
                </Row>
              </Container>
              <Row>
                <Column align="center">
                  <Text
                    style={{
                      color: '#6b7280',
                      fontSize: '13px',
                      lineHeight: '20px',
                      margin: '0 0 16px 0',
                    }}
                  >
                    <Link
                      href={`${baseUrl}/policy`}
                      style={{
                        color: '#6b7280',
                        textDecoration: 'none',
                      }}
                    >
                      Policies
                    </Link>
                    <span style={{ margin: '0 12px', color: '#4b5563' }}>•</span>
                    <Link
                      href={`${baseUrl}/privacy`}
                      style={{
                        color: '#6b7280',
                        textDecoration: 'none',
                      }}
                    >
                      Privacy
                    </Link>
                    <span style={{ margin: '0 12px', color: '#4b5563' }}>•</span>
                    <Link
                      href={`${baseUrl}/terms`}
                      style={{
                        color: '#6b7280',
                        textDecoration: 'none',
                      }}
                    >
                      Terms
                    </Link>
                  </Text>

                  <Hr
                    style={{
                      borderColor: '#1f1f1f',
                      margin: '32px 0 24px 0',
                    }}
                  />

                  <Text
                    style={{
                      color: '#4b5563',
                      fontSize: '12px',
                      lineHeight: '18px',
                      margin: '0',
                    }}
                  >
                    <span style={{ fontWeight: '600', color: '#6b7280' }}>BUNNYBOOKER</span>
                    <br />
                    60 Paya Lebar Road, #07-54
                    <br />
                    Paya Lebar Square, Singapore 409051
                  </Text>
                </Column>
              </Row>
            </Column>
          </Row>
        </Container>
      </Section>
      <Section
        style={{
          backgroundColor: '#000000',
          padding: '0',
        }}
      >
        <Container
          style={{
            maxWidth: '600px',
            margin: '0 auto',
            padding: '24px 20px',
          }}
        >
          <Row>
            <Column align="center">
              <Text
                style={{
                  fontSize: '11px',
                  lineHeight: '16px',
                  margin: '0',
                  color: '#4b5563',
                }}
              >
                © 2025 BunnyBooker. All rights reserved.
                <br />
                This email was sent to <span style={{ color: '#6b7280' }}>{userEmail}</span>
              </Text>
            </Column>
          </Row>
        </Container>
      </Section>
    </>
  );
};
