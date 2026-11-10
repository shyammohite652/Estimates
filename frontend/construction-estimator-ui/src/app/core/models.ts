// Typed mirrors of the API contract (ConstructionEstimator.Application DTOs).

export type UserRole = 'Estimator' | 'Reviewer' | 'Viewer' | 'ProjectManager';
export type EstimateStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
export type EstimateAction =
  | 'Created' | 'Submitted' | 'Approved' | 'Rejected' | 'Reopened' | 'DescriptionCorrected';

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface EstimateSummary {
  id: string;
  number: string;
  description: string;
  status: EstimateStatus;
  pricingDate: string;
  lineCount: number;
  total: number;
}

export interface ProjectSummary {
  id: string;
  code: string;
  name: string;
  client: string | null;
  estimates: EstimateSummary[];
}

/** Server-computed: the UI shows what these allow, the API enforces it regardless. */
export interface EstimateCapabilities {
  canEditLines: boolean;
  canSubmit: boolean;
  canApprove: boolean;
  canReject: boolean;
  canReopen: boolean;
  canCorrectDescription: boolean;
  canViewLabourCost: boolean;
}

export interface EstimateDetails {
  id: string;
  projectId: string;
  projectName: string;
  number: string;
  description: string;
  pricingDate: string;
  status: EstimateStatus;
  createdById: string;
  createdByName: string;
  createdAt: string;
  updatedAt: string;
  lineCount: number;
  netTotal: number;
  markupTotal: number;
  total: number;
  /** Absent (not null) for roles that may not see labour cost — R17. */
  labourTotal?: number;
  capabilities: EstimateCapabilities;
}

export interface EstimateLine {
  id: number;
  catalogueItemId: string;
  itemCode: string;
  itemDescription: string;
  unit: string;
  quantity: number;
  markupPercent: number;
  unitRate: number;
  rateEffectiveFrom: string;
  netAmount: number;
  markupAmount: number;
  lineTotal: number;
  unitLabourCost?: number;
  labourAmount?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface EstimateHistoryEntry {
  id: number;
  action: EstimateAction;
  fromStatus: EstimateStatus | null;
  toStatus: EstimateStatus | null;
  userId: string;
  userName: string;
  occurredAt: string;
  comment: string | null;
}

export interface CatalogueItem {
  id: string;
  code: string;
  description: string;
  unit: string;
  rate: number | null;
  rateEffectiveFrom: string | null;
  labourComponent?: number;
}

/** RFC 7807 body produced by the API's exception handler. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
