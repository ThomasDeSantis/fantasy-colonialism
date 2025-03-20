CREATE TABLE renderEdges (
	id int NOT NULL AUTO_INCREMENT PRIMARY KEY,
    x1 decimal(6,2) NOT NULL,
	y1 decimal(6,2) NOT NULL,
    x2 decimal(6,2) NOT NULL,
	y2 decimal(6,2) NOT NULL,
    provinceId int NOT NULL,
	consumed bool DEFAULT false
    );