import { useNow } from '../hooks/useNow';
import { messages } from '../i18n/messages';

interface Props {
  /** When the data on screen was last fetched successfully (ms since epoch; 0 = never). */
  updatedAt: number;
  /** The latest refresh failed, so what's shown may be out of date. */
  stale: boolean;
}

/** "Updated 5 s ago", or a warning that the data shown may be out of date (Design.md §1: show when data is stale). */
export function FreshnessNote({ updatedAt, stale }: Props) {
  const now = useNow(1_000);
  if (updatedAt <= 0) return null;
  const t = messages.freshness;
  const seconds = Math.max(0, Math.floor((now - updatedAt) / 1000));
  if (stale) {
    return <span className="stale-note">{t.stale(seconds)}</span>;
  }
  return <span>{seconds < 2 ? t.justNow : t.ago(seconds)}</span>;
}
