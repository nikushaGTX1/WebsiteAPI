# Didi Dighomi real-estate coverage

This polygon intentionally does not use OpenStreetMap relation 18183807.
That relation covers only the northern neighbourhood core and ends near
latitude 41.7871.

The v2 coverage was derived on 2026-08-25 from:

- SS.ge real-estate location catalog, subdistrict 45 (Didi digomi), which
  classifies the included streets and publishes representative coordinates.
  Asmati Street is street 1200 at [44.752354, 41.785076].
- OpenStreetMap named-road line geometry downloaded through the standard map
  API for the SS.ge-classified street network. This supplies real road paths,
  including Asmati, Tarieli and the southern Rostevani segments, rather than
  the unrelated neighbourhood relation.

Generation method:

1. Collect all non-null SS.ge subdistrict-45 street coordinates.
2. Add vertices from corresponding named-road lines in the local
   44.72-44.78 / 41.765-41.805 extract.
3. Build a 3 km maximum-edge concave hull.
4. Apply a 180 metre coverage margin and simplify at 0.00018 degrees.
5. Round output coordinates to seven decimal places for drawing-engine
   compatibility while retaining sub-metre precision.

The margin represents address/building coverage around road centre lines. It
is not an administrative claim. The result is stored in
DidiDigomiCoverage.BoundaryGeoJson and is shared by public map geometry,
point resolution, street validation and apartment location backfill.

Source classification:
https://home.ss.ge/en/real-estate/l/Flat/For-Sale?cityIdList=95&subdistrictIds=45
