CREATE INDEX xy_p
ON "Points" (x,y);

CREATE INDEX provinceId
ON "Points"
USING HASH (provinceId);

CREATE INDEX worldPointId
ON "Points"
USING HASH (worldPointId);

CREATE INDEX xy_wp
ON "WorldPoints" (x,y);

CREATE INDEX id_wp
ON "WorldPoints"
USING HASH (id);

CREATE INDEX id_p
ON "Points"
USING HASH (id);