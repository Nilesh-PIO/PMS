import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { API_BASE_PATH, httpClient, request } from './httpClient';
import { ProblemDetailsError, isProblemDetailsError } from './problemDetails';

/**
 * F-1 test strategy: "httpClient.test.ts (Vitest) - ProblemDetails parsing, non-JSON error
 * bodies". Extended to cover the transport-failure path, because that is the one that
 * produces E-47 ("doctor believes it saved") if it is allowed to reject with a raw TypeError.
 */

const fetchMock = vi.fn();

function jsonResponse(body: unknown, status = 200, contentType = 'application/json') {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': contentType },
  });
}

function textResponse(body: string, status: number, contentType = 'text/html') {
  return new Response(body, { status, headers: { 'Content-Type': contentType } });
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('request - success paths', () => {
  it('prefixes every path with /api', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ status: 'Healthy' }));

    await request('/health');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe(`${API_BASE_PATH}/health`);
  });

  it('returns the parsed JSON body', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ status: 'Healthy', component: 'api', detail: null }),
    );

    const body = await request<{ status: string; component: string }>('/health');

    expect(body).toEqual({ status: 'Healthy', component: 'api', detail: null });
  });

  it('sends cookies same-origin and never reads a token from storage', async () => {
    fetchMock.mockResolvedValue(jsonResponse({}));

    await request('/health');

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.credentials).toBe('same-origin');
    // Section 2 auth decision: no token in localStorage/sessionStorage, ever.
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });

  it('serialises a JSON body and sets Content-Type', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: '1' }, 201));

    await httpClient.post('/patients', { fullName: 'Ravi Kumar' });

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.method).toBe('POST');
    expect(init.body).toBe(JSON.stringify({ fullName: 'Ravi Kumar' }));
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
  });

  it('does not force a JSON Content-Type on FormData (signature upload, F-3)', async () => {
    fetchMock.mockResolvedValue(jsonResponse({}));
    const form = new FormData();
    form.append('file', new Blob(['x']), 'signature.png');

    await httpClient.post('/clinic-profile/signature', form);

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(new Headers(init.headers).has('Content-Type')).toBe(false);
    expect(init.body).toBe(form);
  });

  it('resolves with undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    await expect(request('/auth/logout', { method: 'POST' })).resolves.toBeUndefined();
  });

  it('resolves with undefined for an empty 200 body', async () => {
    fetchMock.mockResolvedValue(new Response('', { status: 200 }));

    await expect(request('/health')).resolves.toBeUndefined();
  });

  it('throws when a 2xx body is not valid JSON', async () => {
    fetchMock.mockResolvedValue(textResponse('<html>oops</html>', 200));

    await expect(request('/health')).rejects.toBeInstanceOf(ProblemDetailsError);
  });
});

describe('request - ProblemDetails parsing', () => {
  it('throws a ProblemDetailsError carrying the parsed body', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          type: 'https://example/conflict',
          title: 'The request conflicts with a domain rule.',
          status: 409,
          detail: 'Clinic setup is incomplete.',
          ruleType: 'setup-incomplete',
        },
        409,
        'application/problem+json',
      ),
    );

    const error = await request('/prescriptions/1').catch((e: unknown) => e);

    expect(isProblemDetailsError(error)).toBe(true);
    const problemError = error as ProblemDetailsError;
    expect(problemError.status).toBe(409);
    expect(problemError.ruleType).toBe('setup-incomplete');
    expect(problemError.problem.detail).toBe('Clinic setup is incomplete.');
    expect(problemError.userMessage).toBe('Clinic setup is incomplete.');
  });

  it('exposes field-keyed validation errors from a 400', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            fullName: ['Name is required.'],
            dateOfBirth: ['Date of birth cannot be in the future.'],
          },
        },
        400,
        'application/problem+json',
      ),
    );

    const error = (await request('/patients', { method: 'POST', body: {} }).catch(
      (e: unknown) => e,
    )) as ProblemDetailsError;

    expect(error.status).toBe(400);
    expect(error.fieldErrors.fullName).toEqual(['Name is required.']);
    expect(error.fieldErrors.dateOfBirth).toHaveLength(1);
  });

  it('exposes an empty fieldErrors object when the body has none', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ title: 'Nope', status: 401 }, 401));

    const error = (await request('/patients').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(error.fieldErrors).toEqual({});
    expect(error.ruleType).toBeUndefined();
  });

  it('falls back to the response status when the body omits it', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ title: 'Gone' }, 410));

    const error = (await request('/patients').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(error.status).toBe(410);
    expect(error.problem.status).toBe(410);
  });
});

describe('request - non-JSON and malformed error bodies', () => {
  it('still throws a ProblemDetailsError for an HTML error page', async () => {
    fetchMock.mockResolvedValue(
      textResponse('<html><body>502 Bad Gateway</body></html>', 502),
    );

    const error = (await request('/patients').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(isProblemDetailsError(error)).toBe(true);
    expect(error.status).toBe(502);
    expect(error.problem.detail).toContain('502 Bad Gateway');
    // The HTML is never promoted to the user-facing title.
    expect(error.problem.title).not.toContain('<html>');
  });

  it('handles a completely empty error body', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 500, statusText: 'Server Error' }));

    const error = (await request('/patients').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(error.status).toBe(500);
    expect(error.message).toBeTruthy();
  });

  it('handles a JSON array body, which is not a ProblemDetails', async () => {
    fetchMock.mockResolvedValue(jsonResponse([1, 2, 3], 400));

    const error = (await request('/patients').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(error.status).toBe(400);
    expect(error.fieldErrors).toEqual({});
  });

  it('truncates a very long non-JSON body rather than carrying it whole', async () => {
    fetchMock.mockResolvedValue(textResponse('x'.repeat(5000), 500));

    const error = (await request('/patients').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(error.problem.detail!.length).toBeLessThanOrEqual(503);
    expect(error.problem.detail!.endsWith('...')).toBe(true);
  });
});

describe('request - transport failure (E-47)', () => {
  it('converts a rejected fetch into a ProblemDetailsError, never a raw TypeError', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = (await request('/patients', { method: 'POST', body: {} }).catch(
      (e: unknown) => e,
    )) as ProblemDetailsError;

    expect(isProblemDetailsError(error)).toBe(true);
    expect(error.isNetworkError).toBe(true);
    expect(error.status).toBe(0);
  });

  it('tells the user their work was not saved', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = (await request('/visits/1').catch((e: unknown) => e)) as ProblemDetailsError;

    expect(error.userMessage).toMatch(/not been saved/i);
  });

  it('never resolves on a transport failure', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(request('/patients')).rejects.toBeInstanceOf(ProblemDetailsError);
  });

  it('rethrows an AbortError unchanged so cancellation is not reported as a failure', async () => {
    fetchMock.mockRejectedValue(new DOMException('The operation was aborted.', 'AbortError'));

    const error = await request('/patients').catch((e: unknown) => e);

    expect(isProblemDetailsError(error)).toBe(false);
    expect((error as DOMException).name).toBe('AbortError');
  });
});
