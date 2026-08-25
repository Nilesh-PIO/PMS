import type { ReactNode } from 'react';

export interface EmptyStateProps {
  /** What is empty, in the doctor's words. */
  title: string;
  /** Why it is empty and what to do next. */
  description?: ReactNode;
  /** The one obvious next action, when there is one. */
  action?: ReactNode;
}

/**
 * A list with nothing in it must say so. A blank panel is indistinguishable from a failed
 * load, and on a fresh install every list is empty (E-2) - so this is the default rendering
 * for "no rows", not a nicety.
 */
export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="empty-state" role="status">
      <p className="empty-state__title">{title}</p>
      {description ? <p className="empty-state__description">{description}</p> : null}
      {action ? <div className="empty-state__action">{action}</div> : null}
    </div>
  );
}
