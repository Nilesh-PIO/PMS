import { describe, expect, it } from 'vitest';
import { TemperatureUnit } from '../../features/clinic/types/clinicProfile';
import { formatTemperature, temperatureUnitSymbol } from './temperature';

/**
 * F-3, acceptance criterion 5 / E-24: the chosen unit is displayed alongside every stored
 * temperature. F-11 and F-14 render through this function rather than each concatenating a degree
 * symbol, so "the unit is always shown" is a property of one function instead of a habit.
 */
describe('formatTemperature (E-24)', () => {
  it('renders the value with the clinic-chosen unit', () => {
    expect(formatTemperature(38.4, TemperatureUnit.Celsius)).toBe('38.4 °C');
    expect(formatTemperature(101.1, TemperatureUnit.Fahrenheit)).toBe('101.1 °F');
  });

  it('never renders a bare number', () => {
    // 37 read as Fahrenheit is hypothermia; 98.6 read as Celsius is fatal. A number with no unit
    // on a prescription is worse than nothing, because it looks like an answer.
    for (const unit of [
      TemperatureUnit.Celsius,
      TemperatureUnit.Fahrenheit,
      TemperatureUnit.Unspecified,
    ] as const) {
      expect(formatTemperature(37, unit)).not.toBe('37');
    }
  });

  it('says so when the clinic has not chosen a unit', () => {
    expect(formatTemperature(37, TemperatureUnit.Unspecified)).toBe('37 (unit not set)');
  });

  it('renders "not recorded" rather than a sentinel number for an absent value (E-18)', () => {
    expect(formatTemperature(null, TemperatureUnit.Celsius)).toBe('Not recorded');
    expect(formatTemperature(undefined, TemperatureUnit.Celsius)).toBe('Not recorded');
    expect(formatTemperature(null, TemperatureUnit.Celsius, 'Equipment unavailable')).toBe(
      'Equipment unavailable',
    );
  });

  it('does not treat zero as absent', () => {
    expect(formatTemperature(0, TemperatureUnit.Celsius)).toBe('0 °C');
  });

  it('exposes the bare symbol for labelling an input', () => {
    expect(temperatureUnitSymbol(TemperatureUnit.Celsius)).toBe('°C');
    expect(temperatureUnitSymbol(TemperatureUnit.Fahrenheit)).toBe('°F');
    expect(temperatureUnitSymbol(TemperatureUnit.Unspecified)).toBe('');
  });
});
