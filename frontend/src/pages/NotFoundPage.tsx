import { Link } from 'react-router';
import { messages } from '../i18n/messages';

export function NotFoundPage() {
  const t = messages.notFound;
  return (
    <>
      <h1>{t.title}</h1>
      <p>{t.body}</p>
      <Link to="/">{t.back}</Link>
    </>
  );
}
