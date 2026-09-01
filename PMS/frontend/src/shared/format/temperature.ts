import {
  SELECTABLE_TEMPERATURE_UNITS,
  TemperatureUnit,
  type TemperatureUnitValue,
} from '../../features/clinic/types/clinicProfile';

/**
 * **E-24.** A temperature without its unit is a number nobody can safely act on: 37 and 98.6 are
 * the same fever, and 37 read as Fahrenheit is hypothermia.
 *
 * F-3 owns the clinic's chosen unit, so F-3 also ships the one function that renders a
 * temperature - F-11 (vitals capture) and F-14 (print) call this rather than each concatenating a
 * degree symbol and eventually forgetting one. The unit is never optional in the output; that is
 * the point.
 */

/** The symbol for a unit, e.g. `°C`. Returns an empty string for an unchosen unit. */
export function temperatureUnitSymbol(unit: TemperatureUnitValue): string {
  return SELECTABLE_TEMPERATURE_UNITS.find((u) => u.value === unit)?.symbol ?? '';
}

/**
 * Renders a stored temperature with its unit, e.g. `38.4 °C`.
 *
 * @param value The stored numeric value, or `null`/`undefined` when the vital was not recorded.
 * @param unit The clinic's chosen unit.
 * @param notRecorded What to render when there is no value. Never a sentinel number (E-18).
 */
export function formatTemperature(
  value: number | null | undefined,
  unit: TemperatureUnitValue,
  notRecorded = 'Not recorded',
): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return notRecorded;
  }

  if (unit === TemperatureUnit.Unspecified) {
    // Refusing to render a bare number is deliberate. Printing "38.4" with no unit on a
    // prescription is worse than printing nothing, because it looks like an answer.
    return `${value} (unit not set)`;
  }

  return `${value} ${temperatureUnitSymbol(unit)}`;
}
