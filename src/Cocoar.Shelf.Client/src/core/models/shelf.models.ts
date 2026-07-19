/** Whether a product's source is public — display-only, drives a landing-page badge. */
export type ProductOpenness = 'Unspecified' | 'OpenSource' | 'Proprietary';

/** A principal (group or user) that a product's read access can be granted to. */
export type PrincipalKind = 'Group' | 'User';

export interface PrincipalRef {
  /** Group id (GUID) or user email. */
  kind: PrincipalKind;
  id: string;
}

/** An assignable principal from GET /principals, for the product access picker. */
export interface Principal {
  kind: PrincipalKind;
  id: string;
  displayName: string;
}

export interface Product {
  name: string;
  displayName: string | null;
  description: string | null;
  source: string;
  visibility: string;
  /** Access control: hidden from users without a read grant, 404 on unauthorized access. */
  restricted: boolean;
  /** Principals (groups/users) granted read access when restricted. */
  readPrincipals: PrincipalRef[];
  tags: string[];
  showWhenEmpty: boolean;
  /** Open-source vs proprietary marker (display-only). */
  openness: ProductOpenness;
  /** Optional source-repository URL; when set the landing card shows a "Source ↗" link. */
  repositoryUrl: string | null;
  hasApiKey: boolean;
  latest: string | null;
  versions: string[];
}

export interface ProductVersions {
  name: string;
  latest: string | null;
  versions: string[];
}

export interface CreateProductRequest {
  name: string;
  displayName?: string;
  description?: string;
  source?: string;
  visibility?: string;
  tags?: string[];
  showWhenEmpty?: boolean;
  restricted?: boolean;
  readPrincipals?: PrincipalRef[];
  openness?: ProductOpenness;
  repositoryUrl?: string;
  /** Per-product upload key. Write-only: responses only carry hasApiKey. */
  apiKey?: string;
}

export interface UpdateProductRequest {
  displayName?: string;
  description?: string;
  source?: string;
  visibility?: string;
  tags?: string[];
  showWhenEmpty?: boolean;
  restricted?: boolean;
  readPrincipals?: PrincipalRef[];
  openness?: ProductOpenness;
  /** undefined = keep, '' = remove the repo link, value = set. */
  repositoryUrl?: string;
  /** undefined = keep, '' = remove, value = replace. */
  apiKey?: string;
}

export interface ShelfSettingsInfo {
  hasMasterApiKey: boolean;
  masterApiKey: string | null;
  hasConfigApiKey: boolean;
}

// --- Access Control v2: groups ---

export type MembershipMode = 'Manual' | 'Auto';

export interface GroupSummary {
  id: string;
  name: string;
  description: string | null;
  membershipMode: MembershipMode;
  memberEmails: string[];
  membershipScript: string | null;
  isAdminGroup: boolean;
  autoMemberCount: number;
  membershipLastError: string | null;
}

export interface GroupMember {
  id: string;
  email: string | null;
  displayName: string;
}

export interface GroupDetail {
  id: string;
  name: string;
  description: string | null;
  membershipMode: MembershipMode;
  memberEmails: string[];
  membershipScript: string | null;
  isAdminGroup: boolean;
  membershipLastError: string | null;
  /** Resolved materialized auto-members (read-only in the UI). */
  autoMembers: GroupMember[];
}

export interface GroupUpsertRequest {
  name: string;
  description?: string;
  membershipMode: MembershipMode;
  memberEmails?: string[];
  membershipScript?: string;
  isAdminGroup?: boolean;
}

export interface ScriptTestResult {
  matched: boolean;
  error: string | null;
  user: GroupMember;
}
