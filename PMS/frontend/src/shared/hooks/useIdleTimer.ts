import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Activity that counts as "the physician is still here". Deliberately includes keyboard and
 * touch, not just the mouse: typing notes with the keyboard alone must not look idle.
 */
const ACTIVITY_EVENTS = [
  'mousemove',
  'mousedown',
  'keydown',
  'wheel',
  'touchstart',
  'scroll',
] as const;

export interface UseIdleTimerOptions {
  /** Milliseconds of inactivity before {@link UseIdleTimerResult.isIdle} turns true. */
  idleMs: number;
  /** When false the timer is disabled and never reports idle (used while signed out). */
  enabled?: boolean;
  /** Called once, on the transition into idle. */
  onIdle?: () => void;
}

export interface UseIdleTimerResult {
  /** True once `idleMs` has passed with no activity. */
  isIdle: boolean;
  /** Clears the idle state and restarts the countdown. */
  reset: () => void;
}

/**
 * Reports when the user has stopped interacting with the page.
 *
 * **This hook only ever reports a fact; it never destroys anything.** That separation is the
 * whole mitigation behind E-62 and E-41: the screen lock is a display state driven by this
 * timer, so covering the screen cannot unmount a route, cancel a request or discard a typed
 * draft. If idleness were wired to sign-out instead, a doctor stepping out for five minutes
 * would come back to a lost consultation.
 */
export function useIdleTimer({
  idleMs,
  enabled = true,
  onIdle,
}: UseIdleTimerOptions): UseIdleTimerResult {
  const [isIdle, setIsIdle] = useState(false);

  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Mirrors `isIdle` for the event listeners, which are registered once and would otherwise
  // close over a stale value.
  const isIdleRef = useRef(false);

  // Held in a ref so a caller passing an inline arrow function does not re-subscribe on every
  // render - which would restart the countdown each render and mean it never fired.
  const onIdleRef = useRef(onIdle);
  useEffect(() => {
    onIdleRef.current = onIdle;
  }, [onIdle]);

  const clear = useCallback(() => {
    if (timeoutRef.current !== null) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  const markIdle = useCallback((value: boolean) => {
    isIdleRef.current = value;
    setIsIdle(value);
  }, []);

  const start = useCallback(() => {
    clear();
    timeoutRef.current = setTimeout(() => {
      timeoutRef.current = null;
      markIdle(true);
      onIdleRef.current?.();
    }, idleMs);
  }, [clear, idleMs, markIdle]);

  const reset = useCallback(() => {
    markIdle(false);
    start();
  }, [markIdle, start]);

  useEffect(() => {
    if (!enabled) {
      clear();
      markIdle(false);
      return;
    }

    start();

    const handleActivity = () => {
      // Once locked, activity must NOT unlock the screen - otherwise a passer-by nudging the
      // mouse would reveal the record on display (E-62). Only re-authentication clears it,
      // by calling `reset`.
      if (isIdleRef.current) {
        return;
      }
      start();
    };

    const options: AddEventListenerOptions = { passive: true };
    ACTIVITY_EVENTS.forEach((event) => window.addEventListener(event, handleActivity, options));

    // A tab returning to the foreground counts as activity. A tab going to the background does
    // not, and must not stop the countdown - a minimised browser is the classic unattended
    // screen.
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        handleActivity();
      }
    };
    document.addEventListener('visibilitychange', handleVisibility);

    return () => {
      ACTIVITY_EVENTS.forEach((event) =>
        window.removeEventListener(event, handleActivity, options),
      );
      document.removeEventListener('visibilitychange', handleVisibility);
      clear();
    };
  }, [enabled, start, clear, markIdle]);

  return { isIdle, reset };
}
