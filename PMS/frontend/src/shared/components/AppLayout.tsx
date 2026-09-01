import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useLogout, useSession } from '../../features/auth/useSession';
import { ScreenLock } from './ScreenLock';

interface NavItem {
  to: string;
  label: string;
  end?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Today', end: true },
  { to: '/patients', label: 'Patients' },
  { to: '/settings/clinic', label: 'Clinic settings' },
  { to: '/export', label: 'Export' },
  { to: '/audit', label: 'Audit log' },
];

/**
 * The chrome every authenticated screen renders inside. F-7 mounts the global patient search
 * in the header. F-2 adds the sign-out control and wraps the whole layout in the idle
 * {@link ScreenLock}.
 *
 * The lock wraps the layout rather than sitting inside `<main>` on purpose: the navigation is
 * covered too, and - more importantly - the wrapped tree is never unmounted when the lock
 * engages, so a consultation in progress survives it (E-41, E-62).
 */
export function AppLayout() {
  const navigate = useNavigate();
  const { data: session } = useSession();
  const logout = useLogout();

  const handleSignOut = async () => {
    try {
      await logout.mutateAsync();
    } catch {
      // Deliberately ignored, see below.
    } finally {
      // Navigate either way. If the call failed the cookie may still be live, but leaving the
      // physician on a signed-in screen after they asked to sign out is the worse outcome, and
      // the next API call will 401 them out regardless.
      navigate('/login', { replace: true });
    }
  };

  return (
    <ScreenLock userName={session?.userName ?? ''} enabled={Boolean(session)}>
      <div className="app-layout">
        <header className="app-layout__header">
          <span className="app-layout__brand">Clinic</span>
          <nav className="app-layout__nav" aria-label="Main">
            {NAV_ITEMS.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  isActive ? 'app-layout__link app-layout__link--active' : 'app-layout__link'
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="app-layout__session">
            {session ? <span className="app-layout__user">{session.userName}</span> : null}
            <button
              className="button button--quiet"
              type="button"
              onClick={handleSignOut}
              disabled={logout.isPending}
            >
              {logout.isPending ? 'Signing out...' : 'Sign out'}
            </button>
          </div>
        </header>

        <main className="app-layout__main">
          <Outlet />
        </main>
      </div>
    </ScreenLock>
  );
}
