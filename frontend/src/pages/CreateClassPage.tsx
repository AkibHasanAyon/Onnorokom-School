import { messages } from '../i18n/messages';

/** Route shell; the screen itself is built in Phases 10–11 (Design.md §4). */
export function CreateClassPage() {
  return <h1>{messages.pages.createClass}</h1>;
}
