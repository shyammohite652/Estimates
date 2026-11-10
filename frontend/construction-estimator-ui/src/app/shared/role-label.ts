import { UserRole } from '../core/models';

export const ROLE_LABELS: Record<UserRole, string> = {
  Estimator: 'Estimator',
  Reviewer: 'Reviewer',
  Viewer: 'Viewer',
  ProjectManager: 'Project Manager',
};

export const ROLE_SUMMARIES: Record<UserRole, string> = {
  Estimator: 'Builds and submits draft estimates. Cannot see labour cost.',
  Reviewer: 'Approves or rejects submitted estimates (not their own).',
  Viewer: 'Read-only access to assigned projects.',
  ProjectManager: 'Reads everything, sees labour cost, corrects descriptions.',
};
