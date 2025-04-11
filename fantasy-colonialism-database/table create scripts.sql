/*A point represents a single pixel within the provided seed map.
All white (#FFFFFF and black #000000) tiles within the sample map will be given a point.*/
CREATE TABLE Points (
	id int NOT NULL AUTO_INCREMENT PRIMARY KEY,
    x numeric,
    y numeric,
    land bool NOT NULL DEFAULT true,
    waterSalinity decimal (3,1), /*NULL if a land point, otherwise the percentage of salt in the water. Ocean standard is 3.5%*/
    type enum('land', 'ocean', 'lake') NOT NULL DEFAULT 'land',
    provinceId numeric
);

/*A province is a collection of contiguous points.*/
CREATE TABLE Provinces (
	id int NOT NULL,
    name varchar(255),
    avgX numeric,
    avgY numeric
);

    