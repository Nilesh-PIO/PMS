import { describe, expect, it } from 'vitest';
import { createQueryClient } from './queryClient';
import { ProblemDetailsError } from './problemDetails';

describe('queryClient defaults', () => {
  it('does not refetch on window focus', () => {
    // A refetch when the doctor tabs back mid-consultation would replace what is on screen.
    const defaults = createQueryClient().getDefaultOptions();

    expect(defaults.queries?.refetchOnWindowFocus).toBe(false);
  });

  it('retries a query at most once, and only for network failures', () => {
    const retry = createQueryClient().getDefaultOptions().queries?.retry;
    expect(typeof retry).toBe('function');

    const decide = retry as (failureCount: number, error: Error) => boolean;
    const networkError = new ProblemDetailsError({ status: 0 }, 0);
    const conflict = new ProblemDetailsError({ status: 409 }, 409);

    expect(decide(0, networkError)).toBe(true);
    expect(decide(1, networkError)).toBe(false);
    expect(decide(0, conflict)).toBe(false);
  });

  it('never retries a mutation automatically', () => {
    // A retried POST can create a second patient or a second visit (E-46).
    const defaults = createQueryClient().getDefaultOptions();

    expect(defaults.mutations?.retry).toBe(false);
  });
});
