import { Navigate } from 'react-router-dom';
import { useSession } from '../../features/auth/useSession';

export interface RequireSetupProps {
  children: React.ReactNode;
}

/**
 * The first-run setup gate (planning-pms-verification.md, F-3 point 4: "a router-level guard in
 * routes.tsx redirects to /setup whenever `session.setupComplete === false`").
 *
 * **E-1.** A prescription printed with no clinic name, doctor name or registration number is not
 * a weak document - a pharmacy will refuse it. The server holds the real gate (the prescription
 * endpoints return 409 while setup is incomplete), and this component is the half that means the
 * physician never reaches that 409: they are sent to /setup before they can start a consultation
 * they would not be able to finish.
 *
 * Composed *inside* `RequireAuth`, never instead of it. `setupComplete` is a property of the
 * session, so there is no answer to give a signed-out visitor, and asking this question first
 * would mean a redirect decided from stale or absent data.
 */
export function RequireSetup({ children }: RequireSetupProps) {
  const { data: session, isPending } = useSession();

  if (isPending) {
    // RequireAuth has already rendered its own status for this state; rendering nothing here
    // avoids two "checking..." lines stacked on top of each other.
    return null;
  }

  if (session && !session.setupComplete) {
    // `replace`, so Back does not bounce the physician between / and /setup.
    return <Navigate to="/setup" replace />;
  }

  return <>{children}</>;
}
