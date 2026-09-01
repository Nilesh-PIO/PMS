import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LoginPage } from './LoginPage';
import { SESSION_QUERY_KEY } from './useSession';
import {
  aSession,
  createTestQueryClient,
  jsonResponse,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';

describe('LoginPage', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  function renderSignedOut(routes: Parameters<typeof stubFetch>[0]) {
    const stub = stubFetch({ '/api/auth/session': () => problemResponse(401), ...routes });
    const client = createTestQueryClient();
    // Signed out, known: skips the "checking your session" state without another round trip.
    client.setQueryData(SESSION_QUERY_KEY, null);
    return { ...renderWithProviders(<LoginPage />, { client, route: '/login' }), ...stub };
  }

  it('renders a user name and password field and a submit button', () => {
    renderSignedOut({});

    expect(screen.getByRole('heading', { level: 1, name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByLabelText('User name')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('renders the password field as a password input', () => {
    renderSignedOut({});

    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  // --- E-65: no browser autofill on the shared clinic machine -------------

  it('disables autocomplete on the form and on every field (E-65)', () => {
    const { container } = renderSignedOut({});

    const form = container.querySelector('form');
    expect(form).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByLabelText('User name')).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByLabelText('Password')).toHaveAttribute('autocomplete', 'off');
  });

  // --- the golden path ----------------------------------------------------

  it('posts the credentials to /api/auth/login and caches the session', async () => {
    const user = userEvent.setup();
    const { calls, client } = renderSignedOut({
      '/api/auth/login': () => jsonResponse(aSession()),
    });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.type(screen.getByLabelText('Password'), 'SeedDoctor#2026!');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => {
      expect(client.getQueryData(SESSION_QUERY_KEY)).toMatchObject({ userName: 'doctor' });
    });

    const loginCall = calls.find((c) => c.url.endsWith('/api/auth/login'));
    expect(loginCall).toBeDefined();
    expect(JSON.parse(loginCall!.init!.body as string)).toEqual({
      userName: 'doctor',
      password: 'SeedDoctor#2026!',
    });
  });

  it('sends the cookie-bearing request same-origin and never asks for a token', async () => {
    const user = userEvent.setup();
    const { calls } = renderSignedOut({
      '/api/auth/login': () => jsonResponse(aSession()),
    });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.type(screen.getByLabelText('Password'), 'SeedDoctor#2026!');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => {
      expect(calls.some((c) => c.url.endsWith('/api/auth/login'))).toBe(true);
    });

    const loginCall = calls.find((c) => c.url.endsWith('/api/auth/login'))!;
    expect(loginCall.init!.credentials).toBe('same-origin');
  });

  it('never writes anything to localStorage or sessionStorage', async () => {
    const user = userEvent.setup();
    renderSignedOut({ '/api/auth/login': () => jsonResponse(aSession()) });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.type(screen.getByLabelText('Password'), 'SeedDoctor#2026!');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(window.localStorage.length).toBe(0));
    expect(window.sessionStorage.length).toBe(0);
  });

  // --- failures -----------------------------------------------------------

  it('shows one message for a 401 and never says which half was wrong', async () => {
    const user = userEvent.setup();
    renderSignedOut({ '/api/auth/login': () => problemResponse(401) });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.type(screen.getByLabelText('Password'), 'wrong-password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('That user name and password were not recognised.');
    expect(alert.textContent?.toLowerCase()).not.toContain('user name is');
    expect(alert.textContent?.toLowerCase()).not.toContain('no such user');
  });

  it('keeps the typed user name on screen after a failed attempt', async () => {
    const user = userEvent.setup();
    renderSignedOut({ '/api/auth/login': () => problemResponse(401) });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.type(screen.getByLabelText('Password'), 'wrong-password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await screen.findByRole('alert');
    expect(screen.getByLabelText('User name')).toHaveValue('doctor');
  });

  it('renders 400 field errors beside the field they belong to', async () => {
    const user = userEvent.setup();
    renderSignedOut({
      '/api/auth/login': () =>
        problemResponse(400, { errors: { Password: ['Enter your password.'] } }),
    });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter your password.')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toHaveAttribute('aria-invalid', 'true');
  });

  it('reports a transport failure as a reachability problem, not a wrong password', async () => {
    const user = userEvent.setup();
    renderSignedOut({
      '/api/auth/login': () => {
        throw new TypeError('Failed to fetch');
      },
    });

    await user.type(screen.getByLabelText('User name'), 'doctor');
    await user.type(screen.getByLabelText('Password'), 'SeedDoctor#2026!');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toMatch(/could not reach the server/i);
  });

  // --- already signed in --------------------------------------------------

  it('does not show the form to someone who already has a session', () => {
    stubFetch({ '/api/auth/session': () => jsonResponse(aSession()) });
    const client = createTestQueryClient();
    client.setQueryData(SESSION_QUERY_KEY, aSession());

    renderWithProviders(<LoginPage />, { client, route: '/login' });

    expect(screen.queryByRole('button', { name: 'Sign in' })).toBeNull();
  });
});
