import React, { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Terminal Uncaught React Error:', error, errorInfo);
  }

  public render() {
    if (this.state.hasError) {
      return (
        <div
          style={{
            minHeight: '100vh',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: 'var(--bg-primary, #0b0e14)',
            color: 'var(--text-primary, #f0f3f8)',
            padding: '24px',
            fontFamily: 'Inter, system-ui, sans-serif',
          }}
        >
          <div
            style={{
              maxWidth: '560px',
              width: '100%',
              backgroundColor: 'var(--bg-secondary, #151b26)',
              border: '1px solid var(--border-color, #232d42)',
              borderRadius: '12px',
              padding: '32px',
              textAlign: 'center',
              boxShadow: '0 20px 40px rgba(0,0,0,0.4)',
            }}
          >
            <div
              style={{
                width: '56px',
                height: '56px',
                borderRadius: '50%',
                backgroundColor: 'rgba(246, 70, 93, 0.15)',
                color: '#f6465d',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 20px',
              }}
            >
              <AlertTriangle size={28} />
            </div>

            <h2 style={{ fontSize: '1.4rem', fontWeight: 700, marginBottom: '12px' }}>
              Terminal Encountered an Issue
            </h2>
            <p style={{ color: 'var(--text-secondary, #9aa5be)', fontSize: '0.9rem', marginBottom: '20px' }}>
              A rendering exception occurred while processing live market feeds. You can safely reload the terminal to re-establish the connection.
            </p>

            {this.state.error && (
              <pre
                style={{
                  backgroundColor: 'rgba(0,0,0,0.3)',
                  padding: '12px',
                  borderRadius: '6px',
                  fontSize: '0.75rem',
                  fontFamily: 'JetBrains Mono, monospace',
                  color: '#f6465d',
                  textAlign: 'left',
                  overflowX: 'auto',
                  marginBottom: '24px',
                }}
              >
                {this.state.error.message}
              </pre>
            )}

            <button
              onClick={() => window.location.reload()}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '10px 24px',
                backgroundColor: '#3b82f6',
                color: '#ffffff',
                border: 'none',
                borderRadius: '8px',
                fontWeight: 600,
                fontSize: '0.9rem',
                cursor: 'pointer',
              }}
            >
              <RefreshCw size={16} />
              Reload Terminal
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
