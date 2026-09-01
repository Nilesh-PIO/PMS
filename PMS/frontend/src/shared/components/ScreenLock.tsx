import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { useReauth } from '../../features/auth/useSession';
import { isProblemDetailsError } from '../api/problemDetails';
import { IDLE_LOCK_MS } from '../config/sessionPolicy';
import { useIdleTimer } from '../hooks/useIdleTimer';
import { TextField } from './forms/TextField';

export interface ScreenLockProps {
  /** The signed-in user name, shown on the overlay and submitted with the password. */
  userName: string;
  /** Overridable for tests; defaults to the 5-minute policy. */
  idleMs?: number;
  /** Disable the lock entirely (e.g. while signed out). */
  enabled?: boolean;
  /** The application, which stays mounted underneath the overlay at all times. */
  children: ReactNode;
}

/**
 * The idle screen lock (planning-pms-verification.md, F-2 points 4 and 5).
 *
 * **Two edge cases meet here, and the implementation only satisfies both if it does nothing
 * clever.**
 *
 * - **E-62** — a consulting-room screen left unattended between patients shows the previous
 *   patient's full history. So after {@link IDLE_LOCK_MS} an overlay covers everything and the
 *   content beneath it is blurred, not merely dimmed.
 * - **E-41** — a session that ends mid-consultation must never cost the typed text. So the
 *   overlay is a *sibling* of `children`, rendered on top. `children` is never unmounted,
 *   never re-keyed and never conditionally rendered: React keeps the same component instances
 *   and the same DOM nodes across lock and unlock, so an uncommitted draft in component state
 *   or in an uncontrolled input is still there afterwards, character for character.
 *
 * Re-authentication happens through `POST /api/auth/reauth` from inside this overlay, without
 * navigating. A redirect to `/login` would unmount the consultation, which is the loss E-41
 * describes - so the lock deliberately has no route of its own.
 */
export function ScreenLock({
  userName,
  idleMs = IDLE_LOCK_MS,
  enabled = true,
  children,
}: ScreenLockProps) {
  const [password, setPassword] = useState('');
  const passwordRef = useRef<HTMLInputElement>(null);
  const reauth = useReauth();

  const { isIdle, reset } = useIdleTimer({ idleMs, enabled });

  // Move focus onto the overlay when it appears, so a keystroke aimed at the consultation
  // beneath cannot land in a field the physician can no longer see.
  useEffect(() => {
    if (isIdle) {
      passwordRef.current?.focus();
    }
  }, [isIdle]);

  const locked = enabled && isIdle;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    try {
      await reauth.mutateAsync({ userName, password });
      // Clear the password from component state the moment it is no longer needed, then drop
      // the overlay. `reset` only changes this component's state - nothing below unmounts.
      setPassword('');
      reauth.reset();
      reset();
    } catch {
      // Swallowed on purpose: the error is rendered from `reauth.error` below. Re-throwing
      // would surface an unhandled rejection and tell the physician nothing useful.
    }
  };

  return (
    <div className="screen-lock">
      <div
        className={locked ? 'screen-lock__app screen-lock__app--obscured' : 'screen-lock__app'}
        // Hidden from assistive technology while locked, but still in the DOM - which is the
        // entire point (E-41).
        aria-hidden={locked || undefined}
        data-testid="screen-lock-content"
      >
        {children}
      </div>

      {locked ? (
        <div
          className="screen-lock__overlay"
          role="dialog"
          aria-modal="true"
          aria-labelledby="screen-lock-title"
          data-testid="screen-lock-overlay"
        >
          <div className="screen-lock__panel">
            <h2 className="screen-lock__title" id="screen-lock-title">
              Screen locked
            </h2>
            <p className="screen-lock__message">
              The screen locked after 5 minutes without activity. Nothing has been lost - enter
              your password to carry on exactly where you left off.
            </p>

            <form onSubmit={handleSubmit} autoComplete="off" noValidate>
              <p className="screen-lock__user">
                Signed in as <strong>{userName}</strong>
              </p>

              <TextField
                label="Password"
                name="lock-password"
                type="password"
                ref={passwordRef}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                error={
                  reauth.isError
                    ? isProblemDetailsError(reauth.error) && reauth.error.status === 401
                      ? 'That password was not recognised. Nothing you were working on has been lost.'
                      : isProblemDetailsError(reauth.error)
                        ? reauth.error.userMessage
                        : 'Could not unlock. Try again.'
                    : undefined
                }
              />

              <button className="button button--primary" type="submit" disabled={reauth.isPending}>
                {reauth.isPending ? 'Unlocking...' : 'Unlock'}
              </button>
            </form>
          </div>
        </div>
      ) : null}
    </div>
  );
}
