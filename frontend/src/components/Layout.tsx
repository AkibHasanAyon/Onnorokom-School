import { Link, NavLink, Outlet, useLocation } from 'react-router';
import { messages } from '../i18n/messages';
import { StudentIdBar } from '../student/StudentIdBar';
import { ConnectionBanner } from './ConnectionBanner';
import { ErrorBoundary } from './ErrorBoundary';

/** Header (title, student ID bar, navigation) + the current page. */
export function Layout() {
  const { pathname } = useLocation();
  const t = messages.nav;

  return (
    <>
      <header className="app-header">
        <div className="container">
          <p className="app-title">
            <Link to="/">🏫 {messages.app.title}</Link>
          </p>
          <StudentIdBar />
          <nav className="app-nav" aria-label={t.label}>
            <ul>
              <li>
                <NavLink to="/" end>
                  {t.classes}
                </NavLink>
              </li>
              <li>
                <NavLink to="/my-applications">{t.myApplications}</NavLink>
              </li>
              <li>
                <NavLink to="/admin/classes/new">{t.createClass}</NavLink>
              </li>
            </ul>
          </nav>
        </div>
      </header>
      <ConnectionBanner />
      <main className="container">
        {/* Keyed by path: a crash on one page doesn't stick after navigating elsewhere. */}
        <ErrorBoundary key={pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </>
  );
}
