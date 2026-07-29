export const FAENA_ZONES = ["Zona 0", "Zona 1", "Zona 2", "Zona 3", "Zona 4"] as const;

export type FaenaZone = (typeof FAENA_ZONES)[number];
export function normalizeFaenaZone(value: string): FaenaZone | null {
  const match = value.trim().match(/^(?:zona\s*)?([0-4])$/i);
  return match ? (`Zona ${match[1]}` as FaenaZone) : null;
}