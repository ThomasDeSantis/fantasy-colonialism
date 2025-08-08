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
      x numeric NOT NULL, -- X coordinate of the point in the view.
      y numeric NOT NULL, -- Y coordinate of the point in the view.
      land boolean NOT NULL DEFAULT true,
      waterSalinity decimal(3,1), -- NULL if a land point, otherwise the percentage of salt in the water.
      provinceId numeric NOT NULL DEFAULT -1,
      height numeric NOT NULL DEFAULT 0, -- Redundancy with world points for purposes of not having to join to retrieve the field.
      latitude decimal (5,3) NULL, -- Latitude in degrees, -90 to 90. Redudancy with world points for purposes of not having to join to retrieve the field.
      longitude decimal (6,3) NULL, -- Longitude in degrees, -180 to 180. Redudancy with world points for purposes of not having to join to retrieve the field.
      coastalDistance decimal (7,3) NULL, -- Distance to the nearest oceanic coast in kilometers.
      closestCoastalPoint int NULL, -- The ID of the closest coastal point.
      width numeric NULL, -- Width (N/S) of the point in meters.
      length numeric NULL, -- Length (E/W) of the point in meters.
      area numeric NULL, -- Area of the point in square meters.
      summerSolsticeAverageTemperature decimal(3, 1) NULL, -- Average temperature in degrees Celsius during the summer solstice.
      winterSolsticeAverageTemperature decimal(3, 1) NULL, -- Average temperature in degrees Celsius during the winter solstice.
      averageRainfall numeric NULL, -- Average yearly rainfall in millimeters.
      type pointType NOT NULL DEFAULT 'undefined', -- Type of the point, can be land, ocean, or lake.
      terrainType terrainType NOT NULL DEFAULT 'undefined' -- Type of terrain that defines the roughness of the point. Defined at height gen.
);

-- A province is a collection of contiguous points.
CREATE TABLE "Provinces" (
     id int NOT NULL,
     name varchar(255),
     avgX numeric,
     avgY numeric
);

CREATE TYPE waterType AS ENUM (
    'river', -- A river that flows into the ocean or another river.
    'lake', -- A lake that does not flow into anything.
    'ocean' -- The ocean, which is the end point for rivers.
    );

CREATE TYPE pointType AS ENUM (
    'land', -- A point that is land, not part of any water body.
    'ocean', -- A point that is part of the ocean.
    'lake', -- A point that is completely made up of a lake.
    'undefined'
);

CREATE TYPE terrainType AS ENUM (
    'undefined', -- A point that has not been assigned a terrain type.
    'flatland',
    'hills',
    'mountains'
);

--Header table that holds general information about the river.
--Rivers can be made up of multiple points, and the points will never be shared between them.
--A river will empty into the ocean, a lake, or another river.
CREATE TABLE "Rivers"(
    id SERIAL PRIMARY KEY,
    name varchar(255) NULL, -- Will be entered by player.
    tributaryId int NULL, -- The id of the river or body of water. NULL if it does not feed into another river or body of water, or feeds into the ocean.
    tributaryType waterType NULL -- The type of water the river feeds into.
);


-- This denotes a point that holds a river. Multiple rivers can share a point, but a river point can only be part of one river.
CREATE TABLE "RiverPoints" (
    id SERIAL PRIMARY KEY,
    riverId int NOT NULL, -- The ID of the river this point belongs to.
    pointId int NOT NULL, -- The ID of the point this river point belongs to.
    x numeric, -- X coordinate of the point in the world.
    y numeric, -- Y coordinate of the point in the world.
    headwater boolean NOT NULL DEFAULT false, -- True if this is a headwater point, false otherwise.
    depth decimal (4, 1) NOT NULL, -- Depth of the river at this point in meters.
    width numeric NOT NULL, -- Width of the river at this point in meters.
    averageSteepness decimal (4,1), -- Average steepness of the river at this point in degrees.
    discharge decimal (4,1) NOT NULL, -- Discharge of the river at this point in cubic meters per second.
    rockiness decimal (4,2) NOT NULL -- Rockiness of the river at this point, from 0.00 (smooth) to 1.00 (very rocky).
);

--Represents a waterfall that is part of a river.
--A point can have multiple waterfalls.
CREATE TABLE "Waterfalls" (
    id SERIAL PRIMARY KEY,
    riverPointId int NOT NULL, -- The ID of the river point this waterfall belongs to.
    heightTop numeric NOT NULL, -- Height of the waterfall in meters at the top.
    heightBottom numeric NOT NULL, -- Height of the waterfall in meters at the bottom.
    width numeric NOT NULL, -- Width of the waterfall in meters.
    discharge decimal (4,1) NOT NULL
);

CREATE TABLE "Lakes" (
    id SERIAL PRIMARY KEY,
    name varchar(255) NULL,
    waterSalinity decimal(3,1) NULL, -- The percentage of salt in the water.
    lakeRimHeight numeric NULL, -- Height of the lake surface in meters above sea level.
    lakeBedHeight numeric NULL -- Depth of the lake in meters.

);

CREATE TABLE "LakePoints" (
    id SERIAL PRIMARY KEY,
    lakeId int NOT NULL, -- The ID of the lake this point belongs to.
    pointId int NOT NULL, -- The ID of the point this lake point belongs to.
    x numeric, -- X coordinate of the point in the world.
    y numeric, -- Y coordinate of the point in the world.
    depth numeric NOT NULL, -- Average depth of the lake at this point in meters.
    pointAreaPercent decimal (5,1) NOT NULL -- Percentage of the point that is covered by the lake.
);