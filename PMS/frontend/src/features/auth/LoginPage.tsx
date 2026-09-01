import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { TextField } from '../../shared/components/forms/TextField';
import { useLogin, useSession } from './useSession';

interface LocationState {
  from?: string;
}

/**
 * The sign-in screen (route `/login`, planning-pms-verification.md, F-2 point 4).
 *
 * Rendered outside `AppLayout`: there is no navigation chrome to offer someone who is not
 * signed in, and no PHI anywhere on this page.
 */
export function LoginPage() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');

  const navigate = useNavigate();
  const location = useLocation();
  const login = useLogin();
  const { data: session, isPending: sessionPending } = useSession();

  // Already signed in - typing /login by hand should not strand the physician on a form.
  if (!sessionPending && session) {
    return <Navigate to={(location.state as LocationState | null)?.from ?? '/'} replace />;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    try {
      await login.mutateAsync({ userName, password });
      // Clear the password from component state as soon as the server has it.
      setPassword('');
      navigate((location.state as LocationState | null)?.from ?? '/', { replace: true });
    } catch {
      // Rendered from `login.error` below. Re-throwing here would become an unhandled
      // rejection and tell the physician nothing.
    }
  };

  const fieldErrors = isProblemDetailsError(login.error) ? login.error.fieldErrors : {};

  const formError = (() => {
    if (!login.isError) {
      return null;
    }
    if (isProblemDetailsError(login.error)) {
      if (login.error.status === 401) {
        // One message for a wrong user name and a wrong password, matching the server's single
        // 401 - telling them which half was wrong would confirm the user name.
        return 'That user name and password were not recognised.';
      }
      if (login.error.status === 400) {
        return null; // Shown per field instead.
      }
      return login.error.userMessage;
    }
    return 'Could not sign in. Try again.';
  })();

  return (
    <main className="login-page">
      <div className="login-page__panel">
        <h1 className="login-page__title">Clinic sign in</h1>

        {formError ? (
          <p className="login-page__error" role="alert">
            {formError}
          </p>
        ) : null}

        {/*
          autoComplete="off" on the form and on both fields. The threat is not a stolen laptop
          but the shared consulting-room PC: a browser that remembers this form offers the
          clinic's only credential to whoever sits down next (E-65, E-62).
        */}
        <form onSubmit={handleSubmit} autoComplete="off" noValidate>
          <TextField
            label="User name"
            name="userName"
            type="text"
            value={userName}
            onChange={(event) => setUserName(event.target.value)}
            error={fieldErrors.UserName?.[0]}
            autoFocus
          />

          <TextField
            label="Password"
            name="password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            error={fieldErrors.Password?.[0]}
          />

          <button className="button button--primary" type="submit" disabled={login.isPending}>
            {login.isPending ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </div>
    </main>
  );
}
