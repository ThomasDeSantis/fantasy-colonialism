USE `fantasy_colonialism_backend_dev`;
DROP function IF EXISTS `fnc_RENDER_FIND_CLOSEST_VALID_NODE`;

DELIMITER $$
USE `fantasy_colonialism_backend_dev`$$
CREATE FUNCTION `fnc_RENDER_FIND_CLOSEST_VALID_NODE` (x_input decimal(6,2),y_input decimal(6,2),pid_input int)
RETURNS INTEGER
READS SQL DATA
BEGIN
	DECLARE id_output INT DEFAULT -1;
    
	SELECT id
    INTO id_output
    FROM (
        SELECT x2, y2, id,
		(
		   acos(cos(radians(x_input)) * 
		   cos(radians(x2)) * 
		   cos(radians(y2) - 
		   radians(y_input)) + 
		   sin(radians(x_input)) * 
		   sin(radians(y2)))
		)AS distance 
        FROM renderEdges
        WHERE provinceId = pid_input AND consumed = FALSE
        UNION
        SELECT x1, y1, id,
		(
		   acos(cos(radians(x_input)) * 
		   cos(radians(x1)) * 
		   cos(radians(y1) - 
		   radians(y_input)) + 
		   sin(radians(x_input)) * 
		   sin(radians(y1)))
		)AS distance 
        FROM renderEdges
        WHERE provinceId = pid_input AND consumed = FALSE
		)AS edges
	WHERE distance IS NOT NULL
	ORDER BY DISTANCE
	LIMIT 1;
    
RETURN id_output;
END$$

DELIMITER ;

