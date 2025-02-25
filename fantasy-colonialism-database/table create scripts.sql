/*A point represents a single pixel within the provided seed map.
All white (#FFFFFF and black #000000) tiles within the sample map will be given a point.*/
CREATE TABLE Points (
	id numeric PRIMARY KEY,
    x numeric,
    y numeric,
    provinceId numeric
);

/*A province is a collection of contiguous points.*/
CREATE TABLE Provinces (
	id numeric PRIMARY KEY,
    name varchar(255),
    avgX numeric,
    avgY numeric
);