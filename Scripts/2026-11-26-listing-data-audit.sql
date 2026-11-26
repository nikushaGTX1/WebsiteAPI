-- Velven listing data audit (run by hand against the production database; review before UPDATE).
-- 1) Parking: the parking type says there is parking but the "Parking" box is unticked.
SELECT "Id", "Title", "ParkingCondition", "HasParking"
FROM "Apartments"
WHERE NOT "HasParking"
  AND "ParkingCondition" IN ('Garage', 'PrivateCourtyard', 'CourtyardWithBarrier', 'CourtyardWithoutBarrier');

-- Fix for 1) (the site already treats these as having parking; this makes the stored data agree):
-- UPDATE "Apartments" SET "HasParking" = TRUE
-- WHERE NOT "HasParking"
--   AND "ParkingCondition" IN ('Garage', 'PrivateCourtyard', 'CourtyardWithBarrier', 'CourtyardWithoutBarrier');

-- 2) Parking: the description promises a parking spot but the box is unticked (e.g. #636). Check each by hand.
SELECT "Id", "Title", LEFT("Description", 300) AS "Description"
FROM "Apartments"
WHERE NOT "HasParking"
  AND ("Description" ~* '(one|1|own|private|underground|closed|covered)\s+parking (space|spot|place|lot)'
       OR "Description" ~* '(garage|parking) (is )?included'
       OR "Description" ~ '(საკუთარი|კერძო|დახურული|მიწისქვეშა)\s+(პარკინგ|ავტოსადგომ)'
       OR "Description" ~ 'პარკინგის ადგილი');

-- 3) Districts: listings whose address or street names a different district than the one stored
--    (e.g. Bagebi or Vake stored as Saburtalo). Review and correct "District" by hand.
SELECT "Id", "Title", "District", "Street", "Address"
FROM "Apartments"
WHERE ("District" = 'Saburtalo' AND ("Address" ~* '(bagebi|ბაგები|vake|ვაკე)' OR "Title" ~* '(bagebi|ბაგები|vake|ვაკე)'))
   OR ("District" = 'Vake' AND ("Address" ~* '(saburtalo|საბურთალო|bagebi|ბაგები)' OR "Title" ~* '(saburtalo|საბურთალო|bagebi|ბაგები)'))
ORDER BY "District", "Id";

-- 4) Pets: pet-friendly listings without the new pet type / size lines. Agents should edit these
--    and tick which pets and sizes are allowed (until then, dog owners see them last, marked
--    "Check with the agent about pets").
SELECT "Id", "Title"
FROM "Apartments"
WHERE "IsPetFriendly" AND "Description" !~* 'Pets allowed:';
