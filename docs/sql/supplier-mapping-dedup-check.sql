-- Supplier product mapping duplicate check/cleanup
-- =====================================================
-- Run this BEFORE applying migration 20260815095435_AddUniqueSupplierProductMappingIndex if that
-- migration fails with "Duplicate (SupplierId, InternalProductId) rows exist...". The migration adds
-- a unique index on (SupplierId, InternalProductId), which cannot coexist with duplicate rows.
--
-- This script does NOT delete anything automatically. It is a manual, reviewed runbook:
--   1. Run STEP 1 to see if duplicates exist and how many.
--   2. If they exist, run STEP 2 to see the actual rows so a human can judge which one to keep.
--   3. Only after reviewing STEP 2's output, uncomment and run STEP 3 to remove the losing rows.
--   4. Re-run the migration.
--
-- "Losing" row selection in STEP 3 keeps, per (SupplierId, InternalProductId) group: the Active row
-- over an Inactive one, then the most recently modified/created row. Adjust the ORDER BY in the CTE
-- if a different tie-break makes more sense for the data you actually find.

-- STEP 1: does a duplicate group exist at all?
SELECT
    SupplierId,
    InternalProductId,
    COUNT(*) AS DuplicateRowCount
FROM [suppliers].[SupplierProductMappings]
GROUP BY SupplierId, InternalProductId
HAVING COUNT(*) > 1;

-- STEP 2: show the actual duplicate rows for review.
SELECT m.*
FROM [suppliers].[SupplierProductMappings] m
INNER JOIN (
    SELECT SupplierId, InternalProductId
    FROM [suppliers].[SupplierProductMappings]
    GROUP BY SupplierId, InternalProductId
    HAVING COUNT(*) > 1
) dup ON dup.SupplierId = m.SupplierId AND dup.InternalProductId = m.InternalProductId
ORDER BY m.SupplierId, m.InternalProductId, m.ModifiedOnUtc DESC;

-- STEP 3: (REVIEW STEP 2's OUTPUT FIRST — this is commented out on purpose)
-- Deletes every row in a duplicate group except the one ranked #1 by the tie-break below.
/*
;WITH Ranked AS (
    SELECT
        Id,
        ROW_NUMBER() OVER (
            PARTITION BY SupplierId, InternalProductId
            ORDER BY
                CASE WHEN Status = 'Active' THEN 0 ELSE 1 END,
                ISNULL(ModifiedOnUtc, CreatedOnUtc) DESC
        ) AS RowRank
    FROM [suppliers].[SupplierProductMappings]
)
DELETE m
FROM [suppliers].[SupplierProductMappings] m
INNER JOIN Ranked r ON r.Id = m.Id
WHERE r.RowRank > 1;
*/
