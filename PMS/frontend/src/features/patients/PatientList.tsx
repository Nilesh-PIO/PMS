import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { PatientResults } from './PatientResults';
import { useDebouncedValue, usePatientSearch } from './usePatientSearch';

/**
 * The patients page (route `/patients?query=`, planning-pms-verification.md, F-7 point 4).
 *
 * The same search as the header box, given room to breathe: more results, a visible toggle, and a
 * URL that survives a refresh and can be bookmarked or sent to someone. The header box is for
 * getting somewhere in one keystroke; this is for when the first attempt did not find them.
 *
 * **The query lives in the URL, not in component state.** Refreshing a page mid-search should not
 * silently discard what was typed, and "the search I was looking at" is exactly the sort of thing
 * a physician re-opens from history.
 */
export function PatientList() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const query = searchParams.get('query') ?? '';
  const includeInactive = searchParams.get('includeInactive') === 'true';

  const debouncedQuery = useDebouncedValue(query);
  const search = usePatientSearch(debouncedQuery, { includeInactive, take: 50 });

  const updateParams = (next: { query?: string; includeInactive?: boolean }) => {
    const params = new URLSearchParams(searchParams);

    if (next.query !== undefined) {
      if (next.query) {
        params.set('query', next.query);
      } else {
        params.delete('query');
      }
    }

    if (next.includeInactive !== undefined) {
      if (next.includeInactive) {
        params.set('includeInactive', 'true');
      } else {
        params.delete('includeInactive');
      }
    }

    // `replace` so typing a nine-character name does not leave nine entries in the back stack.
    setSearchParams(params, { replace: true });
  };

  return (
    <section className="patient-list">
      <header className="patient-list__header">
        <h1>Patients</h1>
        <Link className="button button--primary" to="/patients/new">
          Register a patient
        </Link>
      </header>

      <form
        className="patient-list__search"
        role="search"
        autoComplete="off"
        onSubmit={(event) => event.preventDefault()}
      >
        <label className="field">
          <span className="field__label">Search by name or phone number</span>
          <input
            className="field__input"
            type="search"
            autoComplete="off"
            // Autofocus is safe here in a way it is not in the header: this route exists to be
            // searched, and arriving on it is itself the request to type.
            autoFocus
            value={query}
            onChange={(event) => updateParams({ query: event.target.value })}
            placeholder="e.g. Ravi Kumar, or 3210"
          />
        </label>

        <label className="patient-list__toggle">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(event) => updateParams({ includeInactive: event.target.checked })}
          />
          Include retired and merged records
        </label>
      </form>

      <PatientResults
        query={debouncedQuery}
        results={search.data}
        isLoading={search.isLoading}
        isFetching={search.isFetching}
        error={search.error}
        onSelect={(id) => navigate(`/patients/${id}`)}
        idleMessage="Type at least two characters to search. Recent patients are on the home screen."
      />
    </section>
  );
}
