/*
Step 2A: Complex SELECT statements for CRM schema test scenarios
- Uses CTEs, recursive CTE, paging, window functions, APPLY and advanced joins.
*/

USE CrmTestDb;
GO

-- Parameters for paging/filter tests
DECLARE @PageNumber INT = 1;
DECLARE @PageSize INT = 50;
DECLARE @OffsetRows INT = (@PageNumber - 1) * @PageSize;
DECLARE @MinExpectedValue DECIMAL(18,2) = 10000;

/*
Query 1: Pipeline overview with 6 joins + window metrics + paging
*/
WITH OpportunityAgg AS
(
    SELECT
        o.OpportunityId,
        o.Name,
        o.StageCode,
        o.EstimatedValue,
        o.ProbabilityPct,
        o.ExpectedCloseDate,
        o.OwnerUserId,
        o.AccountId,
        o.CampaignId,
        TotalLineAmount = SUM(COALESCE(ol.LineAmount, 0.0)),
        ProductCount = COUNT(DISTINCT ol.ProductId)
    FROM sales.Opportunity o
    LEFT JOIN sales.OpportunityLine ol ON ol.OpportunityId = o.OpportunityId
    GROUP BY
        o.OpportunityId,
        o.Name,
        o.StageCode,
        o.EstimatedValue,
        o.ProbabilityPct,
        o.ExpectedCloseDate,
        o.OwnerUserId,
        o.AccountId,
        o.CampaignId
),
Ranked AS
(
    SELECT
        oa.OpportunityId,
        oa.Name,
        oa.StageCode,
        oa.EstimatedValue,
        oa.ProbabilityPct,
        oa.ExpectedCloseDate,
        oa.TotalLineAmount,
        oa.ProductCount,
        ExpectedWeightedValue = oa.EstimatedValue * oa.ProbabilityPct / 100.0,
        a.AccountName,
        a.IndustryCode,
        ownerUser.DisplayName AS OwnerName,
        ownerTeam.TeamName,
        c.CampaignName,
        c.ChannelCode,
        RowNum = ROW_NUMBER() OVER (ORDER BY oa.EstimatedValue DESC, oa.OpportunityId),
        StageTotal = COUNT(*) OVER (PARTITION BY oa.StageCode),
        GlobalRank = DENSE_RANK() OVER (ORDER BY oa.EstimatedValue DESC)
    FROM OpportunityAgg oa
    INNER JOIN crm.Account a ON a.AccountId = oa.AccountId
    INNER JOIN crm.AppUser ownerUser ON ownerUser.UserId = oa.OwnerUserId
    LEFT JOIN crm.Team ownerTeam ON ownerTeam.TeamId = ownerUser.TeamId
    LEFT JOIN marketing.Campaign c ON c.CampaignId = oa.CampaignId
    WHERE oa.EstimatedValue >= @MinExpectedValue
)
SELECT
    OpportunityId,
    Name,
    StageCode,
    EstimatedValue,
    ProbabilityPct,
    ExpectedWeightedValue,
    ExpectedCloseDate,
    TotalLineAmount,
    ProductCount,
    AccountName,
    IndustryCode,
    OwnerName,
    TeamName,
    CampaignName,
    ChannelCode,
    StageTotal,
    GlobalRank
FROM Ranked
ORDER BY RowNum
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;

/*
Query 2: Recursive account hierarchy + account score card
*/
WITH AccountTree AS
(
    SELECT
        a.AccountId,
        a.ParentAccountId,
        a.AccountName,
        LevelNo = 0,
        HierarchyPath = CAST(a.AccountName AS NVARCHAR(2000))
    FROM crm.Account a
    WHERE a.ParentAccountId IS NULL

    UNION ALL

    SELECT
        child.AccountId,
        child.ParentAccountId,
        child.AccountName,
        parent.LevelNo + 1,
        CAST(parent.HierarchyPath + N' > ' + child.AccountName AS NVARCHAR(2000))
    FROM crm.Account child
    INNER JOIN AccountTree parent ON parent.AccountId = child.ParentAccountId
),
AccountScore AS
(
    SELECT
        a.AccountId,
        OpenTicketCount = SUM(CASE WHEN t.StatusCode IN (N'Open', N'InProgress', N'Pending') THEN 1 ELSE 0 END),
        OpenOpportunityCount = SUM(CASE WHEN o.StageCode NOT IN (N'ClosedWon', N'ClosedLost') THEN 1 ELSE 0 END),
        LastActivityAt = MAX(act.CreatedAt),
        TotalOpportunityValue = SUM(COALESCE(o.EstimatedValue, 0.0))
    FROM crm.Account a
    LEFT JOIN support.Ticket t ON t.AccountId = a.AccountId
    LEFT JOIN sales.Opportunity o ON o.AccountId = a.AccountId
    LEFT JOIN crm.Activity act ON act.AccountId = a.AccountId
    GROUP BY a.AccountId
)
SELECT
    tree.LevelNo,
    tree.HierarchyPath,
    tree.AccountId,
    tree.AccountName,
    score.OpenTicketCount,
    score.OpenOpportunityCount,
    score.TotalOpportunityValue,
    score.LastActivityAt
FROM AccountTree tree
LEFT JOIN AccountScore score ON score.AccountId = tree.AccountId
ORDER BY tree.HierarchyPath
OPTION (MAXRECURSION 100);

/*
Query 3: "Top N contacts per account" with CROSS APPLY and tie handling
*/
SELECT
    a.AccountId,
    a.AccountName,
    topContacts.ContactId,
    topContacts.FullName,
    topContacts.Email,
    topContacts.LastActivityAt,
    topContacts.ActivityCount
FROM crm.Account a
CROSS APPLY
(
    SELECT TOP (3)
        c.ContactId,
        c.FullName,
        c.Email,
        LastActivityAt = MAX(act.CreatedAt),
        ActivityCount = COUNT(act.ActivityId)
    FROM crm.Contact c
    LEFT JOIN crm.Activity act ON act.ContactId = c.ContactId
    WHERE c.AccountId = a.AccountId
    GROUP BY c.ContactId, c.FullName, c.Email
    ORDER BY COUNT(act.ActivityId) DESC, MAX(act.CreatedAt) DESC, c.ContactId
) topContacts
ORDER BY a.AccountName, topContacts.ActivityCount DESC;

/*
Query 4: Paging over support SLA snapshot view
*/
DECLARE @TicketPage INT = 2;
DECLARE @TicketPageSize INT = 25;

SELECT
    TicketId,
    Subject,
    StatusCode,
    SeverityCode,
    OpenedAt,
    ClosedAt,
    SLAHours,
    ElapsedHours,
    IsSlaBreached,
    AccountName,
    OwnerName
FROM support.vwTicketSlaSnapshot
ORDER BY OpenedAt DESC, TicketId DESC
OFFSET (@TicketPage - 1) * @TicketPageSize ROWS
FETCH NEXT @TicketPageSize ROWS ONLY;
