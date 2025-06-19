DROP TABLE IF EXISTS "Points";
DROP TABLE IF EXISTS "WorldPoints";
DROP TABLE IF EXISTS "Provinces";

-- A world point is a simpler table used for points that represent the entire globe.
CREATE TABLE "WorldPoints" (
    id SERIAL PRIMARY KEY,
    x numeric,
    y numeric,
    land boolean NOT NULL DEFAULT true,
    coastal boolean NOT NULL DEFAULT false,
    height numeric NOT NULL DEFAULT 0,
    latitude decimal (5,3), -- Latitude in degrees, -90 to 90.
    longitude decimal (6,3) -- Longitude in degrees, -180 to 180.
);

-- A point represents a single pixel within the provided seed map.
-- All white (#FFFFFF) and black (#000000) tiles within the sample map will be given a point.
CREATE TABLE "Points" (
      id SERIAL PRIMARY KEY,
      worldPointId int NOT NULL,
      x numeric,
      y numeric,
      land boolean NOT NULL DEFAULT true,
      waterSalinity decimal(3,1), -- NULL if a land point, otherwise the percentage of salt in the water.
      provinceId numeric NOT NULL DEFAULT -1,
      height numeric NOT NULL DEFAULT 0, -- Redundancy with world points for purposes of not having to join to retrieve the field.
      latitude decimal (5,3) NULL, -- Latitude in degrees, -90 to 90. Redudancy with world points for purposes of not having to join to retrieve the field.
      longitude decimal (6,3) NULL, -- Longitude in degrees, -180 to 180. Redudancy with world points for purposes of not having to join to retrieve the field.
      width numeric NULL, -- Width (N/S) of the point in meters.
      length numeric NULL, -- Length (E/W) of the point in meters.
      area numeric NULL -- Area of the point in square meters.
);

-- A province is a collection of contiguous points.
CREATE TABLE "Provinces" (
     id int NOT NULL,
     name varchar(255),
     avgX numeric,
     avgY numeric
);
