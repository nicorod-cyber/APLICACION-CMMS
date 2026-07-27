export type Page<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
};

export type DocumentRequirementStatus = {
  code: string;
  status?: string | null;
  expirationDate?: string | null;
  daysUntilExpiration?: number | null;
  applies?: boolean | null;
};

export type EquipmentOverviewRow = {
  rowId: string;
  rowType: "ASSET" | "LOOSE_COMPONENT" | "COMPOSITE_UNIT";
  assetId?: string | null;
  operationalUnitId?: string | null;
  code: string;
  name: string;
  zone?: string | null;
  siteId?: string | null;
  siteCode?: string | null;
  siteName?: string | null;
  operationalStateCode: string;
  operationalStateName: string;
  physicalLocationType?: string | null;
  physicalLocationId?: string | null;
  physicalLocationName?: string | null;
  physicalLocationCommune?: string | null;
  equipmentTypeCode?: string | null;
  equipmentTypeName: string;
  brand?: string | null;
  manufacturingYear?: number | null;
  usageMeasurementType?: string | null;
  lastReading?: number | null;
  usageUnit?: string | null;
  lastPreventiveType?: string | null;
  usageSinceLastPreventive?: number | null;
  approximateNextMaintenanceDate?: string | null;
  technicalReview?: DocumentRequirementStatus | null;
  sernageomin?: DocumentRequirementStatus | null;
  dgmn?: DocumentRequirementStatus | null;
  fireSuppression?: DocumentRequirementStatus | null;
};

export type EquipmentOverviewFiltersValue = {
  search: string;
  faenaCodigo: string;
  zona: string;
  tipoActivoCodigo: string;
  estadoOperacionalCodigo: string;
  tipoUbicacionFisica: string;
  tallerCodigo: string;
};

export type CatalogItem = { codigo: string; nombre: string; tipoActivoCodigo?: string | null };
export type AssetCatalog = { tiposActivo: CatalogItem[]; estadosOperacionales: CatalogItem[] };