import { Section, Row, Column, Text, Heading } from '@react-email/components';

export const TrustBanner = () => {
  return (
    <Section
      style={{
        textAlign: 'center' as const,
        margin: '48px 0',
        padding: '32px',
        background: 'linear-gradient(to right, #fef7ec, #fef3c7)',
        borderRadius: '16px',
        border: '1px solid #fed7aa',
      }}
    >
      <Row>
        <Column>
          <Heading
            as="h3"
            style={{
              color: '#111827',
              fontSize: '20px',
              fontWeight: '600',
              marginBottom: '24px',
              margin: '0 0 24px 0',
            }}
          >
            Join thousands of travelers who trust BunnyBooker!
          </Heading>

          {/* Desktop/Wide view - 2x2 grid */}
          <div style={{ display: 'none', className: 'desktop-grid' }}>
            <table style={{ width: '100%', maxWidth: '400px', margin: '0 auto' }}>
              <tr>
                <td style={{ padding: '6px', width: '50%' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: '#ffffff',
                      borderRadius: '8px',
                      padding: '12px 8px',
                      boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                      minHeight: '55px',
                    }}
                  >
                    <span style={{ fontSize: '20px', marginRight: '6px' }}>🎯</span>
                    <div style={{ textAlign: 'left' as const }}>
                      <div style={{ fontSize: '13px', fontWeight: '700', color: '#111827' }}>99%</div>
                      <div style={{ fontSize: '11px', color: '#374151', fontWeight: '500' }}>Success</div>
                    </div>
                  </div>
                </td>
                <td style={{ padding: '6px', width: '50%' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: '#ffffff',
                      borderRadius: '8px',
                      padding: '12px 8px',
                      boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                      minHeight: '55px',
                    }}
                  >
                    <span style={{ fontSize: '20px', marginRight: '6px' }}>📧</span>
                    <div style={{ textAlign: 'left' as const }}>
                      <div style={{ fontSize: '13px', fontWeight: '700', color: '#111827' }}>Instant</div>
                      <div style={{ fontSize: '11px', color: '#374151', fontWeight: '500' }}>Updates</div>
                    </div>
                  </div>
                </td>
              </tr>
              <tr>
                <td style={{ padding: '6px', width: '50%' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: '#ffffff',
                      borderRadius: '8px',
                      padding: '12px 8px',
                      boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                      minHeight: '55px',
                    }}
                  >
                    <span style={{ fontSize: '20px', marginRight: '6px' }}>💰</span>
                    <div style={{ textAlign: 'left' as const }}>
                      <div style={{ fontSize: '13px', fontWeight: '700', color: '#111827' }}>Refunds</div>
                      <div style={{ fontSize: '11px', color: '#374151', fontWeight: '500' }}>Guaranteed</div>
                    </div>
                  </div>
                </td>
                <td style={{ padding: '6px', width: '50%' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: '#ffffff',
                      borderRadius: '8px',
                      padding: '12px 8px',
                      boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                      minHeight: '55px',
                    }}
                  >
                    <span style={{ fontSize: '20px', marginRight: '6px' }}>🕐</span>
                    <div style={{ textAlign: 'left' as const }}>
                      <div style={{ fontSize: '13px', fontWeight: '700', color: '#111827' }}>24/7</div>
                      <div style={{ fontSize: '11px', color: '#374151', fontWeight: '500' }}>Support</div>
                    </div>
                  </div>
                </td>
              </tr>
            </table>
          </div>

          {/* Mobile view - Single column */}
          <div style={{ display: 'block' }}>
            <div style={{ maxWidth: '280px', margin: '0 auto' }}>
              <div style={{ marginBottom: '8px' }}>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    backgroundColor: '#ffffff',
                    borderRadius: '8px',
                    padding: '12px 16px',
                    boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                  }}
                >
                  <span style={{ fontSize: '18px', marginRight: '12px' }}>🎯</span>
                  <div style={{ flex: '1' }}>
                    <div style={{ fontSize: '14px', fontWeight: '700', color: '#111827' }}>99% Success Rate</div>
                    <div style={{ fontSize: '12px', color: '#6b7280', marginTop: '2px' }}>Reliable bookings</div>
                  </div>
                </div>
              </div>

              <div style={{ marginBottom: '8px' }}>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    backgroundColor: '#ffffff',
                    borderRadius: '8px',
                    padding: '12px 16px',
                    boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                  }}
                >
                  <span style={{ fontSize: '18px', marginRight: '12px' }}>📧</span>
                  <div style={{ flex: '1' }}>
                    <div style={{ fontSize: '14px', fontWeight: '700', color: '#111827' }}>Instant Updates</div>
                    <div style={{ fontSize: '12px', color: '#6b7280', marginTop: '2px' }}>Real-time notifications</div>
                  </div>
                </div>
              </div>

              <div style={{ marginBottom: '8px' }}>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    backgroundColor: '#ffffff',
                    borderRadius: '8px',
                    padding: '12px 16px',
                    boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                  }}
                >
                  <span style={{ fontSize: '18px', marginRight: '12px' }}>💰</span>
                  <div style={{ flex: '1' }}>
                    <div style={{ fontSize: '14px', fontWeight: '700', color: '#111827' }}>Money Back Guarantee</div>
                    <div style={{ fontSize: '12px', color: '#6b7280', marginTop: '2px' }}>Full refund protection</div>
                  </div>
                </div>
              </div>

              <div>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    backgroundColor: '#ffffff',
                    borderRadius: '8px',
                    padding: '12px 16px',
                    boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                  }}
                >
                  <span style={{ fontSize: '18px', marginRight: '12px' }}>🕐</span>
                  <div style={{ flex: '1' }}>
                    <div style={{ fontSize: '14px', fontWeight: '700', color: '#111827' }}>24/7 Support</div>
                    <div style={{ fontSize: '12px', color: '#6b7280', marginTop: '2px' }}>Always here to help</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </Column>
      </Row>
    </Section>
  );
};
