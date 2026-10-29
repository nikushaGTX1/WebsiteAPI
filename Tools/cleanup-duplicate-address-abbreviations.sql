-- One-time cleanup: repeated street abbreviations in listing addresses,
-- for example "შარაშიძე ქ. ქ., 30, დიღომი 1-9" -> "შარაშიძე ქ., 30, დიღომი 1-9".
-- New and edited listings are cleaned by ListingTextNormalizer.CleanAddress, which uses the same rule.
--
-- Do not run this on production before reviewing step 1 on a copy of the data.
-- Usage: psql "$DATABASE_URL" -f Tools/cleanup-duplicate-address-abbreviations.sql
-- The script ends with ROLLBACK. Change it to COMMIT only after the preview looks right.

BEGIN;

-- The same short abbreviation twice or more in a row (ქ. ქ., გამზ. გამზ., ...).
-- Group 1 keeps the space or start of text before it.
CREATE TEMP VIEW address_fix AS
SELECT
    "Id",
    "Address" AS old_address,
    regexp_replace("Address", '(^|\s)([^\s,.0-9]{1,6}\.)(\s+\2)+(?=[\s,]|$)', '\1\2', 'g') AS new_address,
    "Street" AS old_street,
    regexp_replace("Street", '(^|\s)([^\s,.0-9]{1,6}\.)(\s+\2)+(?=[\s,]|$)', '\1\2', 'g') AS new_street
FROM "Apartments";

-- Step 1: preview.
SELECT "Id", old_address, new_address, old_street, new_street
FROM address_fix
WHERE old_address IS DISTINCT FROM new_address OR old_street IS DISTINCT FROM new_street
ORDER BY "Id";

-- Step 2: apply.
UPDATE "Apartments" AS apartment
SET "Address" = fix.new_address,
    "Street" = fix.new_street
FROM address_fix AS fix
WHERE apartment."Id" = fix."Id"
  AND (fix.old_address IS DISTINCT FROM fix.new_address OR fix.old_street IS DISTINCT FROM fix.new_street);

ROLLBACK; -- change to COMMIT after reviewing the preview
