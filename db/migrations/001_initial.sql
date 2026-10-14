-- TrackLeadFlow initial schema.
-- Planned in docs/banco-de-dados.md for SQL Server and applied on PostgreSQL.
-- uuid replaces uniqueidentifier. timestamptz replaces datetimeoffset.
-- boolean replaces bit. numeric(19,4) replaces decimal(19,4).
-- RowVersion is a bigint the application increments. PostgreSQL has no rowversion.
-- Foreign keys stay inside a schema. Copied ids from other modules have no foreign key.
-- Users.Phone is the specialist number from the accepted triage spec.

create schema if not exists identity;
create schema if not exists whatsapp;
create schema if not exists qualification;
create schema if not exists followup;
create schema if not exists attendance;
create schema if not exists payments;
create schema if not exists marketing;

create table if not exists identity."Tenants" (
    "Id" uuid primary key,
    "Name" varchar(200) not null,
    "Status" varchar(32) not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "CK_Tenants_Status" check ("Status" in ('Active', 'Suspended'))
);

create table if not exists identity."Users" (
    "Id" uuid primary key,
    "TenantId" uuid not null references identity."Tenants" ("Id"),
    "Email" varchar(320) not null,
    "DisplayName" varchar(200) not null,
    "Phone" varchar(20) null,
    "PasswordHash" varchar(500) not null,
    "Status" varchar(32) not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "CK_Users_Status" check ("Status" in ('Active', 'Disabled'))
);

create unique index if not exists "UX_Users_Tenant_Email"
    on identity."Users" ("TenantId", "Email");

create unique index if not exists "UX_Users_Tenant_Phone"
    on identity."Users" ("TenantId", "Phone")
    where "Phone" is not null;

create table if not exists identity."UserRoles" (
    "UserId" uuid not null references identity."Users" ("Id"),
    "Role" varchar(32) not null,
    primary key ("UserId", "Role"),
    constraint "CK_UserRoles_Role" check ("Role" in ('Admin', 'Specialist'))
);

create table if not exists whatsapp."OutboxMessages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Type" varchar(200) not null,
    "Payload" text not null,
    "OccurredAt" timestamptz not null,
    "ProcessedAt" timestamptz null,
    "Attempts" integer not null default 0,
    "Error" varchar(2000) null
);

create index if not exists "IX_whatsapp_Outbox_Pending"
    on whatsapp."OutboxMessages" ("OccurredAt")
    where "ProcessedAt" is null;

create table if not exists whatsapp."WebhookReceipts" (
    "Id" uuid primary key,
    "Provider" varchar(64) not null,
    "ProviderEventId" varchar(128) not null,
    "ReceivedAt" timestamptz not null,
    constraint "UX_whatsapp_WebhookReceipts" unique ("Provider", "ProviderEventId")
);

create table if not exists whatsapp."ChannelAccounts" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "PhoneNumberId" varchar(32) not null,
    "WabaId" varchar(32) not null,
    "DisplayPhone" varchar(20) not null,
    "TokenSecretReference" varchar(200) not null,
    "AppSecretReference" varchar(200) not null,
    "IsActive" boolean not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1
);

create unique index if not exists "UX_ChannelAccounts_Active"
    on whatsapp."ChannelAccounts" ("TenantId")
    where "IsActive";

create table if not exists whatsapp."MessageTemplates" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Name" varchar(200) not null,
    "Language" varchar(16) not null,
    "Status" varchar(32) not null,
    "Body" text not null,
    "SyncedAt" timestamptz not null,
    constraint "UX_MessageTemplates" unique ("TenantId", "Name", "Language"),
    constraint "CK_MessageTemplates_Status" check ("Status" in ('Approved', 'Pending', 'Rejected'))
);

create table if not exists whatsapp."Conversations" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Phone" varchar(20) not null,
    "ContactDisplayName" varchar(200) null,
    "Status" varchar(32) not null,
    "ExternalThreadId" varchar(128) null,
    "CtwaClid" varchar(256) null,
    "Fbclid" varchar(256) null,
    "SourceCampaign" varchar(200) null,
    "SourceAd" varchar(200) null,
    "StartedAt" timestamptz not null,
    "LastInboundAt" timestamptz null,
    "ContactStartedAt" timestamptz null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "CK_Conversations_Status" check ("Status" in ('Open', 'Closed'))
);

create unique index if not exists "UX_Conversations_OpenPhone"
    on whatsapp."Conversations" ("TenantId", "Phone")
    where "Status" = 'Open';

create index if not exists "IX_Conversations_ExternalThread"
    on whatsapp."Conversations" ("TenantId", "ExternalThreadId");

create table if not exists whatsapp."Messages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "ConversationId" uuid not null references whatsapp."Conversations" ("Id"),
    "Direction" varchar(16) not null,
    "Body" text null,
    "TemplateKey" varchar(200) null,
    "ExternalMessageId" varchar(128) null,
    "RequestedBy" varchar(32) null,
    "OccurredAt" timestamptz not null,
    constraint "CK_Messages_Direction" check ("Direction" in ('Inbound', 'Outbound'))
);

create unique index if not exists "UX_Messages_External"
    on whatsapp."Messages" ("TenantId", "ExternalMessageId")
    where "ExternalMessageId" is not null;

create index if not exists "IX_Messages_Conversation"
    on whatsapp."Messages" ("ConversationId", "OccurredAt");

create table if not exists qualification."OutboxMessages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Type" varchar(200) not null,
    "Payload" text not null,
    "OccurredAt" timestamptz not null,
    "ProcessedAt" timestamptz null,
    "Attempts" integer not null default 0,
    "Error" varchar(2000) null
);

create index if not exists "IX_qualification_Outbox_Pending"
    on qualification."OutboxMessages" ("OccurredAt")
    where "ProcessedAt" is null;

create table if not exists qualification."InboxMessages" (
    "Id" uuid primary key,
    "EventId" uuid not null,
    "Type" varchar(200) not null,
    "ProcessedAt" timestamptz not null,
    constraint "UX_qualification_Inbox" unique ("EventId", "Type")
);

create table if not exists qualification."CriteriaSets" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Name" varchar(200) not null,
    "Version" integer not null,
    "IsActive" boolean not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1
);

create unique index if not exists "UX_CriteriaSets_Active"
    on qualification."CriteriaSets" ("TenantId")
    where "IsActive";

create table if not exists qualification."Steps" (
    "Id" uuid primary key,
    "CriteriaSetId" uuid not null references qualification."CriteriaSets" ("Id"),
    "SortOrder" integer not null,
    "Kind" varchar(32) not null,
    "Prompt" varchar(1000) not null,
    "ConfigJson" text not null,
    constraint "UX_Steps_Order" unique ("CriteriaSetId", "SortOrder"),
    constraint "CK_Steps_Kind" check ("Kind" in ('Question', 'Capture', 'Decision'))
);

create table if not exists qualification."CaptureFields" (
    "Id" uuid primary key,
    "CriteriaSetId" uuid not null references qualification."CriteriaSets" ("Id"),
    "FieldKey" varchar(64) not null,
    "Label" varchar(200) not null,
    "SemanticRole" varchar(32) not null,
    "Required" boolean not null,
    "SortOrder" integer not null,
    constraint "UX_CaptureFields_Key" unique ("CriteriaSetId", "FieldKey"),
    constraint "CK_CaptureFields_Role" check ("SemanticRole" in ('Name', 'Document', 'None'))
);

create unique index if not exists "UX_CaptureFields_SemanticRole"
    on qualification."CaptureFields" ("CriteriaSetId", "SemanticRole")
    where "SemanticRole" in ('Name', 'Document');

create table if not exists qualification."Leads" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "ConversationId" uuid not null,
    "CriteriaSetId" uuid not null references qualification."CriteriaSets" ("Id"),
    "CurrentStepId" uuid null references qualification."Steps" ("Id"),
    "Status" varchar(32) not null,
    "DisplayName" varchar(200) null,
    "DocumentNumber" varchar(64) null,
    "DisqualifyReason" varchar(500) null,
    "QualifiedAt" timestamptz null,
    "DisqualifiedAt" timestamptz null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "UX_Leads_Conversation" unique ("TenantId", "ConversationId"),
    constraint "CK_Leads_Status" check ("Status" in ('InTriage', 'AwaitingReply', 'Qualified', 'Disqualified'))
);

create index if not exists "IX_Leads_Status"
    on qualification."Leads" ("TenantId", "Status", "CreatedAt");

create table if not exists qualification."LeadAnswers" (
    "Id" uuid primary key,
    "LeadId" uuid not null references qualification."Leads" ("Id"),
    "StepId" uuid not null references qualification."Steps" ("Id"),
    "RawValue" text not null,
    "AnsweredAt" timestamptz not null,
    constraint "UX_LeadAnswers" unique ("LeadId", "StepId")
);

create table if not exists qualification."LeadFieldValues" (
    "LeadId" uuid not null references qualification."Leads" ("Id"),
    "FieldKey" varchar(64) not null,
    "Value" varchar(500) not null,
    primary key ("LeadId", "FieldKey")
);

create table if not exists followup."OutboxMessages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Type" varchar(200) not null,
    "Payload" text not null,
    "OccurredAt" timestamptz not null,
    "ProcessedAt" timestamptz null,
    "Attempts" integer not null default 0,
    "Error" varchar(2000) null
);

create index if not exists "IX_followup_Outbox_Pending"
    on followup."OutboxMessages" ("OccurredAt")
    where "ProcessedAt" is null;

create table if not exists followup."InboxMessages" (
    "Id" uuid primary key,
    "EventId" uuid not null,
    "Type" varchar(200) not null,
    "ProcessedAt" timestamptz not null,
    constraint "UX_followup_Inbox" unique ("EventId", "Type")
);

create table if not exists followup."Sequences" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Name" varchar(200) not null,
    "IsActive" boolean not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1
);

create unique index if not exists "UX_Sequences_Active"
    on followup."Sequences" ("TenantId")
    where "IsActive";

create table if not exists followup."SequenceSteps" (
    "Id" uuid primary key,
    "SequenceId" uuid not null references followup."Sequences" ("Id"),
    "SortOrder" integer not null,
    "DelayMinutes" integer not null,
    "Body" text null,
    "TemplateName" varchar(200) null,
    constraint "UX_SequenceSteps_Order" unique ("SequenceId", "SortOrder")
);

create table if not exists followup."Cases" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "LeadId" uuid not null,
    "ConversationId" uuid not null,
    "SequenceId" uuid not null references followup."Sequences" ("Id"),
    "Status" varchar(32) not null,
    "NextStepOrder" integer not null,
    "NextRunAt" timestamptz null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "CK_Cases_Status" check ("Status" in ('Armed', 'Cancelled', 'Exhausted'))
);

create unique index if not exists "UX_Cases_Armed"
    on followup."Cases" ("TenantId", "LeadId")
    where "Status" = 'Armed';

create index if not exists "IX_Cases_NextRun"
    on followup."Cases" ("NextRunAt")
    where "Status" = 'Armed';

create table if not exists followup."Attempts" (
    "Id" uuid primary key,
    "CaseId" uuid not null references followup."Cases" ("Id"),
    "SortOrder" integer not null,
    "Body" text null,
    "TemplateName" varchar(200) null,
    "ScheduledAt" timestamptz not null,
    "SentAt" timestamptz null,
    "Status" varchar(32) not null,
    constraint "UX_Attempts_Order" unique ("CaseId", "SortOrder"),
    constraint "CK_Attempts_Status" check ("Status" in ('Pending', 'Sent', 'Skipped'))
);

create table if not exists attendance."OutboxMessages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Type" varchar(200) not null,
    "Payload" text not null,
    "OccurredAt" timestamptz not null,
    "ProcessedAt" timestamptz null,
    "Attempts" integer not null default 0,
    "Error" varchar(2000) null
);

create index if not exists "IX_attendance_Outbox_Pending"
    on attendance."OutboxMessages" ("OccurredAt")
    where "ProcessedAt" is null;

create table if not exists attendance."InboxMessages" (
    "Id" uuid primary key,
    "EventId" uuid not null,
    "Type" varchar(200) not null,
    "ProcessedAt" timestamptz not null,
    constraint "UX_attendance_Inbox" unique ("EventId", "Type")
);

create table if not exists attendance."WorkItems" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "LeadId" uuid not null,
    "ConversationId" uuid not null,
    "Status" varchar(32) not null,
    "SpecialistId" uuid null,
    "AssignedAt" timestamptz null,
    "ClosedAt" timestamptz null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "UX_WorkItems_Lead" unique ("TenantId", "LeadId"),
    constraint "CK_WorkItems_Status" check ("Status" in ('Queued', 'Assigned', 'Closed'))
);

create index if not exists "IX_WorkItems_Status"
    on attendance."WorkItems" ("TenantId", "Status", "CreatedAt");

create table if not exists attendance."Notes" (
    "Id" uuid primary key,
    "WorkItemId" uuid not null references attendance."WorkItems" ("Id"),
    "AuthorId" uuid not null,
    "Body" text not null,
    "CreatedAt" timestamptz not null
);

create index if not exists "IX_Notes_WorkItem"
    on attendance."Notes" ("WorkItemId", "CreatedAt");

create table if not exists attendance."Acceptances" (
    "Id" uuid primary key,
    "WorkItemId" uuid not null references attendance."WorkItems" ("Id"),
    "SpecialistId" uuid not null,
    "Label" varchar(200) not null,
    "Amount" numeric(19, 4) not null,
    "Currency" char(3) not null,
    "AcceptedAt" timestamptz not null,
    "ChargeId" uuid null,
    "PaymentStatus" varchar(32) not null,
    constraint "CK_Acceptances_Payment" check ("PaymentStatus" in ('Pending', 'Paid', 'Failed'))
);

create index if not exists "IX_Acceptances_Charge"
    on attendance."Acceptances" ("ChargeId");

create table if not exists payments."OutboxMessages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Type" varchar(200) not null,
    "Payload" text not null,
    "OccurredAt" timestamptz not null,
    "ProcessedAt" timestamptz null,
    "Attempts" integer not null default 0,
    "Error" varchar(2000) null
);

create index if not exists "IX_payments_Outbox_Pending"
    on payments."OutboxMessages" ("OccurredAt")
    where "ProcessedAt" is null;

create table if not exists payments."WebhookReceipts" (
    "Id" uuid primary key,
    "Provider" varchar(64) not null,
    "ProviderEventId" varchar(128) not null,
    "ReceivedAt" timestamptz not null,
    constraint "UX_payments_WebhookReceipts" unique ("Provider", "ProviderEventId")
);

create table if not exists payments."GatewayAccounts" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Provider" varchar(64) not null,
    "SecretReference" varchar(200) not null,
    "IsActive" boolean not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1
);

create unique index if not exists "UX_GatewayAccounts_Active"
    on payments."GatewayAccounts" ("TenantId")
    where "IsActive";

create table if not exists payments."Charges" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "LeadId" uuid not null,
    "Label" varchar(200) not null,
    "Amount" numeric(19, 4) not null,
    "Currency" char(3) not null,
    "Status" varchar(32) not null,
    "Provider" varchar(64) not null,
    "ExternalChargeId" varchar(128) null,
    "CheckoutReference" varchar(500) null,
    "CreatedAt" timestamptz not null,
    "PaidAt" timestamptz null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "CK_Charges_Status" check ("Status" in ('Pending', 'Paid', 'Failed', 'Expired'))
);

create unique index if not exists "UX_Charges_External"
    on payments."Charges" ("Provider", "ExternalChargeId")
    where "ExternalChargeId" is not null;

create index if not exists "IX_Charges_Lead"
    on payments."Charges" ("TenantId", "LeadId");

create index if not exists "IX_Charges_Status"
    on payments."Charges" ("TenantId", "Status");

create table if not exists marketing."OutboxMessages" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "Type" varchar(200) not null,
    "Payload" text not null,
    "OccurredAt" timestamptz not null,
    "ProcessedAt" timestamptz null,
    "Attempts" integer not null default 0,
    "Error" varchar(2000) null
);

create index if not exists "IX_marketing_Outbox_Pending"
    on marketing."OutboxMessages" ("OccurredAt")
    where "ProcessedAt" is null;

create table if not exists marketing."InboxMessages" (
    "Id" uuid primary key,
    "EventId" uuid not null,
    "Type" varchar(200) not null,
    "ProcessedAt" timestamptz not null,
    constraint "UX_marketing_Inbox" unique ("EventId", "Type")
);

create table if not exists marketing."Connections" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "DatasetId" varchar(64) not null,
    "WabaId" varchar(32) not null,
    "TokenSecretReference" varchar(200) not null,
    "ContactEventName" varchar(64) null default 'LeadSubmitted',
    "QualifiedEventName" varchar(64) not null default 'QualifiedLead',
    "PurchaseEventName" varchar(64) not null default 'Purchase',
    "PurchaseChargeLabel" varchar(200) null,
    "IsActive" boolean not null,
    "CreatedAt" timestamptz not null,
    "UpdatedAt" timestamptz not null,
    "RowVersion" bigint not null default 1,
    constraint "UX_Connections_Tenant" unique ("TenantId")
);

create table if not exists marketing."Attributions" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "ConversationId" uuid not null,
    "LeadId" uuid null,
    "CtwaClid" varchar(512) not null,
    "SourceAd" varchar(200) null,
    "CreatedAt" timestamptz not null,
    constraint "UX_Attributions_Conversation" unique ("TenantId", "ConversationId")
);

create table if not exists marketing."Deliveries" (
    "Id" uuid primary key,
    "TenantId" uuid not null,
    "BusinessEvent" varchar(32) not null,
    "SourceId" uuid not null,
    "MetaEventName" varchar(64) not null,
    "Status" varchar(32) not null,
    "SkipReason" varchar(200) null,
    "ResponseCode" integer null,
    "AttemptCount" integer not null default 0,
    "CreatedAt" timestamptz not null,
    "SentAt" timestamptz null,
    constraint "UX_Deliveries_Source" unique ("TenantId", "BusinessEvent", "SourceId"),
    constraint "CK_Deliveries_Status" check ("Status" in ('Pending', 'Sent', 'Skipped', 'Failed'))
);
