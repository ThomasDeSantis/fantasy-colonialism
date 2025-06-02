DROP TABLE IF EXISTS `Points`;
DROP TABLE IF EXISTS `WorldPoints`;

/*A world point is a simpler table used for points that represent the entire globe.*/
CREATE TABLE WorldPoints (
	id int NOT NULL AUTO_INCREMENT PRIMARY KEY,
    x numeric,
    y numeric,
    land bool NOT NULL DEFAULT true,
    coastal bool NOT NULL default FALSE,
    height numeric NOT NULL default 0
    );

/*A point represents a single pixel within the provided seed map.
All white (#FFFFFF and black #000000) tiles within the sample map will be given a point.*/
CREATE TABLE Points (
	id int NOT NULL AUTO_INCREMENT,
    worldPointId int NOT NULL ,
    x numeric,
    y numeric,
    land bool NOT NULL DEFAULT true,
    waterSalinity decimal (3,1), /*NULL if a land point, otherwise the percentage of salt in the water. Ocean standard is 3.5%*/
    provinceId numeric NOT NULL DEFAULT -1,
    PRIMARY KEY (id),
    CONSTRAINT `worldPointId_fk` FOREIGN KEY (worldPointId) REFERENCES WorldPoints(id) ON DELETE CASCADE
);
    
DROP TABLE IF EXISTS `Provinces`;
/*A province is a collection of contiguous points.*/
CREATE TABLE Provinces (
	id int NOT NULL,
    name varchar(255) NULL,
    avgX numeric,
    avgY numeric
);
