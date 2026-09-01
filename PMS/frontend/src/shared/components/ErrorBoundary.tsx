import { Component, type ErrorInfo, type ReactNode } from 'react';
import { isProblemDetailsError } from '../api/problemDetails';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/**
 * Last line of the error contract on the client. A render-time throw anywhere below this
 * boundary shows the doctor a visible failure instead of a blank white screen - a blank
 * screen mid-consultation reads as "it saved and moved on" (E-47).
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Console only for now; F-19 decides what, if anything, is reported to the server.
    // Nothing here may include patient data.
    console.error('Unhandled UI error', error, info.componentStack);
  }

  private handleReload = () => {
    this.setState({ error: null });
    window.location.reload();
  };

  render() {
    const { error } = this.state;

    if (!error) {
      return this.props.children;
    }

    const message = isProblemDetailsError(error)
      ? error.userMessage
      : 'Something went wrong on this screen.';

    return (
      <div className="error-boundary" role="alert">
        <h1>Something went wrong</h1>
        <p>{message}</p>
        <p>Nothing has been discarded. Reload to continue.</p>
        <button type="button" onClick={this.handleReload}>
          Reload
        </button>
      </div>
    );
  }
}
