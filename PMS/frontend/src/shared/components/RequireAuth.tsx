import { Navigate, useLocation } from 'react-router-dom';
import { useSession } from '../../features/auth/useSession';
import { isProblemDetailsError } from '../api/problemDetails';

export interface RequireAuthProps {
  children: React.ReactNode;
}

/**
 * Route guard for every authenticated screen (planning-pms-verification.md, F-2 point 4).
 *
 * The server is the actual gate - every `/api` route except health, `auth/login` and
 * `auth/reauth` returns 401 without the cookie, so this component cannot expose data by being
 * wrong. What it does is decide what the physician *sees*: a login screen rather than a page
 * of empty panels and error toasts.
 */
export function RequireAuth({ children }: RequireAuthProps) {
  const location = useLocation();
  const { data: session, isPending, isError, error } = useSession();

  if (isPending) {
    // Neither the app nor a redirect: rendering the app here would flash PHI-shaped chrome
    // before we know there is a session, and redirecting would bounce a signed-in physician to
    // /login on every hard refresh.
    return (
      <p className="app-status" role="status">
        Checking your session...
      </p>
    );
  }

  if (isError) {
    // "The server is unreachable" is not "you are signed out". Saying the second would send the
    // physician to a login screen that cannot possibly work and hide the real problem.
    return (
      <div className="app-status app-status--error" role="alert">
        <p>
          {isProblemDetailsError(error)
            ? error.userMessage
            : 'Could not check your session. Check the connection and try again.'}
        </p>
      </div>
    );
  }

  if (!session) {
    // `state.from` lets F-3 and later features send the physician back where they were headed
    // once they have signed in.
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  return <>{children}</>;
}
