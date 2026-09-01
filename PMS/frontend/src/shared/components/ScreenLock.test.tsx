import { act, fireEvent, screen } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ScreenLock } from './ScreenLock';
import {
  aSession,
  jsonResponse,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';

/**
 * A stand-in for the consultation page F-10 will build: one uncontrolled textarea holding
 * unsaved typing, and one piece of component state. Both are the things E-41 says must survive
 * an idle lock and a re-authentication.
 */
function DraftConsultation() {
  const [clicks, setClicks] = useState(0);

  return (
    <div>
      <h1>Consultation</h1>
      <label htmlFor="notes">Consultation notes</label>
      <textarea id="notes" defaultValue="" />
      <button type="button" onClick={() => setClicks((c) => c + 1)}>
        counter
      </button>
      <span data-testid="clicks">{clicks}</span>
    </div>
  );
}

const IDLE_MS = 1000;

/**
 * Fake timers are used so the idle threshold is crossed deterministically rather than by
 * sleeping. `fireEvent` rather than `userEvent` for the same reason: userEvent's
 * inter-keystroke delay never resolves against a frozen clock. What is being asserted here is
 * what survives a lock, not the fidelity of keystroke simulation - `useIdleTimer.test.ts`
 * covers the timing behaviour on its own.
 */
describe('ScreenLock', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  const goIdle = () => act(() => void vi.advanceTimersByTime(IDLE_MS));

  /**
   * Lets pending promise callbacks (the reauth mutation, and the extra body-reading hop the
   * error path takes through `parseProblemDetails`) run while the clock is frozen.
   */
  const flush = async () => {
    for (let i = 0; i < 10; i += 1) {
      await act(async () => {
        await Promise.resolve();
        vi.advanceTimersByTime(0);
      });
    }
  };

  const typeInto = (element: HTMLElement, value: string) =>
    act(() => void fireEvent.change(element, { target: { value } }));

  const click = (element: HTMLElement) => act(() => void fireEvent.click(element));

  const submitUnlock = async (password: string) => {
    typeInto(screen.getByLabelText('Password'), password);
    click(screen.getByRole('button', { name: 'Unlock' }));
    await flush();
  };

  function renderLock(routes: Parameters<typeof stubFetch>[0] = {}) {
    const stub = stubFetch(routes);
    const rendered = renderWithProviders(
      <ScreenLock userName="doctor" idleMs={IDLE_MS}>
        <DraftConsultation />
      </ScreenLock>,
    );
    return { ...rendered, ...stub };
  }

  it('shows no overlay while the physician is working', () => {
    renderLock();

    expect(screen.queryByTestId('screen-lock-overlay')).toBeNull();
    expect(screen.getByRole('heading', { name: 'Consultation' })).toBeInTheDocument();
  });

  // --- E-62: the overlay covers the record on display ---------------------

  it('covers the screen once the idle timeout passes (E-62)', () => {
    renderLock();

    goIdle();

    expect(screen.getByTestId('screen-lock-overlay')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
    expect(screen.getByRole('heading', { name: 'Screen locked' })).toBeInTheDocument();
  });

  it('blurs and hides the content beneath from assistive technology', () => {
    renderLock();

    goIdle();

    const content = screen.getByTestId('screen-lock-content');
    expect(content).toHaveAttribute('aria-hidden', 'true');
    expect(content.className).toContain('screen-lock__app--obscured');
  });

  it('does not unlock because someone brushed the mouse', () => {
    renderLock();
    goIdle();

    act(() => {
      window.dispatchEvent(new Event('mousemove'));
      window.dispatchEvent(new Event('keydown'));
    });

    expect(screen.getByTestId('screen-lock-overlay')).toBeInTheDocument();
  });

  // --- E-41: the draft underneath is never discarded ----------------------

  it('keeps the underlying route mounted while locked (E-41, E-62)', () => {
    renderLock();

    typeInto(screen.getByLabelText('Consultation notes'), 'Patient reports headache since Tuesday');

    goIdle();

    // Still in the document, still carrying every character - it was covered, not unmounted.
    expect(screen.getByTestId('screen-lock-overlay')).toBeInTheDocument();
    expect(screen.getByLabelText('Consultation notes')).toHaveValue(
      'Patient reports headache since Tuesday',
    );
  });

  it('restores the exact view and every typed character after re-authentication (E-41)', async () => {
    const { calls } = renderLock({ '/api/auth/reauth': () => jsonResponse(aSession()) });

    typeInto(screen.getByLabelText('Consultation notes'), 'BP 130/85, review in two weeks');
    click(screen.getByRole('button', { name: 'counter' }));

    // Capture the actual DOM node: if it is the same object afterwards, React never unmounted
    // and remounted the subtree, which is the strongest form this assertion can take.
    const nodeBeforeLock = screen.getByLabelText('Consultation notes');

    goIdle();
    expect(screen.getByTestId('screen-lock-overlay')).toBeInTheDocument();

    await submitUnlock('SeedDoctor#2026!');

    expect(screen.queryByTestId('screen-lock-overlay')).toBeNull();

    const nodeAfterUnlock = screen.getByLabelText('Consultation notes');
    expect(nodeAfterUnlock).toBe(nodeBeforeLock);
    expect(nodeAfterUnlock).toHaveValue('BP 130/85, review in two weeks');
    expect(screen.getByTestId('clicks')).toHaveTextContent('1');

    expect(calls.some((c) => c.url.endsWith('/api/auth/reauth'))).toBe(true);
  });

  it('re-authenticates in place and never navigates away', async () => {
    const { calls } = renderLock({ '/api/auth/reauth': () => jsonResponse(aSession()) });

    goIdle();
    await submitUnlock('SeedDoctor#2026!');

    expect(screen.queryByTestId('screen-lock-overlay')).toBeNull();

    // /api/auth/login would mean the client had gone through the login page, which unmounts
    // the consultation. The lock must use reauth, and only reauth.
    expect(calls.some((c) => c.url.endsWith('/api/auth/login'))).toBe(false);
    expect(screen.getByRole('heading', { name: 'Consultation' })).toBeInTheDocument();
  });

  it('sends the user name from the session, not one typed at the lock screen', async () => {
    const { calls } = renderLock({ '/api/auth/reauth': () => jsonResponse(aSession()) });

    goIdle();
    await submitUnlock('SeedDoctor#2026!');

    const reauthCall = calls.find((c) => c.url.endsWith('/api/auth/reauth'))!;
    expect(JSON.parse(reauthCall.init!.body as string)).toEqual({
      userName: 'doctor',
      password: 'SeedDoctor#2026!',
    });
  });

  it('keeps the draft when the unlock password is wrong', async () => {
    renderLock({ '/api/auth/reauth': () => problemResponse(401) });

    typeInto(screen.getByLabelText('Consultation notes'), 'Do not lose this');
    goIdle();

    await submitUnlock('wrong');

    expect(screen.getByText(/not recognised/i)).toBeInTheDocument();
    expect(screen.getByTestId('screen-lock-overlay')).toBeInTheDocument();
    expect(screen.getByLabelText('Consultation notes')).toHaveValue('Do not lose this');
  });

  it('tells the physician nothing has been lost, on the overlay itself', () => {
    renderLock();

    goIdle();

    expect(screen.getByText(/nothing has been lost/i)).toBeInTheDocument();
  });

  it('locks again after the next idle period', async () => {
    renderLock({ '/api/auth/reauth': () => jsonResponse(aSession()) });

    goIdle();
    await submitUnlock('SeedDoctor#2026!');
    expect(screen.queryByTestId('screen-lock-overlay')).toBeNull();

    goIdle();

    expect(screen.getByTestId('screen-lock-overlay')).toBeInTheDocument();
  });

  it('never locks when disabled', () => {
    stubFetch({});
    renderWithProviders(
      <ScreenLock userName="doctor" idleMs={IDLE_MS} enabled={false}>
        <DraftConsultation />
      </ScreenLock>,
    );

    goIdle();

    expect(screen.queryByTestId('screen-lock-overlay')).toBeNull();
  });

  it('disables autocomplete on the unlock form (E-65)', () => {
    const { container } = renderLock();

    goIdle();

    const form = container.querySelector('.screen-lock__panel form');
    expect(form).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByLabelText('Password')).toHaveAttribute('autocomplete', 'off');
  });

  it('never puts anything in web storage', async () => {
    renderLock({ '/api/auth/reauth': () => jsonResponse(aSession()) });

    goIdle();
    await submitUnlock('SeedDoctor#2026!');

    expect(screen.queryByTestId('screen-lock-overlay')).toBeNull();
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });
});
