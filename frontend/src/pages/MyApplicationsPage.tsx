import { messages } from '../i18n/messages';

/** Route shell; the screen itself is built in Phases 10–11 (Design.md §4). */
export function MyApplicationsPage() {
  return <h1>{messages.pages.myApplications}</h1>;
}
