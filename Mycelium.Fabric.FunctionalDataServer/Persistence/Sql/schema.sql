-- ------------------------------------------------------------------------------------------------
-- <copyright file="schema.sql" company="Starion Group S.A.">
--
--   Copyright 2026 Starion Group S.A.
--   SPDX-License-Identifier: Apache-2.0
--
-- </copyright>
-- ------------------------------------------------------------------------------------------------
-- THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!
-- Fabric #33: initial PostgreSQL 18 schema; the consumer owns the transaction.
-- Mycelium.Model.Fabric 0.3.0
-- Mycelium.Model.CommonPrimitives 0.4.0

CREATE SCHEMA IF NOT EXISTS "Fabric";

CREATE TABLE "Fabric"."AuditableThing" (
    "id" uuid NOT NULL,
    "createdBy" uuid NOT NULL,
    "updatedBy" uuid NOT NULL,
    CONSTRAINT "pk_AuditableThing_id_72a95e53fd209adf" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."BranchProtectionRule" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_BranchProtectionRule_id_d67bbafec6116a4d" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."BranchProtectionRule_defaultReviewers__User" (
    "sourceBranchProtectionRule" uuid NOT NULL,
    "targetUser" uuid NOT NULL,
    CONSTRAINT "pk_BranchProtectionRule_defaultReviewers__User_6858b7d342fcbed9" PRIMARY KEY ("sourceBranchProtectionRule", "targetUser")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Comment" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_Comment_id_983b648ee1e42290" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Comment_replies__Reply" (
    "sourceComment" uuid NOT NULL,
    "targetReply" uuid NOT NULL,
    CONSTRAINT "pk_Comment_replies__Reply_source_target_d8e9f108e1d307f6" PRIMARY KEY ("sourceComment", "targetReply")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."FunctionalProject" (
    "id" uuid NOT NULL,
    "belongsTo" uuid NOT NULL,
    "policy" uuid NOT NULL,
    CONSTRAINT "pk_FunctionalProject_id_8091cd99121dc778" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."FunctionalProject_branchRules__BranchProtectionRule" (
    "sourceFunctionalProject" uuid NOT NULL,
    "targetBranchProtectionRule" uuid NOT NULL,
    CONSTRAINT "pk_FunctionalProject_branchRules__BranchProtec_120f3ff7eb786a19" PRIMARY KEY ("sourceFunctionalProject", "targetBranchProtectionRule")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."FunctionalProject_defines__Ownership" (
    "sourceFunctionalProject" uuid NOT NULL,
    "targetOwnership" uuid NOT NULL,
    CONSTRAINT "pk_FunctionalProject_defines__Ownership_source_50db1001d7e8fd43" PRIMARY KEY ("sourceFunctionalProject", "targetOwnership")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."FunctionalProject_reviews__Review" (
    "sourceFunctionalProject" uuid NOT NULL,
    "targetReview" uuid NOT NULL,
    CONSTRAINT "pk_FunctionalProject_reviews__Review_source_ta_b8bb25f7f1492c0c" PRIMARY KEY ("sourceFunctionalProject", "targetReview")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."FunctionalProjectPolicy" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_FunctionalProjectPolicy_id_290f0091afc559b6" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Message" (
    "id" uuid NOT NULL,
    "quotes" uuid,
    CONSTRAINT "pk_Message_id_85b431e2aab021fc" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Organization" (
    "id" uuid NOT NULL,
    "policy" uuid NOT NULL,
    CONSTRAINT "pk_Organization_id_61322b2aa1e23b7c" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."OrganizationMember" (
    "id" uuid NOT NULL,
    "organization" uuid NOT NULL,
    "user" uuid NOT NULL,
    CONSTRAINT "pk_OrganizationMember_id_4c61768751cae2d4" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."OrganizationPolicy" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_OrganizationPolicy_id_049e5722ef5457fe" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Ownership" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_Ownership_id_626caa43d71d9778" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."ProjectMember" (
    "id" uuid NOT NULL,
    "activeOwnership" uuid,
    "isPartOf" uuid NOT NULL,
    "user" uuid NOT NULL,
    CONSTRAINT "pk_ProjectMember_id_0e42d580abc52fbf" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."ProjectMember_owns__Ownership" (
    "sourceProjectMember" uuid NOT NULL,
    "targetOwnership" uuid NOT NULL,
    CONSTRAINT "pk_ProjectMember_owns__Ownership_source_target_d88c3dc8cbed57a5" PRIMARY KEY ("sourceProjectMember", "targetOwnership")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Reply" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_Reply_id_35bb1ec58f8714f2" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Review" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_Review_id_422edf4c4d2965c4" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Review_comments__Comment" (
    "sourceReview" uuid NOT NULL,
    "targetComment" uuid NOT NULL,
    CONSTRAINT "pk_Review_comments__Comment_source_target_13caf0d89676b0f0" PRIMARY KEY ("sourceReview", "targetComment")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Review_reviewers__User" (
    "sourceReview" uuid NOT NULL,
    "targetUser" uuid NOT NULL,
    CONSTRAINT "pk_Review_reviewers__User_source_target_705476f6ac9cce63" PRIMARY KEY ("sourceReview", "targetUser")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."Thing" (
    "id" uuid NOT NULL,
    "classKind" text,
    "data" jsonb,
    CONSTRAINT "pk_Thing_id_784c32a358b305ca" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

CREATE TABLE "Fabric"."User" (
    "id" uuid NOT NULL,
    CONSTRAINT "pk_User_id_26ae2f601080c9c3" PRIMARY KEY ("id")
)
WITH (
    autovacuum_vacuum_scale_factor = 0.0,
    autovacuum_vacuum_threshold = 2500,
    autovacuum_analyze_scale_factor = 0.0,
    autovacuum_analyze_threshold = 2500
);

ALTER TABLE "Fabric"."AuditableThing"
    ADD CONSTRAINT "fk_AuditableThing_id_Thing_d1925265588d54c7" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."AuditableThing"
    ADD CONSTRAINT "fk_AuditableThing_createdBy_User_2a41fbeb37adc6d9" FOREIGN KEY ("createdBy") REFERENCES "Fabric"."User" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_AuditableThing_createdBy_914bfc2010211775" ON "Fabric"."AuditableThing" ("createdBy");

ALTER TABLE "Fabric"."AuditableThing"
    ADD CONSTRAINT "fk_AuditableThing_updatedBy_User_010cb77982b99b4b" FOREIGN KEY ("updatedBy") REFERENCES "Fabric"."User" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_AuditableThing_updatedBy_89e97663de45ef42" ON "Fabric"."AuditableThing" ("updatedBy");

ALTER TABLE "Fabric"."BranchProtectionRule"
    ADD CONSTRAINT "fk_BranchProtectionRule_id_Thing_a1b03acf0008e204" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."BranchProtectionRule_defaultReviewers__User"
    ADD CONSTRAINT "fk_BranchProtectionRule_defaultReviewers__User_4e2e522564423b43" FOREIGN KEY ("sourceBranchProtectionRule") REFERENCES "Fabric"."BranchProtectionRule" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."BranchProtectionRule_defaultReviewers__User"
    ADD CONSTRAINT "fk_BranchProtectionRule_defaultReviewers__User_9cfe05764a457aaa" FOREIGN KEY ("targetUser") REFERENCES "Fabric"."User" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_BranchProtectionRule_defaultReviewers__User_e1175db9b738ea6b" ON "Fabric"."BranchProtectionRule_defaultReviewers__User" ("targetUser");

ALTER TABLE "Fabric"."Comment"
    ADD CONSTRAINT "fk_Comment_id_Thing_5f92289c45416a87" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Comment_replies__Reply"
    ADD CONSTRAINT "fk_Comment_replies__Reply_source_Comment_ab0e614f95316ad6" FOREIGN KEY ("sourceComment") REFERENCES "Fabric"."Comment" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Comment_replies__Reply"
    ADD CONSTRAINT "fk_Comment_replies__Reply_target_Reply_ae79a693680a89b1" FOREIGN KEY ("targetReply") REFERENCES "Fabric"."Reply" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_Comment_replies__Reply_target_Reply_efc9e3600352ed42" ON "Fabric"."Comment_replies__Reply" ("targetReply");

ALTER TABLE "Fabric"."FunctionalProject"
    ADD CONSTRAINT "fk_FunctionalProject_id_Thing_c13540c58a34737d" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."FunctionalProject"
    ADD CONSTRAINT "fk_FunctionalProject_belongsTo_Organization_cd618b13446bee23" FOREIGN KEY ("belongsTo") REFERENCES "Fabric"."Organization" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_FunctionalProject_belongsTo_a63ca55fd274e433" ON "Fabric"."FunctionalProject" ("belongsTo");

ALTER TABLE "Fabric"."FunctionalProject_branchRules__BranchProtectionRule"
    ADD CONSTRAINT "fk_FunctionalProject_branchRules__BranchProtec_a9cde78c1ef1d8d6" FOREIGN KEY ("sourceFunctionalProject") REFERENCES "Fabric"."FunctionalProject" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."FunctionalProject_branchRules__BranchProtectionRule"
    ADD CONSTRAINT "fk_FunctionalProject_branchRules__BranchProtec_70455e4b2c333196" FOREIGN KEY ("targetBranchProtectionRule") REFERENCES "Fabric"."BranchProtectionRule" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_FunctionalProject_branchRules__BranchProtec_ebecb5845568094f" ON "Fabric"."FunctionalProject_branchRules__BranchProtectionRule" ("targetBranchProtectionRule");

ALTER TABLE "Fabric"."FunctionalProject_defines__Ownership"
    ADD CONSTRAINT "fk_FunctionalProject_defines__Ownership_source_9b20d43747d1f672" FOREIGN KEY ("sourceFunctionalProject") REFERENCES "Fabric"."FunctionalProject" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."FunctionalProject_defines__Ownership"
    ADD CONSTRAINT "fk_FunctionalProject_defines__Ownership_target_eae9f5e9a36e5585" FOREIGN KEY ("targetOwnership") REFERENCES "Fabric"."Ownership" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_FunctionalProject_defines__Ownership_target_d61d2a466953d31e" ON "Fabric"."FunctionalProject_defines__Ownership" ("targetOwnership");

ALTER TABLE "Fabric"."FunctionalProject"
    ADD CONSTRAINT "fk_FunctionalProject_policy_FunctionalProjectP_feeee30c211ab0e5" FOREIGN KEY ("policy") REFERENCES "Fabric"."FunctionalProjectPolicy" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_FunctionalProject_policy_e484dbbd9bd93703" ON "Fabric"."FunctionalProject" ("policy");

ALTER TABLE "Fabric"."FunctionalProject_reviews__Review"
    ADD CONSTRAINT "fk_FunctionalProject_reviews__Review_source_Fu_55cd2de8139c2867" FOREIGN KEY ("sourceFunctionalProject") REFERENCES "Fabric"."FunctionalProject" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."FunctionalProject_reviews__Review"
    ADD CONSTRAINT "fk_FunctionalProject_reviews__Review_target_Re_2ca9d927e6ee7953" FOREIGN KEY ("targetReview") REFERENCES "Fabric"."Review" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_FunctionalProject_reviews__Review_target_Re_dde4b1f23adcbc5c" ON "Fabric"."FunctionalProject_reviews__Review" ("targetReview");

ALTER TABLE "Fabric"."FunctionalProjectPolicy"
    ADD CONSTRAINT "fk_FunctionalProjectPolicy_id_Thing_0360678339ed60e5" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Message"
    ADD CONSTRAINT "fk_Message_id_Thing_ffa37cfdf681b804" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Message"
    ADD CONSTRAINT "fk_Message_quotes_Message_beb2e7251640849c" FOREIGN KEY ("quotes") REFERENCES "Fabric"."Message" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_Message_quotes_ed74432451f40c2e" ON "Fabric"."Message" ("quotes");

ALTER TABLE "Fabric"."Organization"
    ADD CONSTRAINT "fk_Organization_id_Thing_609760c56d991d0a" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Organization"
    ADD CONSTRAINT "fk_Organization_policy_OrganizationPolicy_92cbd90c5a97ae73" FOREIGN KEY ("policy") REFERENCES "Fabric"."OrganizationPolicy" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_Organization_policy_d35c848fbb376a84" ON "Fabric"."Organization" ("policy");

ALTER TABLE "Fabric"."OrganizationMember"
    ADD CONSTRAINT "fk_OrganizationMember_id_Thing_523e6b264f65ecdf" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."OrganizationMember"
    ADD CONSTRAINT "fk_OrganizationMember_organization_Organizatio_379ee846a5248b70" FOREIGN KEY ("organization") REFERENCES "Fabric"."Organization" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_OrganizationMember_organization_46eacfdcc9cb5f43" ON "Fabric"."OrganizationMember" ("organization");

ALTER TABLE "Fabric"."OrganizationMember"
    ADD CONSTRAINT "fk_OrganizationMember_user_User_a6918a2b15674837" FOREIGN KEY ("user") REFERENCES "Fabric"."User" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_OrganizationMember_user_b7980814bc477109" ON "Fabric"."OrganizationMember" ("user");

ALTER TABLE "Fabric"."OrganizationPolicy"
    ADD CONSTRAINT "fk_OrganizationPolicy_id_Thing_749a1cf0facd00e9" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Ownership"
    ADD CONSTRAINT "fk_Ownership_id_Thing_79c98750873d3514" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."ProjectMember"
    ADD CONSTRAINT "fk_ProjectMember_id_Thing_c2cb3bed1050af42" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."ProjectMember"
    ADD CONSTRAINT "fk_ProjectMember_activeOwnership_Ownership_cd753f96ad8f0035" FOREIGN KEY ("activeOwnership") REFERENCES "Fabric"."Ownership" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_ProjectMember_activeOwnership_18f949df6dafe913" ON "Fabric"."ProjectMember" ("activeOwnership");

ALTER TABLE "Fabric"."ProjectMember"
    ADD CONSTRAINT "fk_ProjectMember_isPartOf_FunctionalProject_a95598d6adb06456" FOREIGN KEY ("isPartOf") REFERENCES "Fabric"."FunctionalProject" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_ProjectMember_isPartOf_0d5a03b0171d1585" ON "Fabric"."ProjectMember" ("isPartOf");

ALTER TABLE "Fabric"."ProjectMember_owns__Ownership"
    ADD CONSTRAINT "fk_ProjectMember_owns__Ownership_source_Projec_c7c79df389452ca1" FOREIGN KEY ("sourceProjectMember") REFERENCES "Fabric"."ProjectMember" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."ProjectMember_owns__Ownership"
    ADD CONSTRAINT "fk_ProjectMember_owns__Ownership_target_Owners_2ee9c00e908e29b2" FOREIGN KEY ("targetOwnership") REFERENCES "Fabric"."Ownership" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_ProjectMember_owns__Ownership_target_Owners_1d0aa67bbee0aff8" ON "Fabric"."ProjectMember_owns__Ownership" ("targetOwnership");

ALTER TABLE "Fabric"."ProjectMember"
    ADD CONSTRAINT "fk_ProjectMember_user_User_16ad0ada343ba0d7" FOREIGN KEY ("user") REFERENCES "Fabric"."User" ("id")
    ON DELETE NO ACTION DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_ProjectMember_user_98fd8bad23b7d176" ON "Fabric"."ProjectMember" ("user");

ALTER TABLE "Fabric"."Reply"
    ADD CONSTRAINT "fk_Reply_id_Thing_f705ecb6cb4fcf0f" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Review"
    ADD CONSTRAINT "fk_Review_id_Thing_7ce8b14dba599ed9" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Review_comments__Comment"
    ADD CONSTRAINT "fk_Review_comments__Comment_source_Review_60652a66352602c6" FOREIGN KEY ("sourceReview") REFERENCES "Fabric"."Review" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Review_comments__Comment"
    ADD CONSTRAINT "fk_Review_comments__Comment_target_Comment_01616243a1b625d0" FOREIGN KEY ("targetComment") REFERENCES "Fabric"."Comment" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_Review_comments__Comment_target_Comment_a1b7eb710f19d005" ON "Fabric"."Review_comments__Comment" ("targetComment");

ALTER TABLE "Fabric"."Review_reviewers__User"
    ADD CONSTRAINT "fk_Review_reviewers__User_source_Review_bd7c34746f88db02" FOREIGN KEY ("sourceReview") REFERENCES "Fabric"."Review" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "Fabric"."Review_reviewers__User"
    ADD CONSTRAINT "fk_Review_reviewers__User_target_User_4ebc4a117f722f7e" FOREIGN KEY ("targetUser") REFERENCES "Fabric"."User" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE INDEX "ix_Review_reviewers__User_target_User_80ac0b37a8414f67" ON "Fabric"."Review_reviewers__User" ("targetUser");

ALTER TABLE "Fabric"."User"
    ADD CONSTRAINT "fk_User_id_Thing_2e6b3dcc67751abe" FOREIGN KEY ("id") REFERENCES "Fabric"."Thing" ("id")
    ON DELETE CASCADE DEFERRABLE INITIALLY IMMEDIATE;

CREATE FUNCTION "Fabric"."auditablething_delete"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    DELETE FROM "Fabric"."AuditableThing" WHERE "id" = OLD."id";
    RETURN OLD;
END;
$mycelium$;

CREATE FUNCTION "Fabric"."message_delete"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    DELETE FROM "Fabric"."Message" WHERE "id" = OLD."id";
    RETURN OLD;
END;
$mycelium$;

CREATE FUNCTION "Fabric"."thing_delete"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."id";
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."AuditableThing"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."BranchProtectionRule"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."BranchProtectionRule"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."Comment"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_message_delete"
    AFTER DELETE ON "Fabric"."Comment"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."message_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."FunctionalProject"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."FunctionalProject"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."FunctionalProjectPolicy"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."FunctionalProjectPolicy"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."Message"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."Message"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."Organization"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."Organization"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."OrganizationMember"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."OrganizationMember"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."OrganizationPolicy"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."OrganizationPolicy"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."Ownership"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."Ownership"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."ProjectMember"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."ProjectMember"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."Reply"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_message_delete"
    AFTER DELETE ON "Fabric"."Reply"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."message_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."Review"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."Review"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE TRIGGER "trg_thing_delete"
    AFTER DELETE ON "Fabric"."User"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."thing_delete"();

CREATE TRIGGER "trg_auditablething_delete"
    AFTER DELETE ON "Fabric"."User"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."auditablething_delete"();

CREATE FUNCTION "Fabric"."fn_delete_parts_Comment_replies_b52e0cee92e8df6b"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Fabric"."Comment"
        WHERE "id" = OLD."sourceComment"
    ) THEN
        DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."targetReply";
    END IF;
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_Comment_replies_4750de3e1dbe2ce9"
    AFTER DELETE ON "Fabric"."Comment_replies__Reply"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_Comment_replies_b52e0cee92e8df6b"();

CREATE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_branchRules_225915afde7a82b7"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Fabric"."FunctionalProject"
        WHERE "id" = OLD."sourceFunctionalProject"
    ) THEN
        DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."targetBranchProtectionRule";
    END IF;
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_FunctionalProject_branchRules_44c5f4eb95cf3578"
    AFTER DELETE ON "Fabric"."FunctionalProject_branchRules__BranchProtectionRule"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_branchRules_225915afde7a82b7"();

CREATE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_defines_f4190a208f996686"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Fabric"."FunctionalProject"
        WHERE "id" = OLD."sourceFunctionalProject"
    ) THEN
        DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."targetOwnership";
    END IF;
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_FunctionalProject_defines_f91ff0694a6d8ff3"
    AFTER DELETE ON "Fabric"."FunctionalProject_defines__Ownership"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_defines_f4190a208f996686"();

CREATE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_policy_2cfad9821ce30253"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."policy";
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_FunctionalProject_policy_54c1ea6f767e941a"
    AFTER DELETE ON "Fabric"."FunctionalProject"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_policy_2cfad9821ce30253"();

CREATE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_reviews_c212e395eb35492b"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Fabric"."FunctionalProject"
        WHERE "id" = OLD."sourceFunctionalProject"
    ) THEN
        DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."targetReview";
    END IF;
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_FunctionalProject_reviews_3ccdb1ffa5604f18"
    AFTER DELETE ON "Fabric"."FunctionalProject_reviews__Review"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_FunctionalProject_reviews_c212e395eb35492b"();

CREATE FUNCTION "Fabric"."fn_delete_parts_Organization_policy_b8bce4eb34060e4b"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."policy";
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_Organization_policy_c145b330744f41ca"
    AFTER DELETE ON "Fabric"."Organization"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_Organization_policy_b8bce4eb34060e4b"();

CREATE FUNCTION "Fabric"."fn_delete_parts_Review_comments_4d8aa985316b17a0"()
RETURNS trigger
LANGUAGE plpgsql
AS $mycelium$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Fabric"."Review"
        WHERE "id" = OLD."sourceReview"
    ) THEN
        DELETE FROM "Fabric"."Thing" WHERE "id" = OLD."targetComment";
    END IF;
    RETURN OLD;
END;
$mycelium$;

CREATE TRIGGER "trg_delete_parts_Review_comments_e1bcd35ec831d00c"
    AFTER DELETE ON "Fabric"."Review_comments__Comment"
    FOR EACH ROW EXECUTE FUNCTION "Fabric"."fn_delete_parts_Review_comments_4d8aa985316b17a0"();
