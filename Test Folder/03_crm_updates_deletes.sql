/*
Step 2B: Update/Delete (and controlled upsert) scenarios for CRM schema tests
- Includes CTE updates, MERGE, delete with joins, OUTPUT and transaction handling.
*/

USE CrmTestDb;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

BEGIN TRY
    /*
    Scenario 1: Recalculate lead score based on multiple business signals.
    */
    WITH LeadSignals AS
    (
        SELECT
            l.LeadId,
            NewScore =
                (CASE WHEN l.StatusCode = N'Qualified' THEN 35 ELSE 0 END)
                + (CASE WHEN l.EstimatedDealValue >= 50000 THEN 25 WHEN l.EstimatedDealValue >= 10000 THEN 15 ELSE 5 END)
                + (CASE WHEN l.SourceCode IN (N'Referral', N'Partner') THEN 20 ELSE 8 END)
                + (CASE WHEN l.Email IS NOT NULL THEN 5 ELSE 0 END)
                + (CASE WHEN l.Phone IS NOT NULL THEN 5 ELSE 0 END)
        FROM sales.Lead l
    )
    UPDATE l
    SET
        l.Score = CASE WHEN s.NewScore > 100 THEN 100 ELSE s.NewScore END,
        l.QualifiedAt = CASE WHEN l.StatusCode = N'Qualified' AND l.QualifiedAt IS NULL THEN SYSUTCDATETIME() ELSE l.QualifiedAt END
    OUTPUT
        INSERTED.LeadId,
        DELETED.Score AS OldScore,
        INSERTED.Score AS NewScore
    FROM sales.Lead l
    INNER JOIN LeadSignals s ON s.LeadId = l.LeadId;

    /*
    Scenario 2: Sync quote line prices from product master for draft quotes only.
    */
    UPDATE ql
    SET ql.UnitPrice = p.UnitPrice
    OUTPUT
        INSERTED.QuoteLineId,
        DELETED.UnitPrice AS OldUnitPrice,
        INSERTED.UnitPrice AS NewUnitPrice
    FROM sales.QuoteLine ql
    INNER JOIN sales.Quote q ON q.QuoteId = ql.QuoteId
    INNER JOIN sales.Product p ON p.ProductId = ql.ProductId
    WHERE q.QuoteStatus = N'Draft'
      AND ql.UnitPrice <> p.UnitPrice;

    /*
    Scenario 3: MERGE example for campaign ownership standardization by channel.
    */
    DECLARE @CampaignOwner TABLE
    (
        CampaignCode NVARCHAR(40) NOT NULL PRIMARY KEY,
        NewOwnerUserId INT NOT NULL
    );

    -- Fill this table variable with mappings before execution in real tests.
    -- Example (optional):
    -- INSERT INTO @CampaignOwner (CampaignCode, NewOwnerUserId)
    -- VALUES (N'SUMMER-2027', 12), (N'EVENT-DACH-01', 8);

    MERGE marketing.Campaign AS target
    USING @CampaignOwner AS source
        ON target.CampaignCode = source.CampaignCode
    WHEN MATCHED AND target.OwnerUserId <> source.NewOwnerUserId THEN
        UPDATE SET target.OwnerUserId = source.NewOwnerUserId
    OUTPUT
        $action AS MergeAction,
        INSERTED.CampaignId,
        INSERTED.CampaignCode,
        INSERTED.OwnerUserId;

    /*
    Scenario 4: Delete orphan activities older than X months (no account/contact/opportunity/ticket link).
    */
    DECLARE @MonthsBack INT = 18;

    DELETE act
    OUTPUT
        DELETED.ActivityId,
        DELETED.ActivityType,
        DELETED.Subject,
        DELETED.CreatedAt
    FROM crm.Activity act
    WHERE act.AccountId IS NULL
      AND act.ContactId IS NULL
      AND act.OpportunityId IS NULL
      AND act.TicketId IS NULL
      AND act.CreatedAt < DATEADD(MONTH, -@MonthsBack, SYSUTCDATETIME());

    /*
    Scenario 5: Close stale tickets in bulk with joined filtering.
    */
    ;WITH StaleTickets AS
    (
        SELECT
            t.TicketId,
            t.StatusCode,
            t.OpenedAt,
            LastActivityAt = MAX(act.CreatedAt)
        FROM support.Ticket t
        LEFT JOIN crm.Activity act ON act.TicketId = t.TicketId
        INNER JOIN crm.Account a ON a.AccountId = t.AccountId
        WHERE t.StatusCode IN (N'Open', N'InProgress', N'Pending')
          AND a.StatusCode = N'Active'
        GROUP BY t.TicketId, t.StatusCode, t.OpenedAt
        HAVING MAX(COALESCE(act.CreatedAt, t.OpenedAt)) < DATEADD(DAY, -90, SYSUTCDATETIME())
    )
    UPDATE t
    SET
        t.StatusCode = N'Closed',
        t.ClosedAt = COALESCE(t.ClosedAt, SYSUTCDATETIME())
    OUTPUT
        INSERTED.TicketId,
        DELETED.StatusCode AS OldStatus,
        INSERTED.StatusCode AS NewStatus,
        INSERTED.ClosedAt
    FROM support.Ticket t
    INNER JOIN StaleTickets st ON st.TicketId = t.TicketId;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
