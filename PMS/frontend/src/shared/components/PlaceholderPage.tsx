import { EmptyState } from './EmptyState';

export interface PlaceholderPageProps {
  /** The screen this route will become. */
  title: string;
  /** The plan's Feature ID that will replace this placeholder. */
  featureId: string;
}

/**
 * Stands in for a route that F-1 registers but does not yet implement.
 *
 * These are deliberately not stub files inside the feature folders: an empty
 * `PatientProfile.tsx` would look like an implemented component to the next reader. Naming
 * the owning Feature ID on screen makes the gap explicit instead.
 */
export function PlaceholderPage({ title, featureId }: PlaceholderPageProps) {
  return (
    <section className="placeholder-page">
      <h1>{title}</h1>
      <EmptyState
        title="Not built yet"
        description={
          <>
            This screen is delivered by feature <strong>{featureId}</strong>.
          </>
        }
      />
    </section>
  );
}
