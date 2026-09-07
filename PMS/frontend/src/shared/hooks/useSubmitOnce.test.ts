import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useSubmitOnce } from './useSubmitOnce';

/**
 * The double-submit guard (E-43, E-46, REC-17).
 *
 * The interesting assertion is the *synchronous* one: two calls inside a single act() block, with
 * no await between them, is exactly the shape of a double-click, and it is the case a
 * `disabled={isSubmitting}` prop alone does not catch because React state has not re-rendered yet.
 */
describe('useSubmitOnce', () => {
  it('runs the second of two synchronous submits not at all', async () => {
    const action = vi.fn().mockResolvedValue('ok');
    const { result } = renderHook(() => useSubmitOnce());

    await act(async () => {
      const a = result.current.submit(action);
      const b = result.current.submit(action);
      await Promise.all([a, b]);
    });

    expect(action).toHaveBeenCalledTimes(1);
  });

  it('passes the same submission token to every retry of one intent', async () => {
    // What makes the create idempotent rather than merely rate-limited: a retry after a timeout
    // must be recognisable by the server as the same registration.
    const seen: string[] = [];
    const action = vi.fn(async (id: string) => {
      seen.push(id);
      throw new Error('network');
    });

    const { result } = renderHook(() => useSubmitOnce());

    await act(async () => {
      await result.current.submit(action).catch(() => undefined);
    });
    await act(async () => {
      await result.current.submit(action).catch(() => undefined);
    });

    expect(seen).toHaveLength(2);
    expect(seen[0]).toBe(seen[1]);
  });

  it('issues a new token after reset, so the next save is a new intent', async () => {
    // Without this, registering a second patient without a page reload would replay the first
    // token and be answered with the first patient.
    const seen: string[] = [];
    const action = vi.fn(async (id: string) => {
      seen.push(id);
      return 'ok';
    });

    const { result } = renderHook(() => useSubmitOnce());

    await act(async () => {
      await result.current.submit(action);
    });
    act(() => result.current.reset());
    await act(async () => {
      await result.current.submit(action);
    });

    expect(seen[0]).not.toBe(seen[1]);
  });

  it('releases the lock after a failure so a retry is possible', async () => {
    // A form that locks itself after one network error is a form the physician has to reload,
    // and reloading is how typed work gets lost (E-47).
    const action = vi.fn().mockRejectedValue(new Error('network'));
    const { result } = renderHook(() => useSubmitOnce());

    await act(async () => {
      await result.current.submit(action).catch(() => undefined);
    });

    await waitFor(() => expect(result.current.isSubmitting).toBe(false));

    await act(async () => {
      await result.current.submit(action).catch(() => undefined);
    });

    expect(action).toHaveBeenCalledTimes(2);
  });

  it('reports isSubmitting while in flight and clears it afterwards', async () => {
    let release: (() => void) | undefined;
    const action = vi.fn(
      () =>
        new Promise<string>((resolve) => {
          release = () => resolve('ok');
        }),
    );

    const { result } = renderHook(() => useSubmitOnce());

    let pending: Promise<unknown> | undefined;
    act(() => {
      pending = result.current.submit(action);
    });

    await waitFor(() => expect(result.current.isSubmitting).toBe(true));

    await act(async () => {
      release?.();
      await pending;
    });

    expect(result.current.isSubmitting).toBe(false);
  });

  it('returns the actions value to the caller that actually ran it', async () => {
    const { result } = renderHook(() => useSubmitOnce());

    let returned: string | undefined;
    await act(async () => {
      returned = await result.current.submit(async () => 'created');
    });

    expect(returned).toBe('created');
  });
});
