import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderResult } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { vi } from 'vitest';
import type { Session } from '../features/auth/types/session';
import {
  TemperatureUnit,
  type ClinicProfile,
} from '../features/clinic/types/clinicProfile';

/**
 * Shared test helpers. Not a test file itself - the vitest `include` pattern only picks up
 * `*.test.*` and `*.spec.*`.
 */

/**
 * A plausible signed-in session, 12 hours out.
 *
 * `setupComplete` defaults to **true** from F-3 onward: the ordinary state of a working clinic is
 * one whose profile has been saved, and every screen from F-5 on is only reachable in that state.
 * A test that cares about first-run setup passes `{ setupComplete: false }` explicitly, which
 * makes the unusual case the visible one.
 */
export function aSession(overrides: Partial<Session> = {}): Session {
  return {
    userName: 'doctor',
    expiresUtc: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString(),
    setupComplete: true,
    ...overrides,
  };
}

/** A saved clinic profile, as the API returns it (F-3). */
export function aClinicProfile(overrides: Partial<ClinicProfile> = {}): ClinicProfile {
  return {
    clinicName: 'Sunrise Clinic',
    addressLines: '12 Station Road\nPune 411001',
    doctorName: 'Dr A. Mehta',
    doctorRegistrationNo: 'MMC-99215',
    prescriptionFooter: 'Please bring this prescription to your next visit.',
    temperatureUnit: TemperatureUnit.Celsius,
    signatureImageDataUrl: null,
    isSetupComplete: true,
    updatedUtc: new Date().toISOString(),
    ...overrides,
  };
}

/** A query client with retries off, so a test asserts behaviour rather than waiting for backoff. */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 60_000 },
      mutations: { retry: false },
    },
  });
}

export interface RenderOptions {
  client?: QueryClient;
  route?: string;
}

/** Renders a component inside the providers every screen in this app has around it. */
export function renderWithProviders(
  ui: ReactNode,
  { client = createTestQueryClient(), route = '/' }: RenderOptions = {},
): RenderResult & { client: QueryClient } {
  const result = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );

  return Object.assign(result, { client });
}

/** Builds a JSON `Response`, so tests stub `fetch` rather than the app's own modules. */
export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

/** Builds an RFC-7807 error `Response`. */
export function problemResponse(status: number, body: Record<string, unknown> = {}): Response {
  return new Response(JSON.stringify({ status, title: 'Request failed.', ...body }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

/** A 204, which is what logout returns. */
export function noContentResponse(): Response {
  return new Response(null, { status: 204 });
}

/**
 * Replaces `globalThis.fetch` with a router keyed by path suffix, so a test states what the
 * server says and nothing else is mocked - `httpClient` and the feature API module both run
 * for real.
 */
export function stubFetch(
  routes: Record<string, (init?: RequestInit) => Response | Promise<Response>>,
) {
  const calls: { url: string; init?: RequestInit }[] = [];

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input.toString();
    calls.push({ url, init });

    const match = Object.keys(routes).find((path) => url.endsWith(path));
    if (!match) {
      throw new Error(`No stub for ${url}`);
    }
    // `init` is handed to the route so a stub can answer a GET and a PUT on the same path
    // differently - which is what a save-then-reload flow actually does.
    return routes[match](init);
  });

  vi.stubGlobal('fetch', fetchMock);
  return { calls, fetchMock };
}
