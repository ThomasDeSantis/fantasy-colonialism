DROP TABLE IF EXISTS "RenderEdges";
CREATE TABLE "RenderEdges" (
	id SERIAL PRIMARY KEY,
    x1 decimal(6,2) NOT NULL,
	y1 decimal(6,2) NOT NULL,
    x2 decimal(6,2) NOT NULL,
	y2 decimal(6,2) NOT NULL,
    provinceId int NOT NULL,
	consumed bool DEFAULT false
);