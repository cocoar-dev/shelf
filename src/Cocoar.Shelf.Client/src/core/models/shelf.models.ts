export interface Product {
  name: string;
  displayName: string | null;
  description: string | null;
  source: string;
  visibility: string;
  /** Access control: hidden from users without a read grant, 404 on unauthorized access. */
  restricted: boolean;
  tags: string[];
  showWhenEmpty: boolean;
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
  readProducts: string[];
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
  readProducts: string[];
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
  readProducts?: string[];
  isAdminGroup?: boolean;
}

export interface ScriptTestResult {
  matched: boolean;
  error: string | null;
  user: GroupMember;
}
