import React from 'react';

interface ScanErrorNoticeProps {
  message: string;
  canRetry: boolean;
  onRetry: () => void;
  onDismiss: () => void;
  subject?: 'scan' | 'search';
}

export function ScanErrorNotice({
  message,
  canRetry,
  onRetry,
  onDismiss,
  subject = 'scan',
}: ScanErrorNoticeProps) {
  const isSearch = subject === 'search';
  const label = isSearch ? 'Search' : 'Scan';
  const retryLabel = isSearch ? 'Retry search' : 'Retry scan';
  const retryAriaLabel = isSearch
    ? 'Retry the current search'
    : 'Retry scan with the current folder set';
  const disabledRetryAriaLabel = isSearch
    ? 'Retry search is unavailable'
    : 'Retry scan is unavailable because no folder set is selected';

  return (
    <div className="landing-error scan-error-notice" role="alert">
      <span>{label} error: {message}</span>
      <div className="scan-error-actions">
        <button
          className="sidebar-link"
          type="button"
          onClick={onRetry}
          disabled={!canRetry}
          aria-label={canRetry
            ? retryAriaLabel
            : disabledRetryAriaLabel}
        >
          {retryLabel}
        </button>
        <button className="sidebar-link" type="button" onClick={onDismiss}>
          Dismiss
        </button>
      </div>
    </div>
  );
}
