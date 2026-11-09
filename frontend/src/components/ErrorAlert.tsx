import type { ReactNode } from 'react';
import { errorMessage, messages } from '../i18n/messages';

interface Props {
  error: unknown;
  /** Shows a Retry button (e.g. when loading failed). */
  onRetry?: () => void;
  /** Extra action next to the message, e.g. a link to the existing application. */
  action?: ReactNode;
}

/** A friendly, announced error message (R12). */
export function ErrorAlert({ error, onRetry, action }: Props) {
  return (
    <div className="alert alert-error" role="alert">
      <p>{errorMessage(error)}</p>
      {(onRetry || action) && (
        <div className="actions">
          {action}
          {onRetry && (
            <button type="button" className="secondary" onClick={onRetry}>
              {messages.classList.retry}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
