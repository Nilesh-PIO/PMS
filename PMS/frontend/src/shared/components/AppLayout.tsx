import { NavLink, Outlet } from 'react-router-dom';

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
 * in the header; F-2 mounts the screen-lock overlay and the sign-out control. F-1 provides
 * the frame and the navigation only.
 */
export function AppLayout() {
  return (
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
      </header>

      <main className="app-layout__main">
        <Outlet />
      </main>
    </div>
  );
}
