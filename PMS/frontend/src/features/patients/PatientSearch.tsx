import { useEffect, useId, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PatientResults } from './PatientResults';
import { useDebouncedValue, usePatientSearch } from './usePatientSearch';

/**
 * The global patient search box (planning-pms-verification.md, F-7 point 4: "global, mounted in
 * `AppLayout`; `/` focuses it — REC-16"; BRD L93 and L158).
 *
 * **Why this lives in the layout chrome rather than on a page.** REC-16 asks for keyboard-first
 * navigation, and the physician's most common action by a wide margin is "find this patient". Put
 * on a page, finding someone costs a navigation first; put in the header with a `/` shortcut, it
 * costs one keystroke from anywhere in the application, including mid-consultation.
 *
 * **What it deliberately does not do: select anything for you.** Results are a list of buttons.
 * Even a single result must be clicked. See `PatientPickerRow` for why that is a rule rather than
 * a preference (E-28, RSK-12).
 */
export function PatientSearch() {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = useId();

  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [includeInactive, setIncludeInactive] = useState(false);

  // The input shows what was typed immediately; only the request lags behind it. Debouncing the
  // input value itself would make the box feel broken.
  const debouncedQuery = useDebouncedValue(query);

  const search = usePatientSearch(debouncedQuery, { includeInactive, enabled: isOpen });

  // REC-16: "/" focuses the search from anywhere.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
        return;
      }

      if (event.key !== '/' || event.ctrlKey || event.metaKey || event.altKey) {
        return;
      }

      // Never steal the key from someone typing. A consultation note full of "/" characters that
      // silently teleported the caret to the search box would be worse than having no shortcut.
      if (isTypingTarget(event.target)) {
        return;
      }

      event.preventDefault();
      setIsOpen(true);
      inputRef.current?.focus();
      inputRef.current?.select();
    };

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  // Close when focus or a click leaves the widget - but never on a blur *inside* it, or clicking a
  // result would unmount the result before the click landed.
  useEffect(() => {
    const onPointerDown = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', onPointerDown);
    return () => document.removeEventListener('mousedown', onPointerDown);
  }, []);

  const handleSelect = (id: string) => {
    setIsOpen(false);
    setQuery('');
    navigate(`/patients/${id}`);
  };

  return (
    <div className="patient-search" ref={containerRef}>
      <form
        className="patient-search__form"
        role="search"
        // E-65 / F-2's convention: patient-identifying input is never offered to the browser's
        // autofill store.
        autoComplete="off"
        onSubmit={(event) => {
          event.preventDefault();
          // Enter goes to the full results page rather than opening the top hit - the same
          // no-auto-selection rule the list follows.
          if (query.trim().length >= 2) {
            setIsOpen(false);
            navigate(`/patients?query=${encodeURIComponent(query.trim())}`);
          }
        }}
      >
        <label className="patient-search__label" htmlFor="patient-search-input">
          Find a patient
        </label>
        <input
          id="patient-search-input"
          ref={inputRef}
          className="patient-search__input"
          type="search"
          role="combobox"
          aria-expanded={isOpen}
          aria-controls={listboxId}
          aria-autocomplete="list"
          autoComplete="off"
          placeholder="Name or phone number  ( / )"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value);
            setIsOpen(true);
          }}
          onFocus={() => setIsOpen(true)}
        />
      </form>

      {isOpen ? (
        <div className="patient-search__panel" id={listboxId}>
          <label className="patient-search__toggle">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(event) => setIncludeInactive(event.target.checked)}
            />
            Include retired and merged records
          </label>

          <PatientResults
            query={debouncedQuery}
            results={search.data}
            isLoading={search.isLoading}
            isFetching={search.isFetching}
            error={search.error}
            onSelect={handleSelect}
            idleMessage="Type at least two characters of a name or phone number."
          />
        </div>
      ) : null}
    </div>
  );
}

/** True when the event target is somewhere a "/" is a character rather than a shortcut. */
function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  if (target.isContentEditable) {
    return true;
  }

  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}
