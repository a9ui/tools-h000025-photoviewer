import React from 'react';

interface ScanErrorNoticeProps {
  message: string;
  canRetry: boolean;
  onRetry: () => void;
  onDismiss: () => void;
}

export function ScanErrorNotice({ message, canRetry, onRetry, onDismiss }: ScanErrorNoticeProps) {
  return (
    <div className="landing-error scan-error-notice" role="alert">
      <span>Scan error: {message}</span>
      <div className="scan-error-actions">
        <button
          className="sidebar-link"
          type="button"
          onClick={onRetry}
          disabled={!canRetry}
          aria-label={canRetry
            ? 'Retry scan with the current folder set'
            : 'Retry scan is unavailable because no folder set is selected'}
        >
          Retry scan
        </button>
        <button className="sidebar-link" type="button" onClick={onDismiss}>
          Dismiss
        </button>
      </div>
    </div>
  );
}
