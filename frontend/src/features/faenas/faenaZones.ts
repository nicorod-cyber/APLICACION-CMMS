export const FAENA_ZONES = ["Zona 0", "Zona 1", "Zona 2", "Zona 3", "Zona 4"] as const;

export type FaenaZone = (typeof FAENA_ZONES)[number];