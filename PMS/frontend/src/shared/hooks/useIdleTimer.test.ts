import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IDLE_LOCK_MS } from '../config/sessionPolicy';
import { useIdleTimer } from './useIdleTimer';

describe('useIdleTimer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const advance = (ms: number) => act(() => void vi.advanceTimersByTime(ms));

  const fireActivity = (type = 'mousemove') =>
    act(() => void window.dispatchEvent(new Event(type)));

  it('does not report idle before the timeout', () => {
    const { result } = renderHook(() => useIdleTimer({ idleMs: 5000 }));

    advance(4999);

    expect(result.current.isIdle).toBe(false);
  });

  it('reports idle once the timeout passes', () => {
    const { result } = renderHook(() => useIdleTimer({ idleMs: 5000 }));

    advance(5000);

    expect(result.current.isIdle).toBe(true);
  });

  it('calls onIdle exactly once on the transition', () => {
    const onIdle = vi.fn();
    renderHook(() => useIdleTimer({ idleMs: 5000, onIdle }));

    advance(20_000);

    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it.each(['mousemove', 'mousedown', 'keydown', 'wheel', 'touchstart', 'scroll'])(
    'treats %s as activity and restarts the countdown',
    (eventType) => {
      const { result } = renderHook(() => useIdleTimer({ idleMs: 5000 }));

      advance(4000);
      fireActivity(eventType);
      advance(4000);

      expect(result.current.isIdle).toBe(false);
    },
  );

  it('counts keyboard-only work as activity', () => {
    // The BRD asks for keyboard-first entry (F-19). A physician typing a long consultation
    // note without touching the mouse must not be treated as absent.
    const { result } = renderHook(() => useIdleTimer({ idleMs: 5000 }));

    for (let i = 0; i < 10; i += 1) {
      advance(4000);
      fireActivity('keydown');
    }

    expect(result.current.isIdle).toBe(false);
  });

  it('stays idle when someone brushes the mouse after it has locked (E-62)', () => {
    // The whole point of the lock is that a passer-by cannot reveal the record on display.
    const { result } = renderHook(() => useIdleTimer({ idleMs: 5000 }));

    advance(5000);
    expect(result.current.isIdle).toBe(true);

    fireActivity('mousemove');
    fireActivity('keydown');

    expect(result.current.isIdle).toBe(true);
  });

  it('unlocks only through reset, and restarts the countdown from there', () => {
    const { result } = renderHook(() => useIdleTimer({ idleMs: 5000 }));

    advance(5000);
    expect(result.current.isIdle).toBe(true);

    act(() => result.current.reset());
    expect(result.current.isIdle).toBe(false);

    advance(4999);
    expect(result.current.isIdle).toBe(false);

    advance(1);
    expect(result.current.isIdle).toBe(true);
  });

  it('never reports idle while disabled', () => {
    const { result } = renderHook(() => useIdleTimer({ idleMs: 5000, enabled: false }));

    advance(60_000);

    expect(result.current.isIdle).toBe(false);
  });

  it('clears a pending lock when it is disabled mid-countdown', () => {
    const { result, rerender } = renderHook(
      ({ enabled }: { enabled: boolean }) => useIdleTimer({ idleMs: 5000, enabled }),
      { initialProps: { enabled: true } },
    );

    advance(5000);
    expect(result.current.isIdle).toBe(true);

    rerender({ enabled: false });

    expect(result.current.isIdle).toBe(false);
  });

  it('does not fire after unmount', () => {
    const onIdle = vi.fn();
    const { unmount } = renderHook(() => useIdleTimer({ idleMs: 5000, onIdle }));

    unmount();
    advance(60_000);

    expect(onIdle).not.toHaveBeenCalled();
  });

  it('does not restart the countdown when the onIdle callback identity changes', () => {
    // A caller passing an inline arrow gets a new function every render. If that resubscribed
    // the timer, the lock would be pushed back on every render and never fire.
    const { result, rerender } = renderHook(
      ({ onIdle }: { onIdle: () => void }) => useIdleTimer({ idleMs: 5000, onIdle }),
      { initialProps: { onIdle: () => {} } },
    );

    advance(4000);
    rerender({ onIdle: () => {} });
    advance(1000);

    expect(result.current.isIdle).toBe(true);
  });

  it('locks after the policy default of five minutes', () => {
    const { result } = renderHook(() => useIdleTimer({ idleMs: IDLE_LOCK_MS }));

    advance(IDLE_LOCK_MS - 1);
    expect(result.current.isIdle).toBe(false);

    advance(1);
    expect(result.current.isIdle).toBe(true);
    expect(IDLE_LOCK_MS).toBe(5 * 60 * 1000);
  });
});
