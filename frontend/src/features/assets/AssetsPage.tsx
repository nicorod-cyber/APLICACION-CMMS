import { EquipmentOverviewPage } from "../equipment-overview/EquipmentOverviewPage";

/** Compatibility route: the canonical equipment workspace is reused without redirecting /activos. */
export function AssetsPage() {
  return <EquipmentOverviewPage />;
}