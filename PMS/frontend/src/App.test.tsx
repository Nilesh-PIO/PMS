import { QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from './App';
import { REGISTERED_PATHS, routes } from './routes';

function renderAt(path: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<App client={client} initialEntries={[path]} />);
}

describe('app shell routing', () => {
  it('renders the layout chrome on an authenticated route', () => {
    renderAt('/');

    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Patients' })).toBeInTheDocument();
  });

  it.each([
    ['/', 'Today'],
    ['/patients', 'Patients'],
    ['/patients/abc-123', 'Patient profile'],
    ['/visits/abc-123', 'Consultation'],
    ['/settings/clinic', 'Clinic profile'],
    ['/export', 'Export'],
    ['/audit', 'Audit log'],
  ])('registers %s and renders its placeholder', (path, heading) => {
    renderAt(path);

    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });

  it('renders /login without the app navigation chrome', () => {
    renderAt('/login');

    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
  });

  it('renders /setup without the app navigation chrome', () => {
    renderAt('/setup');

    expect(screen.getByRole('heading', { level: 1, name: 'First-run setup' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
  });

  it('shows a not-found page instead of a blank screen for an unknown route', () => {
    renderAt('/nothing-here');

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
  });

  it('declares every path the F-1 plan names', () => {
    const declared = new Set<string>();
    const walk = (list: typeof routes, prefix = '') => {
      for (const route of list) {
        const path = route.index
          ? prefix || '/'
          : `${prefix === '/' ? '' : prefix}/${route.path ?? ''}`.replace(/\/+/g, '/');
        if (route.path?.startsWith('/')) {
          declared.add(route.path);
        } else if (route.path || route.index) {
          declared.add(path.replace(/\/$/, '') || '/');
        }
        if (route.children) {
          walk(route.children, route.path?.startsWith('/') ? route.path : path);
        }
      }
    };
    walk(routes);

    for (const path of REGISTERED_PATHS) {
      expect(declared).toContain(path);
    }
  });
});
