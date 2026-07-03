/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

export enum AlphaZeroModulesVideoUploadingApplicationVideoEncryptionMethod {
  None = 0,
  ClearKey = 1,
  DRM = 2,
}

export enum AlphaZeroModulesVideoUploadingApplicationVideoTranscodingMetehod {
  FFMPEG = 0,
  MediaConvert = 1,
}

export enum AlphaZeroModulesIdentityDomainModelsPrincipalsConditionType {
  And = 0,
  Or = 1,
  Not = 2,
  Statement = 3,
  Reference = 4,
}

export enum AlphaZeroModulesIdentityDomainModelsPrincipalsPrincipalType {
  User = 0,
  Role = 1,
}

export enum AlphaZeroModulesIdentityDomainModelsDevicePlatform {
  Web = 0,
  Android = 1,
  Ios = 2,
}

export enum AlphaZeroModulesAssessmentsDomainEnumsAssessmentType {
  MCQ = 0,
  Handwritten = 1,
  Hybrid = 2,
}

export enum AlphaZeroModulesAssessmentsDomainEnumsQuestionType {
  MCQ = 0,
  Handwritten = 1,
  Voice = 2,
  Video = 3,
}

export enum AlphaZeroModulesAssessmentsDomainEnumsItemType {
  Paragraph = 0,
  Question = 1,
}

export interface AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentResponse {
  /** @format decimal */
  score?: number | null;
  status?: string;
}

export interface AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentRequest {
  responses?: AlphaZeroModulesAssessmentsDomainModelsSubmissionsAssessmentSubmissionResponses;
}

export interface AlphaZeroModulesAssessmentsDomainModelsSubmissionsAssessmentSubmissionResponses {
  answers?: Record<string, any>;
}

export interface AlphaZeroSharedQueriesPagedResultOfSubmissionSummaryDto {
  items?: AlphaZeroModulesAssessmentsApplicationSubmissionsQueriesGetSubmissionsSubmissionSummaryDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesAssessmentsApplicationSubmissionsQueriesGetSubmissionsSubmissionSummaryDto {
  /** @format guid */
  id?: string;
  /** @format guid */
  assessmentId?: string;
  /** @format guid */
  studentId?: string;
  status?: string;
  /** @format decimal */
  totalScore?: number | null;
  /** @format date-time */
  submittedAt?: string;
}

export type AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsListListSubmissionsRequest =
  object;

/**
 * RFC7807 compatible problem details/ error response class. this can be used by configuring startup like so:
 * app.UseFastEndpoints(c => c.Errors.UseProblemDetails())
 */
export interface FastEndpointsProblemDetails {
  /** @default "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1" */
  type?: string;
  /** @default "One or more validation errors occurred." */
  title?: string;
  /**
   * @format int32
   * @default 400
   */
  status?: number;
  /** @default "/api/route" */
  instance?: string;
  /** @default "0HMPNHL0JHL76:00000001" */
  traceId?: string;
  /** the details of the error */
  detail?: string | null;
  errors?: FastEndpointsProblemDetailsError[];
}

/** the error details object */
export interface FastEndpointsProblemDetailsError {
  /**
   * the name of the error or property of the dto that caused the error
   * @default "Error or field name"
   */
  name?: string;
  /**
   * the reason for the error
   * @default "Error reason"
   */
  reason?: string;
  /** the code of the error */
  code?: string | null;
  /** the severity of the error */
  severity?: string | null;
}

export interface AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsUpdateContentUpdateAssessmentContentRequest {
  content?: AlphaZeroModulesAssessmentsDomainModelsContentAssessmentContent;
}

export interface AlphaZeroModulesAssessmentsDomainModelsContentAssessmentContent {
  version?: string;
  items?: AlphaZeroModulesAssessmentsDomainModelsContentAssessmentItem[];
}

export interface AlphaZeroModulesAssessmentsDomainModelsContentAssessmentItem {
  id?: string;
  type?: AlphaZeroModulesAssessmentsDomainEnumsItemType;
  renderData?: any;
  questionType?: AlphaZeroModulesAssessmentsDomainEnumsQuestionType | null;
  /** @format decimal */
  points?: number | null;
  gradingData?: AlphaZeroModulesAssessmentsDomainModelsContentGradingData | null;
}

export interface AlphaZeroModulesAssessmentsDomainModelsContentGradingData {
  choices?: AlphaZeroModulesAssessmentsDomainModelsContentChoice[] | null;
  correctChoiceId?: string | null;
  shuffleOptions?: boolean;
  rubric?: string | null;
  aiHint?: string | null;
}

export interface AlphaZeroModulesAssessmentsDomainModelsContentChoice {
  id?: string;
  renderData?: any;
}

export interface AlphaZeroSharedQueriesPagedResultOfAssessmentDto {
  items?: AlphaZeroModulesAssessmentsApplicationAssessmentsQueriesListAssessmentsAssessmentDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesAssessmentsApplicationAssessmentsQueriesListAssessmentsAssessmentDto {
  /** @format guid */
  id?: string;
  title?: string;
  description?: string | null;
  type?: string;
  /** @format decimal */
  passingScore?: number;
  status?: string;
}

export type AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsListListAssessmentsRequest =
  object;

export interface AlphaZeroModulesAssessmentsApplicationAssessmentsQueriesGetAssessmentAssessmentDetailsDto {
  /** @format guid */
  id?: string;
  title?: string;
  description?: string | null;
  type?: string;
  /** @format decimal */
  passingScore?: number;
  status?: string;
  /** @format int32 */
  versionNumber?: number;
  content?: AlphaZeroModulesAssessmentsDomainModelsContentAssessmentContent | null;
}

export type AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsGetGetAssessmentRequest =
  object;

export interface AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentResponse {
  /** @format guid */
  id?: string;
}

export interface AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentRequest {
  title?: string;
  description?: string | null;
  type?: AlphaZeroModulesAssessmentsDomainEnumsAssessmentType;
  /** @format decimal */
  passingScore?: number;
  initialContent?: AlphaZeroModulesAssessmentsDomainModelsContentAssessmentContent | null;
}

export interface AlphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemRequest {
  /** @format int32 */
  bitIndex?: number;
}

export interface AlphaZeroSharedQueriesPagedResultOfSubjectDto {
  items?: AlphaZeroModulesCoursesApplicationSubjectsQueriesGetSubjectSubjectDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesCoursesApplicationSubjectsQueriesGetSubjectSubjectDto {
  /** @format guid */
  id?: string;
  name?: string;
  description?: string | null;
}

export type AlphaZeroModulesCoursesPresentationSubjectsListListSubjectsRequest =
  object;

export type AlphaZeroModulesCoursesPresentationSubjectsGetGetSubjectRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectResponse {
  /** @format guid */
  id?: string;
}

/** @example {"name":"Mathematics","description":"General mathematics curriculum for high school."} */
export interface AlphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectRequest {
  /** @example "Mathematics" */
  name?: string;
  /** @example "General mathematics curriculum for high school." */
  description?: string | null;
}

export interface AlphaZeroModulesCoursesPresentationEnrollementsGetEnrollementResponse {
  /** @format guid */
  id?: string;
  /** @format guid */
  studentId?: string;
  /** @format guid */
  courseId?: string;
  status?: string;
  /** @format double */
  completionPercentage?: number;
  /** @format date-time */
  enrolledOn?: string;
  /** @format guid */
  tenantId?: string;
}

export type AlphaZeroModulesCoursesPresentationEnrollementsGetGetEnrollementRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseResponse {
  /** @format guid */
  enrollmentId?: string;
}

/** @example {"studentId":"ea726e7c-ab83-4c3c-8513-e140d123ee8e","courseId":"8fd9347c-8a0a-441a-b2d8-7eeaba31f2bb"} */
export interface AlphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseRequest {
  /**
   * @format guid
   * @example "ea726e7c-ab83-4c3c-8513-e140d123ee8e"
   */
  studentId?: string;
  /**
   * @format guid
   * @example "8fd9347c-8a0a-441a-b2d8-7eeaba31f2bb"
   */
  courseId?: string;
}

export interface AlphaZeroModulesCoursesPresentationEnrollementsDashboardDashboardResponse {
  academies?: Record<
    string,
    AlphaZeroModulesCoursesPresentationEnrollementsDashboardEnrollmentDto[]
  >;
}

export interface AlphaZeroModulesCoursesPresentationEnrollementsDashboardEnrollmentDto {
  /** @format guid */
  enrollmentId?: string;
  /** @format guid */
  courseId?: string;
  status?: string;
  /** @format double */
  completionPercentage?: number;
  /** @format date-time */
  enrolledOn?: string;
}

export type AlphaZeroModulesCoursesPresentationEnrollementsDashboardGetStudentDashboardRequest =
  object;

export type AlphaZeroModulesCoursesPresentationCoursesStateApproveCourseRequest =
  object;

export type AlphaZeroModulesCoursesPresentationCoursesStatePublishCourseRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationCoursesStateRejectCourseRequest {
  reason?: string;
}

export type AlphaZeroModulesCoursesPresentationCoursesStateSubmitForReviewRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationCoursesReorderSectionsReorderSectionsRequest {
  sectionIds?: string[];
}

export interface AlphaZeroModulesCoursesPresentationCoursesReorderItemsReorderItemsRequest {
  itemIds?: string[];
}

export interface AlphaZeroModulesCoursesPresentationCoursesPlansUpdatePlanUpdatePlanRequest {
  name?: string;
  /** @format guid */
  principalId?: string;
}

export type AlphaZeroModulesCoursesPresentationCoursesPlansRemovePlanRemovePlanRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationCoursesPlansAddPlanAddPlanRequest {
  name?: string;
  /** @format guid */
  principalId?: string;
}

export interface AlphaZeroSharedQueriesPagedResultOfCourseSummaryDto {
  items?: AlphaZeroModulesCoursesApplicationCoursesQueriesListCoursesCourseSummaryDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesCoursesApplicationCoursesQueriesListCoursesCourseSummaryDto {
  /** @format guid */
  id?: string;
  title?: string;
  description?: string | null;
  /** @format guid */
  subjectId?: string;
  status?: string;
}

export type AlphaZeroModulesCoursesPresentationCoursesListListCoursesRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationCoursesGetCourseResponse {
  /** @format guid */
  id?: string;
  title?: string;
  description?: string | null;
  /** @format guid */
  subjectId?: string;
  status?: string;
  sections?: AlphaZeroModulesCoursesPresentationCoursesGetSectionResponse[];
}

export interface AlphaZeroModulesCoursesPresentationCoursesGetSectionResponse {
  /** @format guid */
  id?: string;
  title?: string;
  /** @format int32 */
  order?: number;
  items?: AlphaZeroModulesCoursesPresentationCoursesGetItemResponse[];
}

export interface AlphaZeroModulesCoursesPresentationCoursesGetItemResponse {
  /** @format guid */
  id?: string;
  title?: string;
  type?: string;
  /** @format int32 */
  order?: number;
  /** @format int32 */
  bitIndex?: number;
  resources?: AlphaZeroModulesCoursesPresentationCoursesGetResourceResponse[];
}

export interface AlphaZeroModulesCoursesPresentationCoursesGetResourceResponse {
  arn?: string;
  type?: string;
  /** @format int32 */
  order?: number;
  metadata?: any;
}

export type AlphaZeroModulesCoursesPresentationCoursesGetGetCourseRequest =
  object;

export interface AlphaZeroModulesCoursesPresentationCoursesCreateCreateCourseResponse {
  /** @format guid */
  id?: string;
}

/** @example {"title":"Introduction to Algebra","description":"A basic course covering algebraic foundations.","subjectId":"00000000-0000-0000-0000-000000000001"} */
export interface AlphaZeroModulesCoursesPresentationCoursesCreateCreateCourseRequest {
  /** @example "Introduction to Algebra" */
  title?: string;
  /** @example "A basic course covering algebraic foundations." */
  description?: string | null;
  /**
   * @format guid
   * @example "00000000-0000-0000-0000-000000000001"
   */
  subjectId?: string;
}

export interface AlphaZeroModulesCoursesPresentationCoursesAddSectionAddSectionRequest {
  title?: string;
}

export interface AlphaZeroModulesCoursesPresentationCoursesAddItemAddLessonRequest {
  title?: string;
  /** @format guid */
  videoId?: string;
}

export interface AlphaZeroModulesCoursesPresentationCoursesAddItemAddAssessmentRequest {
  title?: string;
  /** @format guid */
  assessmentId?: string;
  type?: string;
  /** @format decimal */
  passingScore?: number;
  description?: string | null;
}

export interface AlphaZeroModulesCoursesApplicationAnalyticsQueriesGetCourseAnalyticsCourseAnalyticsDto {
  /** @format guid */
  courseId?: string;
  /** @format int32 */
  totalEnrollments?: number;
  /** @format double */
  averageCompletionPercentage?: number;
  itemCompletionRates?: AlphaZeroModulesCoursesApplicationAnalyticsQueriesGetCourseAnalyticsItemCompletionDto[];
}

export interface AlphaZeroModulesCoursesApplicationAnalyticsQueriesGetCourseAnalyticsItemCompletionDto {
  /** @format int32 */
  bitIndex?: number;
  /** @format int32 */
  completedCount?: number;
  /** @format double */
  completionPercentage?: number;
}

export type AlphaZeroModulesCoursesPresentationAnalyticsGetCourseAnalyticsRequest =
  object;

export interface AlphaZeroSharedQueriesPagedResultOfEnrollmentDto {
  items?: AlphaZeroModulesCoursesApplicationEnrollementsQueriesGetEnrollementEnrollmentDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesCoursesApplicationEnrollementsQueriesGetEnrollementEnrollmentDto {
  /** @format guid */
  id?: string;
  /** @format guid */
  studentId?: string;
  /** @format guid */
  courseId?: string;
  status?: string;
  /** @format double */
  completionPercentage?: number;
  /** @format date-time */
  enrolledOn?: string;
  /** @format guid */
  tenantId?: string;
}

export type AlphaZeroModulesCoursesPresentationAnalyticsListStudentProgressRequest =
  object;

export interface AlphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceResponse {
  /** @format guid */
  deviceId?: string;
}

export interface AlphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceRequest {
  deviceName?: string;
  platform?: AlphaZeroModulesIdentityDomainModelsDevicePlatform;
  publicKey?: string;
}

export interface AlphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceRequest {
  /** @format guid */
  deviceId?: string;
}

export interface AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalsByResourcePrincipalDto {
  /** @format guid */
  id?: string;
  username?: string;
  name?: string;
  principalType?: AlphaZeroModulesIdentityDomainModelsPrincipalsPrincipalType;
  principalScopeUrn?: string | null;
}

export type AlphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalsByResourceGetPrincipalsByResourceRequest =
  object;

export interface AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalPoliciesPrincipalPoliciesDto {
  /** @format guid */
  principalId?: string;
  inlinePolicies?: AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalPoliciesPolicyDto[];
  managedPolicies?: AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalPoliciesManagedPolicyDto[];
}

export interface AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalPoliciesPolicyDto {
  /** @format guid */
  id?: string;
  name?: string;
  statements?: AlphaZeroModulesIdentityDomainModelsPrincipalsPoliciesPolicyStatement[];
}

export interface AlphaZeroModulesIdentityDomainModelsPrincipalsPoliciesPolicyStatement {
  sid?: string;
  actions?: string[];
  effect?: boolean;
  resources?: AlphaZeroSharedDomainResourcePattern[];
  condition?: AlphaZeroModulesIdentityDomainModelsPrincipalsIConditionNode | null;
}

export interface AlphaZeroSharedDomainResourcePattern {
  value?: string;
  service?: string;
  tenantIdString?: string;
  resourcePath?: string;
}

export interface AlphaZeroModulesIdentityDomainModelsPrincipalsIConditionNode {
  type?: AlphaZeroModulesIdentityDomainModelsPrincipalsConditionType;
}

export interface AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalPoliciesManagedPolicyDto {
  /** @format guid */
  id?: string;
  name?: string;
  statements?: AlphaZeroModulesIdentityDomainModelsPrincipalsPoliciesManagedPolicyStatement[];
}

export interface AlphaZeroModulesIdentityDomainModelsPrincipalsPoliciesManagedPolicyStatement {
  sid?: string;
  actions?: string[];
  effect?: boolean;
  condition?: AlphaZeroModulesIdentityDomainModelsPrincipalsIConditionNode | null;
}

export type AlphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalPoliciesGetPrincipalPoliciesRequest =
  object;

export type AlphaZeroModulesIdentityPresentationPrincipalsCommandsDetachManagedPolicyDetachManagedPolicyRequest =
  object;

export type AlphaZeroModulesIdentityPresentationPrincipalsCommandsDetachInlinePolicyDetachInlinePolicyRequest =
  object;

export interface AlphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalResponse {
  /** @format guid */
  id?: string;
}

export interface AlphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalRequest {
  username?: string;
  password?: string;
  principalType?: AlphaZeroModulesIdentityDomainModelsPrincipalsPrincipalType;
  principalScope?: string | null;
  name?: string;
}

export type AlphaZeroModulesIdentityPresentationPrincipalsCommandsAttachManagedPolicyAttachManagedPolicyRequest =
  object;

export interface AlphaZeroModulesIdentityPresentationPrincipalsCommandsAttachInlinePolicyAttachInlinePolicyRequest {
  policyName?: string;
  statements?: AlphaZeroModulesIdentityDomainModelsPrincipalsPoliciesPolicyStatement[];
}

export type AlphaZeroModulesIdentityPresentationPoliciesCommandsDeleteManagedPolicyDeleteManagedPolicyRequest =
  object;

export interface AlphaZeroModulesIdentityPresentationPoliciesCommandsCreateManagedPolicyCreateManagedPolicyResponse {
  /** @format guid */
  id?: string;
}

export interface AlphaZeroModulesIdentityPresentationPoliciesCommandsCreateManagedPolicyCreateManagedPolicyRequest {
  name?: string;
  statements?: AlphaZeroModulesIdentityDomainModelsPrincipalsPoliciesManagedPolicyStatement[];
}

export interface AlphaZeroModulesIdentityApplicationAuthCommandsLoginAsTenantUserTokenResponse {
  token?: string;
  /** @format guid */
  tenantUserId?: string;
  /** @format guid */
  deviceId?: string | null;
}

export interface AlphaZeroModulesIdentityPresentationAuthCommandsLoginPrincipalLoginPrincipalRequest {
  /** @format guid */
  tenantId?: string;
  username?: string;
  password?: string;
}

export interface AlphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserRequest {
  /** @format guid */
  tenantId?: string;
  publicKey?: string;
  deviceName?: string;
  platform?: AlphaZeroModulesIdentityDomainModelsDevicePlatform;
}

export interface AlphaZeroSharedQueriesPagedResultOfRedemptionAuditLogDto {
  items?: AlphaZeroModulesLibraryApplicationRedemptionAuditLogsGetRedemptionLogsRedemptionAuditLogDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesLibraryApplicationRedemptionAuditLogsGetRedemptionLogsRedemptionAuditLogDto {
  /** @format guid */
  id?: string;
  /** @format guid */
  accessCodeId?: string;
  /** @format guid */
  libraryId?: string | null;
  /** @format guid */
  redeemedByUserId?: string;
  strategyId?: string;
  targetResourceArn?: string;
  /** @format date-time */
  redeemedAt?: string;
  ipAddress?: string | null;
  deviceFingerprint?: string | null;
}

export type AlphaZeroModulesLibraryPresentationEndpointsRedemptionAuditLogsGetRedemptionLogsRequest =
  object;

export interface AlphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeRequest {
  rawCode?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsLibrariesUpdateLibraryUpdateLibraryRequest {
  name?: string;
  address?: string;
  contactNumber?: string;
}

export interface AlphaZeroSharedQueriesPagedResultOfLibraryDto {
  items?: AlphaZeroModulesLibraryApplicationLibrariesQueriesGetLibraryLibraryDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesLibraryApplicationLibrariesQueriesGetLibraryLibraryDto {
  /** @format guid */
  id?: string;
  name?: string;
  address?: string;
  contactNumber?: string;
  allowedResources?: string[];
}

export type AlphaZeroModulesLibraryPresentationEndpointsLibrariesListLibrariesListLibrariesRequest =
  object;

export type AlphaZeroModulesLibraryPresentationEndpointsLibrariesGetLibraryGetLibraryRequest =
  object;

export type AlphaZeroModulesLibraryPresentationEndpointsLibrariesDeleteLibraryDeleteLibraryRequest =
  object;

export interface AlphaZeroModulesLibraryPresentationEndpointsLibrariesDeauthorizeResourceDeauthorizeResourceRequest {
  resourceArn?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsLibrariesCreateLibraryCreateLibraryResponse {
  /** @format guid */
  id?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsLibrariesCreateLibraryCreateLibraryRequest {
  name?: string;
  address?: string;
  contactNumber?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsLibrariesAuthorizeResourceAuthorizeResourceRequest {
  resourceArn?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsAccessCodesVoidCodeVoidCodeRequest {
  rawCode?: string;
  reason?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateBatchGenerateBatchResponse {
  codes?: string[];
}

export interface AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateBatchGenerateBatchRequest {
  /** @format int32 */
  quantity?: number;
  strategyId?: string;
  targetResourceArn?: string;
  metadata?: Record<string, any>;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateAdminCodeGenerateAdminCodeResponse {
  code?: string;
}

export interface AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateAdminCodeGenerateAdminCodeRequest {
  targetResourceArn?: string;
  metadata?: Record<string, any> | null;
}

export type AlphaZeroModulesLibraryPresentationEndpointsAccessCodesDistributeBatchDistributeBatchRequest =
  object;

export interface AlphaZeroModulesTenantsPresentationEndpointsUpdateTenantUpdateTenantRequest {
  name?: string;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
}

export interface AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantResponse {
  /** @format guid */
  id?: string;
  subdomain?: string;
  name?: string;
  branding?: AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantBranding;
}

export interface AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantBranding {
  primaryColor?: string | null;
  secondaryColor?: string | null;
  logoUrl?: string | null;
}

export type AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantRequest =
  object;

export interface AlphaZeroSharedQueriesPagedResultOfTenantDto {
  items?: AlphaZeroModulesTenantsApplicationTenantsQueriesGetTenantTenantDto[];
  /** @format int32 */
  totalCount?: number;
  /** @format int32 */
  currentPage?: number;
  /** @format int32 */
  pageSize?: number;
  /** @format int32 */
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface AlphaZeroModulesTenantsApplicationTenantsQueriesGetTenantTenantDto {
  /** @format guid */
  id?: string;
  name?: string;
  subdomain?: string;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  status?: string;
  /** @format date-time */
  createdAt?: string;
}

export type AlphaZeroModulesTenantsPresentationEndpointsListTenantsListTenantsRequest =
  object;

export type AlphaZeroModulesTenantsPresentationEndpointsGetTenantGetTenantRequest =
  object;

export type AlphaZeroModulesTenantsPresentationEndpointsDeleteTenantDeleteTenantRequest =
  object;

export interface AlphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantResponse {
  /** @format guid */
  id?: string;
}

export interface AlphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantRequest {
  name?: string;
  subdomain?: string;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
}

export type AlphaZeroModulesVideoUploadingPresentationFeaturesGetVideoKeyRequest =
  object;

export interface AlphaZeroModulesVideoUploadingPresentationFeaturesUpdateVideoInfoRequest {
  title?: string;
  description?: string | null;
}

export interface AlphaZeroModulesVideoUploadingPresentationFeaturesUploadRequest {
  fileName?: string;
  contentType?: string;
  title?: string;
  description?: string | null;
  transcodingMethod?: AlphaZeroModulesVideoUploadingApplicationVideoTranscodingMetehod | null;
  encryptionMethod?: AlphaZeroModulesVideoUploadingApplicationVideoEncryptionMethod | null;
  generateCustomThumbnailUrl?: boolean | null;
  targetResourceArn?: string | null;
}

export type QueryParamsType = Record<string | number, any>;
export type ResponseFormat = keyof Omit<Body, "body" | "bodyUsed">;

export interface FullRequestParams extends Omit<RequestInit, "body"> {
  /** set parameter to `true` for call `securityWorker` for this request */
  secure?: boolean;
  /** request path */
  path: string;
  /** content type of request body */
  type?: ContentType;
  /** query params */
  query?: QueryParamsType;
  /** format of response (i.e. response.json() -> format: "json") */
  format?: ResponseFormat;
  /** request body */
  body?: unknown;
  /** base url */
  baseUrl?: string;
  /** request cancellation token */
  cancelToken?: CancelToken;
}

export type RequestParams = Omit<
  FullRequestParams,
  "body" | "method" | "query" | "path"
>;

export interface ApiConfig<SecurityDataType = unknown> {
  baseUrl?: string;
  baseApiParams?: Omit<RequestParams, "baseUrl" | "cancelToken" | "signal">;
  securityWorker?: (
    securityData: SecurityDataType | null,
  ) => Promise<RequestParams | void> | RequestParams | void;
  customFetch?: typeof fetch;
}

export interface HttpResponse<D extends unknown, E extends unknown = unknown>
  extends Response {
  data: D;
  error: E;
}

type CancelToken = Symbol | string | number;

export enum ContentType {
  Json = "application/json",
  JsonApi = "application/vnd.api+json",
  FormData = "multipart/form-data",
  UrlEncoded = "application/x-www-form-urlencoded",
  Text = "text/plain",
}

export class HttpClient<SecurityDataType = unknown> {
  public baseUrl: string = "http://localhost:5053";
  private securityData: SecurityDataType | null = null;
  private securityWorker?: ApiConfig<SecurityDataType>["securityWorker"];
  private abortControllers = new Map<CancelToken, AbortController>();
  private customFetch = (...fetchParams: Parameters<typeof fetch>) =>
    fetch(...fetchParams);

  private baseApiParams: RequestParams = {
    credentials: "same-origin",
    headers: {},
    redirect: "follow",
    referrerPolicy: "no-referrer",
  };

  constructor(apiConfig: ApiConfig<SecurityDataType> = {}) {
    Object.assign(this, apiConfig);
  }

  public setSecurityData = (data: SecurityDataType | null) => {
    this.securityData = data;
  };

  protected encodeQueryParam(key: string, value: any) {
    const encodedKey = encodeURIComponent(key);
    return `${encodedKey}=${encodeURIComponent(typeof value === "number" ? value : `${value}`)}`;
  }

  protected addQueryParam(query: QueryParamsType, key: string) {
    return this.encodeQueryParam(key, query[key]);
  }

  protected addArrayQueryParam(query: QueryParamsType, key: string) {
    const value = query[key];
    return value.map((v: any) => this.encodeQueryParam(key, v)).join("&");
  }

  protected toQueryString(rawQuery?: QueryParamsType): string {
    const query = rawQuery || {};
    const keys = Object.keys(query).filter(
      (key) => "undefined" !== typeof query[key],
    );
    return keys
      .map((key) =>
        Array.isArray(query[key])
          ? this.addArrayQueryParam(query, key)
          : this.addQueryParam(query, key),
      )
      .join("&");
  }

  protected addQueryParams(rawQuery?: QueryParamsType): string {
    const queryString = this.toQueryString(rawQuery);
    return queryString ? `?${queryString}` : "";
  }

  private contentFormatters: Record<ContentType, (input: any) => any> = {
    [ContentType.Json]: (input: any) =>
      input !== null && (typeof input === "object" || typeof input === "string")
        ? JSON.stringify(input)
        : input,
    [ContentType.JsonApi]: (input: any) =>
      input !== null && (typeof input === "object" || typeof input === "string")
        ? JSON.stringify(input)
        : input,
    [ContentType.Text]: (input: any) =>
      input !== null && typeof input !== "string"
        ? JSON.stringify(input)
        : input,
    [ContentType.FormData]: (input: any) => {
      if (input instanceof FormData) {
        return input;
      }

      return Object.keys(input || {}).reduce((formData, key) => {
        const property = input[key];
        formData.append(
          key,
          property instanceof Blob
            ? property
            : typeof property === "object" && property !== null
              ? JSON.stringify(property)
              : `${property}`,
        );
        return formData;
      }, new FormData());
    },
    [ContentType.UrlEncoded]: (input: any) => this.toQueryString(input),
  };

  protected mergeRequestParams(
    params1: RequestParams,
    params2?: RequestParams,
  ): RequestParams {
    return {
      ...this.baseApiParams,
      ...params1,
      ...(params2 || {}),
      headers: {
        ...(this.baseApiParams.headers || {}),
        ...(params1.headers || {}),
        ...((params2 && params2.headers) || {}),
      },
    };
  }

  protected createAbortSignal = (
    cancelToken: CancelToken,
  ): AbortSignal | undefined => {
    if (this.abortControllers.has(cancelToken)) {
      const abortController = this.abortControllers.get(cancelToken);
      if (abortController) {
        return abortController.signal;
      }
      return void 0;
    }

    const abortController = new AbortController();
    this.abortControllers.set(cancelToken, abortController);
    return abortController.signal;
  };

  public abortRequest = (cancelToken: CancelToken) => {
    const abortController = this.abortControllers.get(cancelToken);

    if (abortController) {
      abortController.abort();
      this.abortControllers.delete(cancelToken);
    }
  };

  public request = async <T = any, E = any>({
    body,
    secure,
    path,
    type,
    query,
    format,
    baseUrl,
    cancelToken,
    ...params
  }: FullRequestParams): Promise<HttpResponse<T, E>> => {
    const secureParams =
      ((typeof secure === "boolean" ? secure : this.baseApiParams.secure) &&
        this.securityWorker &&
        (await this.securityWorker(this.securityData))) ||
      {};
    const requestParams = this.mergeRequestParams(params, secureParams);
    const queryString = query && this.toQueryString(query);
    const payloadFormatter = this.contentFormatters[type || ContentType.Json];
    const responseFormat = format || requestParams.format;

    return this.customFetch(
      `${baseUrl || this.baseUrl || ""}${path}${queryString ? `?${queryString}` : ""}`,
      {
        ...requestParams,
        headers: {
          ...(requestParams.headers || {}),
          ...(type && type !== ContentType.FormData
            ? { "Content-Type": type }
            : {}),
        },
        signal:
          (cancelToken
            ? this.createAbortSignal(cancelToken)
            : requestParams.signal) || null,
        body:
          typeof body === "undefined" || body === null
            ? null
            : payloadFormatter(body),
      },
    ).then(async (response) => {
      const r = response as HttpResponse<T, E>;
      r.data = null as unknown as T;
      r.error = null as unknown as E;

      const responseToParse = responseFormat ? response.clone() : response;
      const data = !responseFormat
        ? r
        : await responseToParse[responseFormat]()
            .then((data) => {
              if (r.ok) {
                r.data = data;
              } else {
                r.error = data;
              }
              return r;
            })
            .catch((e) => {
              r.error = e;
              return r;
            });

      if (cancelToken) {
        this.abortControllers.delete(cancelToken);
      }

      if (!response.ok) throw data;
      return data;
    });
  };
}

/**
 * @title Alpha Zero
 * @version 1.0.0
 * @baseUrl http://localhost:5053
 */
export class Api<
  SecurityDataType extends unknown,
> extends HttpClient<SecurityDataType> {
  assessments = {
    /**
     * No description
     *
     * @tags Assessments, Assessments
     * @name AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentEndpoint
     * @request POST:/assessments/submissions/{submissionId}/submit
     * @secure
     */
    alphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentEndpoint:
      (
        submissionId: string,
        data: AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsSubmitSubmitAssessmentResponse,
          void
        >({
          path: `/assessments/submissions/${submissionId}/submit`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Submissions, Assessments
     * @name AlphaZeroModulesAssessmentsPresentationEndpointsSubmissionsListListSubmissionsEndpoint
     * @request GET:/assessments/submissions
     * @secure
     */
    alphaZeroModulesAssessmentsPresentationEndpointsSubmissionsListListSubmissionsEndpoint:
      (
        query: {
          /** @format guid */
          assessmentId?: string | null;
          status?: string | null;
          /** @format int32 */
          page: number;
          /** @format int32 */
          perPage: number;
        },
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroSharedQueriesPagedResultOfSubmissionSummaryDto,
          FastEndpointsProblemDetails
        >({
          path: `/assessments/submissions`,
          method: "GET",
          query: query,
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Assessments, Assessments
     * @name AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsUpdateContentUpdateAssessmentContentEndpoint
     * @request PUT:/assessments/{assessmentId}/content
     * @secure
     */
    alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsUpdateContentUpdateAssessmentContentEndpoint:
      (
        assessmentId: string,
        data: AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsUpdateContentUpdateAssessmentContentRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/assessments/${assessmentId}/content`,
          method: "PUT",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * @description Returns a paged list of assessments for the current tenant.
     *
     * @tags Assessments, Assessments
     * @name AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsListListAssessmentsEndpoint
     * @summary Lists all assessments with pagination
     * @request GET:/assessments
     */
    alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsListListAssessmentsEndpoint:
      (
        query: {
          /** @format int32 */
          page: number;
          /** @format int32 */
          perPage: number;
        },
        params: RequestParams = {},
      ) =>
        this.request<AlphaZeroSharedQueriesPagedResultOfAssessmentDto, any>({
          path: `/assessments`,
          method: "GET",
          query: query,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Assessments, Assessments
     * @name AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentEndpoint
     * @request POST:/assessments
     * @secure
     */
    alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentEndpoint:
      (
        data: AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsCreateCreateAssessmentResponse,
          void
        >({
          path: `/assessments`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * @description Returns full details of an assessment including its current content snapshot or a specific version.
     *
     * @tags Assessments, Assessments
     * @name AlphaZeroModulesAssessmentsPresentationEndpointsAssessmentsGetGetAssessmentEndpoint
     * @summary Retrieves a specific assessment by ID
     * @request GET:/assessments/{id}
     */
    alphaZeroModulesAssessmentsPresentationEndpointsAssessmentsGetGetAssessmentEndpoint:
      (
        id: string,
        query?: {
          /** @format int32 */
          version?: number | null;
        },
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesAssessmentsApplicationAssessmentsQueriesGetAssessmentAssessmentDetailsDto,
          void
        >({
          path: `/assessments/${id}`,
          method: "GET",
          query: query,
          format: "json",
          ...params,
        }),
  };
  courses = {
    /**
     * No description
     *
     * @tags Enrollement, Courses
     * @name AlphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemEndpoint
     * @request POST:/courses/enrollements/{enrollmentId}/complete
     * @secure
     */
    alphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemEndpoint:
      (
        enrollmentId: string,
        data: AlphaZeroModulesIdentityPresentationEnrollementsCompleteItemCompleteItemRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/courses/enrollements/${enrollmentId}/complete`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Subjects, Courses
     * @name AlphaZeroModulesCoursesPresentationSubjectsListListSubjectsEndpoint
     * @request GET:/courses/subjects
     * @secure
     */
    alphaZeroModulesCoursesPresentationSubjectsListListSubjectsEndpoint: (
      query: {
        /** @format int32 */
        page: number;
        /** @format int32 */
        perPage: number;
      },
      params: RequestParams = {},
    ) =>
      this.request<AlphaZeroSharedQueriesPagedResultOfSubjectDto, void>({
        path: `/courses/subjects`,
        method: "GET",
        query: query,
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * @description Initializes a subject category (e.g., Physics, Chemistry) for the current tenant.
     *
     * @tags Subjects, Courses
     * @name AlphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectEndpoint
     * @summary Creates a new educational subject
     * @request POST:/courses/subjects
     * @secure
     */
    alphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectEndpoint: (
      data: AlphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectRequest,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesCoursesPresentationSubjectsCreateCreateSubjectResponse,
        void
      >({
        path: `/courses/subjects`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Subjects, Courses
     * @name AlphaZeroModulesCoursesPresentationSubjectsGetGetSubjectEndpoint
     * @request GET:/courses/subjects/{id}
     * @secure
     */
    alphaZeroModulesCoursesPresentationSubjectsGetGetSubjectEndpoint: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesCoursesApplicationSubjectsQueriesGetSubjectSubjectDto,
        void
      >({
        path: `/courses/subjects/${id}`,
        method: "GET",
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Enrollment, Courses
     * @name AlphaZeroModulesCoursesPresentationEnrollementsGetGetEnrollementEndpoint
     * @request GET:/courses/enrollments/{id}
     * @secure
     */
    alphaZeroModulesCoursesPresentationEnrollementsGetGetEnrollementEndpoint: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesCoursesPresentationEnrollementsGetEnrollementResponse,
        void
      >({
        path: `/courses/enrollments/${id}`,
        method: "GET",
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * @description Creates a new enrollment record and initializes the progress bitmask for the student.
     *
     * @tags Enrollment, Courses
     * @name AlphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseEndpoint
     * @summary Enrolls a student in a course
     * @request POST:/courses/enroll
     * @secure
     */
    alphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseEndpoint:
      (
        data: AlphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesCoursesPresentationEnrollementsEnrollEnrollInCourseResponse,
          void
        >({
          path: `/courses/enroll`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * @description Returns a list of all active course enrollments for the student, grouped by the academy (tenant) they belong to.
     *
     * @tags Enrollment, Courses
     * @name AlphaZeroModulesCoursesPresentationEnrollementsDashboardGetStudentDashboardEndpoint
     * @summary Retrieves student's learning dashboard across all academies
     * @request GET:/courses/dashboard/{studentId}
     * @secure
     */
    alphaZeroModulesCoursesPresentationEnrollementsDashboardGetStudentDashboardEndpoint:
      (studentId: string, params: RequestParams = {}) =>
        this.request<
          AlphaZeroModulesCoursesPresentationEnrollementsDashboardDashboardResponse,
          void
        >({
          path: `/courses/dashboard/${studentId}`,
          method: "GET",
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesStateApproveCourseEndpoint
     * @request PATCH:/courses/{courseId}/approve
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesStateApproveCourseEndpoint: (
      courseId: string,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/approve`,
        method: "PATCH",
        secure: true,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesStatePublishCourseEndpoint
     * @request PATCH:/courses/{courseId}/publish
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesStatePublishCourseEndpoint: (
      courseId: string,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/publish`,
        method: "PATCH",
        secure: true,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesStateRejectCourseEndpoint
     * @request PATCH:/courses/{courseId}/reject
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesStateRejectCourseEndpoint: (
      courseId: string,
      data: AlphaZeroModulesCoursesPresentationCoursesStateRejectCourseRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/reject`,
        method: "PATCH",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesStateSubmitForReviewEndpoint
     * @request PATCH:/courses/{courseId}/review
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesStateSubmitForReviewEndpoint: (
      courseId: string,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/review`,
        method: "PATCH",
        secure: true,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesReorderSectionsReorderSectionsEndpoint
     * @request POST:/courses/{courseId}/sections/reorder
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesReorderSectionsReorderSectionsEndpoint:
      (
        courseId: string,
        data: AlphaZeroModulesCoursesPresentationCoursesReorderSectionsReorderSectionsRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/courses/${courseId}/sections/reorder`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesReorderItemsReorderItemsEndpoint
     * @request POST:/courses/{courseId}/sections/{sectionId}/reorder
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesReorderItemsReorderItemsEndpoint:
      (
        courseId: string,
        sectionId: string,
        data: AlphaZeroModulesCoursesPresentationCoursesReorderItemsReorderItemsRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/courses/${courseId}/sections/${sectionId}/reorder`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesPlansUpdatePlanUpdatePlanEndpoint
     * @request PUT:/courses/{courseId}/plans/{planId}
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesPlansUpdatePlanUpdatePlanEndpoint:
      (
        courseId: string,
        planId: string,
        data: AlphaZeroModulesCoursesPresentationCoursesPlansUpdatePlanUpdatePlanRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/courses/${courseId}/plans/${planId}`,
          method: "PUT",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesPlansRemovePlanRemovePlanEndpoint
     * @request DELETE:/courses/{courseId}/plans/{planId}
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesPlansRemovePlanRemovePlanEndpoint:
      (courseId: string, planId: string, params: RequestParams = {}) =>
        this.request<void, void>({
          path: `/courses/${courseId}/plans/${planId}`,
          method: "DELETE",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesPlansAddPlanAddPlanEndpoint
     * @request POST:/courses/{courseId}/plans
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesPlansAddPlanAddPlanEndpoint: (
      courseId: string,
      data: AlphaZeroModulesCoursesPresentationCoursesPlansAddPlanAddPlanRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/plans`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * @description Returns a paged list of courses for the current tenant. Optionally filterable by subject.
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesListListCoursesEndpoint
     * @summary Lists all courses with pagination
     * @request GET:/courses
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesListListCoursesEndpoint: (
      query: {
        /** @format guid */
        subjectId?: string | null;
        /** @format int32 */
        page: number;
        /** @format int32 */
        perPage: number;
      },
      params: RequestParams = {},
    ) =>
      this.request<AlphaZeroSharedQueriesPagedResultOfCourseSummaryDto, void>({
        path: `/courses`,
        method: "GET",
        query: query,
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * @description Creates a course in Draft status under a specific subject.
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesCreateCreateCourseEndpoint
     * @summary Initializes a new course
     * @request POST:/courses
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesCreateCreateCourseEndpoint: (
      data: AlphaZeroModulesCoursesPresentationCoursesCreateCreateCourseRequest,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesCoursesPresentationCoursesCreateCreateCourseResponse,
        void
      >({
        path: `/courses`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Returns the complete structure of a course, including all sections, lessons, and assessments.
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesGetGetCourseEndpoint
     * @summary Retrieves a course by its ID
     * @request GET:/courses/{id}
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesGetGetCourseEndpoint: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesCoursesPresentationCoursesGetCourseResponse,
        void
      >({
        path: `/courses/${id}`,
        method: "GET",
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesAddSectionAddSectionEndpoint
     * @request POST:/courses/{courseId}/sections
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesAddSectionAddSectionEndpoint: (
      courseId: string,
      data: AlphaZeroModulesCoursesPresentationCoursesAddSectionAddSectionRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/sections`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesAddItemAddLessonEndpoint
     * @request POST:/courses/{courseId}/sections/{sectionId}/lessons
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesAddItemAddLessonEndpoint: (
      courseId: string,
      sectionId: string,
      data: AlphaZeroModulesCoursesPresentationCoursesAddItemAddLessonRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/sections/${sectionId}/lessons`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Courses, Courses
     * @name AlphaZeroModulesCoursesPresentationCoursesAddItemAddAssessmentEndpoint
     * @request POST:/courses/{courseId}/sections/{sectionId}/assessments
     * @secure
     */
    alphaZeroModulesCoursesPresentationCoursesAddItemAddAssessmentEndpoint: (
      courseId: string,
      sectionId: string,
      data: AlphaZeroModulesCoursesPresentationCoursesAddItemAddAssessmentRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/courses/${courseId}/sections/${sectionId}/assessments`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * @description Returns total enrollments, average completion rate, and per-item completion stats.
     *
     * @tags Analytics, Courses
     * @name AlphaZeroModulesCoursesPresentationAnalyticsGetCourseAnalyticsEndpoint
     * @summary Retrieves analytics for a course
     * @request GET:/courses/{courseId}/analytics
     * @secure
     */
    alphaZeroModulesCoursesPresentationAnalyticsGetCourseAnalyticsEndpoint: (
      courseId: string,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesCoursesApplicationAnalyticsQueriesGetCourseAnalyticsCourseAnalyticsDto,
        void
      >({
        path: `/courses/${courseId}/analytics`,
        method: "GET",
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * @description Returns a paginated list of enrollments and their completion percentages.
     *
     * @tags Analytics, Courses
     * @name AlphaZeroModulesCoursesPresentationAnalyticsListStudentProgressEndpoint
     * @summary Lists student progress for a course
     * @request GET:/courses/{courseId}/students
     * @secure
     */
    alphaZeroModulesCoursesPresentationAnalyticsListStudentProgressEndpoint: (
      courseId: string,
      query: {
        /** @format int32 */
        page: number;
        /** @format int32 */
        perPage: number;
      },
      params: RequestParams = {},
    ) =>
      this.request<AlphaZeroSharedQueriesPagedResultOfEnrollmentDto, void>({
        path: `/courses/${courseId}/students`,
        method: "GET",
        query: query,
        secure: true,
        format: "json",
        ...params,
      }),
  };
  identity = {
    /**
     * No description
     *
     * @tags Devices, Identity
     * @name AlphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceEndpoint
     * @request POST:/identity/users/devices
     * @secure
     */
    alphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceEndpoint: (
      data: AlphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceRequest,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesIdentityPresentationUsersDevicesRegisterDeviceResponse,
        void
      >({
        path: `/identity/users/devices`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Devices, Identity
     * @name AlphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint
     * @request POST:/identity/users/devices/main
     * @secure
     */
    alphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceEndpoint: (
      data: AlphaZeroModulesIdentityPresentationUsersDevicesSetMainDeviceRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/identity/users/devices/main`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalsByResourceGetPrincipalsByResourceEndpoint
     * @request GET:/identity/resources/{resourceType}/{resourceId}/principals
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalsByResourceGetPrincipalsByResourceEndpoint:
      (resourceType: string, resourceId: string, params: RequestParams = {}) =>
        this.request<
          AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalsByResourcePrincipalDto[],
          void
        >({
          path: `/identity/resources/${resourceType}/${resourceId}/principals`,
          method: "GET",
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalPoliciesGetPrincipalPoliciesEndpoint
     * @request GET:/identity/principals/{principalId}/policies
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsQueriesGetPrincipalPoliciesGetPrincipalPoliciesEndpoint:
      (principalId: string, params: RequestParams = {}) =>
        this.request<
          AlphaZeroModulesIdentityApplicationPrincipalsQueriesGetPrincipalPoliciesPrincipalPoliciesDto,
          void
        >({
          path: `/identity/principals/${principalId}/policies`,
          method: "GET",
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsCommandsDetachManagedPolicyDetachManagedPolicyEndpoint
     * @request DELETE:/identity/principals/{principalId}/policies/managed/{managedPolicyId}
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsCommandsDetachManagedPolicyDetachManagedPolicyEndpoint:
      (
        principalId: string,
        managedPolicyId: string,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/identity/principals/${principalId}/policies/managed/${managedPolicyId}`,
          method: "DELETE",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsCommandsAttachManagedPolicyAttachManagedPolicyEndpoint
     * @request POST:/identity/principals/{principalId}/policies/managed/{managedPolicyId}
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsCommandsAttachManagedPolicyAttachManagedPolicyEndpoint:
      (
        principalId: string,
        managedPolicyId: string,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/identity/principals/${principalId}/policies/managed/${managedPolicyId}`,
          method: "POST",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsCommandsDetachInlinePolicyDetachInlinePolicyEndpoint
     * @request DELETE:/identity/principals/{principalId}/policies/inline/{policyId}
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsCommandsDetachInlinePolicyDetachInlinePolicyEndpoint:
      (principalId: string, policyId: string, params: RequestParams = {}) =>
        this.request<void, void>({
          path: `/identity/principals/${principalId}/policies/inline/${policyId}`,
          method: "DELETE",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalEndpoint
     * @request POST:/identity/principals
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalEndpoint:
      (
        data: AlphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesIdentityPresentationPrincipalsCommandsCreatePrincipalCreatePrincipalResponse,
          void
        >({
          path: `/identity/principals`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity, Identity
     * @name AlphaZeroModulesIdentityPresentationPrincipalsCommandsAttachInlinePolicyAttachInlinePolicyEndpoint
     * @request POST:/identity/principals/{principalId}/policies/inline
     * @secure
     */
    alphaZeroModulesIdentityPresentationPrincipalsCommandsAttachInlinePolicyAttachInlinePolicyEndpoint:
      (
        principalId: string,
        data: AlphaZeroModulesIdentityPresentationPrincipalsCommandsAttachInlinePolicyAttachInlinePolicyRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/identity/principals/${principalId}/policies/inline`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity Policies, Identity
     * @name AlphaZeroModulesIdentityPresentationPoliciesCommandsDeleteManagedPolicyDeleteManagedPolicyEndpoint
     * @request DELETE:/identity/policies/managed/{policyId}
     * @secure
     */
    alphaZeroModulesIdentityPresentationPoliciesCommandsDeleteManagedPolicyDeleteManagedPolicyEndpoint:
      (policyId: string, params: RequestParams = {}) =>
        this.request<void, void>({
          path: `/identity/policies/managed/${policyId}`,
          method: "DELETE",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity Policies, Identity
     * @name AlphaZeroModulesIdentityPresentationPoliciesCommandsCreateManagedPolicyCreateManagedPolicyEndpoint
     * @request POST:/identity/policies/managed
     * @secure
     */
    alphaZeroModulesIdentityPresentationPoliciesCommandsCreateManagedPolicyCreateManagedPolicyEndpoint:
      (
        data: AlphaZeroModulesIdentityPresentationPoliciesCommandsCreateManagedPolicyCreateManagedPolicyRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesIdentityPresentationPoliciesCommandsCreateManagedPolicyCreateManagedPolicyResponse,
          void
        >({
          path: `/identity/policies/managed`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity Auth, Identity
     * @name AlphaZeroModulesIdentityPresentationAuthCommandsLoginPrincipalLoginPrincipalEndpoint
     * @request POST:/identity/auth/login-principal
     */
    alphaZeroModulesIdentityPresentationAuthCommandsLoginPrincipalLoginPrincipalEndpoint:
      (
        data: AlphaZeroModulesIdentityPresentationAuthCommandsLoginPrincipalLoginPrincipalRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesIdentityApplicationAuthCommandsLoginAsTenantUserTokenResponse,
          any
        >({
          path: `/identity/auth/login-principal`,
          method: "POST",
          body: data,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Identity Auth, Identity
     * @name AlphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint
     * @request POST:/identity/auth/exchange-tenant-token
     * @secure
     */
    alphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserEndpoint:
      (
        data: AlphaZeroModulesIdentityPresentationAuthCommandsLoginAsTenantUserLoginAsTenantUserRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesIdentityApplicationAuthCommandsLoginAsTenantUserTokenResponse,
          void
        >({
          path: `/identity/auth/exchange-tenant-token`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),
  };
  library = {
    /**
     * No description
     *
     * @tags Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsRedemptionAuditLogsGetRedemptionLogsEndpoint
     * @request GET:/library/libraries/{libraryId}/audit-logs
     */
    alphaZeroModulesLibraryPresentationEndpointsRedemptionAuditLogsGetRedemptionLogsEndpoint:
      (
        libraryId: string,
        query?: {
          /** @format date */
          from?: string | null;
          /** @format date */
          to?: string | null;
          /**
           * @format int32
           * @default 1
           */
          page?: number;
          /**
           * @format int32
           * @default 50
           */
          pageSize?: number;
        },
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroSharedQueriesPagedResultOfRedemptionAuditLogDto,
          any
        >({
          path: `/library/libraries/${libraryId}/audit-logs`,
          method: "GET",
          query: query,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint
     * @request POST:/library/redeem
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeEndpoint: (
      data: AlphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/library/redeem`,
        method: "POST",
        body: data,
        secure: true,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesUpdateLibraryUpdateLibraryEndpoint
     * @request PATCH:/library/libraries/{id}
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesUpdateLibraryUpdateLibraryEndpoint:
      (
        id: string,
        data: AlphaZeroModulesLibraryPresentationEndpointsLibrariesUpdateLibraryUpdateLibraryRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/library/libraries/${id}`,
          method: "PATCH",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesGetLibraryGetLibraryEndpoint
     * @request GET:/library/libraries/{id}
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesGetLibraryGetLibraryEndpoint:
      (id: string, params: RequestParams = {}) =>
        this.request<
          AlphaZeroModulesLibraryApplicationLibrariesQueriesGetLibraryLibraryDto,
          void
        >({
          path: `/library/libraries/${id}`,
          method: "GET",
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesDeleteLibraryDeleteLibraryEndpoint
     * @request DELETE:/library/libraries/{id}
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesDeleteLibraryDeleteLibraryEndpoint:
      (id: string, params: RequestParams = {}) =>
        this.request<void, void>({
          path: `/library/libraries/${id}`,
          method: "DELETE",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesListLibrariesListLibrariesEndpoint
     * @request GET:/library/libraries
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesListLibrariesListLibrariesEndpoint:
      (
        query: {
          /** @format int32 */
          page: number;
          /** @format int32 */
          perPage: number;
        },
        params: RequestParams = {},
      ) =>
        this.request<AlphaZeroSharedQueriesPagedResultOfLibraryDto, void>({
          path: `/library/libraries`,
          method: "GET",
          query: query,
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesCreateLibraryCreateLibraryEndpoint
     * @request POST:/library/libraries
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesCreateLibraryCreateLibraryEndpoint:
      (
        data: AlphaZeroModulesLibraryPresentationEndpointsLibrariesCreateLibraryCreateLibraryRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesLibraryPresentationEndpointsLibrariesCreateLibraryCreateLibraryResponse,
          void
        >({
          path: `/library/libraries`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesDeauthorizeResourceDeauthorizeResourceEndpoint
     * @request DELETE:/library/libraries/{id}/resources
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesDeauthorizeResourceDeauthorizeResourceEndpoint:
      (
        id: string,
        data: AlphaZeroModulesLibraryPresentationEndpointsLibrariesDeauthorizeResourceDeauthorizeResourceRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/library/libraries/${id}/resources`,
          method: "DELETE",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsLibrariesAuthorizeResourceAuthorizeResourceEndpoint
     * @request POST:/library/libraries/{id}/resources
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsLibrariesAuthorizeResourceAuthorizeResourceEndpoint:
      (
        id: string,
        data: AlphaZeroModulesLibraryPresentationEndpointsLibrariesAuthorizeResourceAuthorizeResourceRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/library/libraries/${id}/resources`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsAccessCodesVoidCodeVoidCodeEndpoint
     * @request POST:/library/access-codes/void
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsAccessCodesVoidCodeVoidCodeEndpoint:
      (
        data: AlphaZeroModulesLibraryPresentationEndpointsAccessCodesVoidCodeVoidCodeRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/library/access-codes/void`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateBatchGenerateBatchEndpoint
     * @request POST:/library/libraries/{libraryId}/access-codes/generate
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateBatchGenerateBatchEndpoint:
      (
        libraryId: string,
        data: AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateBatchGenerateBatchRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateBatchGenerateBatchResponse,
          void
        >({
          path: `/library/libraries/${libraryId}/access-codes/generate`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library Management, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateAdminCodeGenerateAdminCodeEndpoint
     * @request POST:/library/admin/access-codes/generate-single
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateAdminCodeGenerateAdminCodeEndpoint:
      (
        data: AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateAdminCodeGenerateAdminCodeRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesLibraryPresentationEndpointsAccessCodesGenerateAdminCodeGenerateAdminCodeResponse,
          void
        >({
          path: `/library/admin/access-codes/generate-single`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Library, Library
     * @name AlphaZeroModulesLibraryPresentationEndpointsAccessCodesDistributeBatchDistributeBatchEndpoint
     * @request POST:/library/access-codes/batches/{batchId}/distribute
     * @secure
     */
    alphaZeroModulesLibraryPresentationEndpointsAccessCodesDistributeBatchDistributeBatchEndpoint:
      (batchId: string, params: RequestParams = {}) =>
        this.request<void, void>({
          path: `/library/access-codes/batches/${batchId}/distribute`,
          method: "POST",
          secure: true,
          ...params,
        }),
  };
  tenants = {
    /**
     * No description
     *
     * @tags Tenants, Tenants
     * @name AlphaZeroModulesTenantsPresentationEndpointsUpdateTenantUpdateTenantEndpoint
     * @request PUT:/tenants/{id}
     * @secure
     */
    alphaZeroModulesTenantsPresentationEndpointsUpdateTenantUpdateTenantEndpoint:
      (
        id: string,
        data: AlphaZeroModulesTenantsPresentationEndpointsUpdateTenantUpdateTenantRequest,
        params: RequestParams = {},
      ) =>
        this.request<void, void>({
          path: `/tenants/${id}`,
          method: "PUT",
          body: data,
          secure: true,
          type: ContentType.Json,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Tenants, Tenants
     * @name AlphaZeroModulesTenantsPresentationEndpointsGetTenantGetTenantEndpoint
     * @request GET:/tenants/{id}
     * @secure
     */
    alphaZeroModulesTenantsPresentationEndpointsGetTenantGetTenantEndpoint: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<
        AlphaZeroModulesTenantsApplicationTenantsQueriesGetTenantTenantDto,
        void
      >({
        path: `/tenants/${id}`,
        method: "GET",
        secure: true,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Tenants, Tenants
     * @name AlphaZeroModulesTenantsPresentationEndpointsDeleteTenantDeleteTenantEndpoint
     * @request DELETE:/tenants/{id}
     * @secure
     */
    alphaZeroModulesTenantsPresentationEndpointsDeleteTenantDeleteTenantEndpoint:
      (id: string, params: RequestParams = {}) =>
        this.request<void, void>({
          path: `/tenants/${id}`,
          method: "DELETE",
          secure: true,
          ...params,
        }),

    /**
     * No description
     *
     * @tags Tenants, Tenants
     * @name AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint
     * @request GET:/tenants/lookup
     */
    alphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantEndpoint:
      (
        query: {
          subdomain: string;
        },
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantResponse,
          FastEndpointsProblemDetails
        >({
          path: `/tenants/lookup`,
          method: "GET",
          query: query,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Tenants, Tenants
     * @name AlphaZeroModulesTenantsPresentationEndpointsListTenantsListTenantsEndpoint
     * @request GET:/tenants
     * @secure
     */
    alphaZeroModulesTenantsPresentationEndpointsListTenantsListTenantsEndpoint:
      (
        query: {
          q?: string | null;
          /** @format int32 */
          page: number;
          /** @format int32 */
          perPage: number;
        },
        params: RequestParams = {},
      ) =>
        this.request<AlphaZeroSharedQueriesPagedResultOfTenantDto, void>({
          path: `/tenants`,
          method: "GET",
          query: query,
          secure: true,
          format: "json",
          ...params,
        }),

    /**
     * No description
     *
     * @tags Tenants, Tenants
     * @name AlphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantEndpoint
     * @request POST:/tenants
     * @secure
     */
    alphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantEndpoint:
      (
        data: AlphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantRequest,
        params: RequestParams = {},
      ) =>
        this.request<
          AlphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantResponse,
          void
        >({
          path: `/tenants`,
          method: "POST",
          body: data,
          secure: true,
          type: ContentType.Json,
          format: "json",
          ...params,
        }),
  };
  api = {
    /**
     * No description
     *
     * @tags Video Streaming, Api
     * @name AlphaZeroModulesVideoUploadingPresentationFeaturesGetVideoKeyEndpoint
     * @request GET:/api/video/keys/{videoId}
     * @secure
     */
    alphaZeroModulesVideoUploadingPresentationFeaturesGetVideoKeyEndpoint: (
      videoId: string,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/api/video/keys/${videoId}`,
        method: "GET",
        secure: true,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Uploading Debug
     * @name GetApiVideoUploadingDebugVideos
     * @request GET:/api/video-uploading/debug/videos
     */
    getApiVideoUploadingDebugVideos: (
      query?: {
        /** @format int32 */
        page?: number | null;
        /** @format int32 */
        perPage?: number | null;
      },
      params: RequestParams = {},
    ) =>
      this.request<void, any>({
        path: `/api/video-uploading/debug/videos`,
        method: "GET",
        query: query,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Uploading Debug
     * @name GetApiVideoUploadingDebugVideos2
     * @request GET:/api/video-uploading/debug/videos/{id}
     */
    getApiVideoUploadingDebugVideos2: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<void, any>({
        path: `/api/video-uploading/debug/videos/${id}`,
        method: "GET",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Uploading Debug
     * @name DeleteApiVideoUploadingDebugVideos
     * @request DELETE:/api/video-uploading/debug/videos/{id}
     */
    deleteApiVideoUploadingDebugVideos: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<void, any>({
        path: `/api/video-uploading/debug/videos/${id}`,
        method: "DELETE",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Uploading Debug
     * @name PatchApiVideoUploadingDebugVideos
     * @request PATCH:/api/video-uploading/debug/videos/{id}
     */
    patchApiVideoUploadingDebugVideos: (
      id: string,
      data: AlphaZeroModulesVideoUploadingPresentationFeaturesUpdateVideoInfoRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, any>({
        path: `/api/video-uploading/debug/videos/${id}`,
        method: "PATCH",
        body: data,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Uploading Debug
     * @name GetApiVideoUploadingDebugVideosState
     * @request GET:/api/video-uploading/debug/videos/{id}/state
     */
    getApiVideoUploadingDebugVideosState: (
      id: string,
      params: RequestParams = {},
    ) =>
      this.request<void, any>({
        path: `/api/video-uploading/debug/videos/${id}/state`,
        method: "GET",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Uploading
     * @name PostApiVideoUploadingUpload
     * @request POST:/api/video-uploading/upload
     */
    postApiVideoUploadingUpload: (
      data: AlphaZeroModulesVideoUploadingPresentationFeaturesUploadRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, any>({
        path: `/api/video-uploading/upload`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Video Streaming
     * @name GetApiVideo
     * @request GET:/api/video/{videoId}
     */
    getApiVideo: (videoId: string, params: RequestParams = {}) =>
      this.request<void, any>({
        path: `/api/video/${videoId}`,
        method: "GET",
        ...params,
      }),
  };
  users = {
    /**
     * No description
     *
     * @name GetUsersMe
     * @request GET:/users/me
     * @secure
     */
    getUsersMe: (params: RequestParams = {}) =>
      this.request<Record<string, string>, any>({
        path: `/users/me`,
        method: "GET",
        secure: true,
        format: "json",
        ...params,
      }),
  };
}
