import { Component, type ErrorInfo, type ReactNode } from 'react';
import { messages } from '../i18n/messages';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/**
 * Catches render crashes so the parent never sees a blank page (R12). Uses plain links (not router Links)
 * so it also works if the crash happened outside the router. Wrap it with `key={pathname}` to reset on navigation.
 */
export class ErrorBoundary extends Component<Props, State> {
  override state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    // Shown to the user as friendly text; details kept for developers.
    console.error('Render error caught by ErrorBoundary', error, info.componentStack);
  }

  override render(): ReactNode {
    if (!this.state.error) return this.props.children;

    const t = messages.errorBoundary;
    return (
      <section className="alert alert-error" role="alert">
        <h2>{t.title}</h2>
        <p>{t.body}</p>
        <div className="actions">
          <button type="button" onClick={() => window.location.reload()}>
            {t.reload}
          </button>
          <a className="button secondary" href="/">
            {t.home}
          </a>
        </div>
      </section>
    );
  }
}
