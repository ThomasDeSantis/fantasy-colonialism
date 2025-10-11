CREATE INDEX ChPointId
ON "ChPointExtension"
USING HASH (id);

CREATE INDEX ChPointHexId
ON "ChPointExtension"
USING HASH (hexId);

CREATE INDEX ChHexesId
ON "ChHexes"
USING HASH (id);