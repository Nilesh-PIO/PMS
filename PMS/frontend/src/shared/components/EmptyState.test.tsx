import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmptyState } from './EmptyState';

describe('EmptyState', () => {
  it('renders the title as a status so it is announced, not silent (E-2)', () => {
    render(<EmptyState title="No patients yet" />);

    expect(screen.getByRole('status')).toHaveTextContent('No patients yet');
  });

  it('renders a description when given one', () => {
    render(<EmptyState title="No patients yet" description="Register the first patient." />);

    expect(screen.getByText('Register the first patient.')).toBeInTheDocument();
  });

  it('renders an action when given one', () => {
    render(<EmptyState title="No patient found" action={<button>Register</button>} />);

    expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument();
  });

  it('renders nothing extra when description and action are omitted', () => {
    const { container } = render(<EmptyState title="Nothing here" />);

    expect(container.querySelector('.empty-state__description')).toBeNull();
    expect(container.querySelector('.empty-state__action')).toBeNull();
  });
});
