-- Used to reset render edges. Should be executed each time before you begin rendering edges.
USE `fantasy_colonialism_backend_dev`;
DROP procedure IF EXISTS `sp_RENDER_RESET_EDGE`;

DELIMITER $$
USE `fantasy_colonialism_backend_dev`$$
CREATE PROCEDURE `sp_RENDER_RESET_EDGE` ()
BEGIN
UPDATE renderEdges SET consumed = false;
END$$

DELIMITER ;

-- Used to take an return an edge that has not been consumed, provided an x,y coordinate and province id.
-- Will mark the edge as consumed so it cannot be used again.
USE `fantasy_colonialism_backend_dev`;
DROP procedure IF EXISTS `sp_RENDER_CONSUME_EDGE`;

USE `fantasy_colonialism_backend_dev`;
DROP procedure IF EXISTS `fantasy_colonialism_backend_dev`.`sp_RENDER_CONSUME_EDGE`;
;

DELIMITER $$
USE `fantasy_colonialism_backend_dev`$$
CREATE PROCEDURE `sp_RENDER_CONSUME_EDGE` (
    IN x_input DECIMAL(6,2),
    IN y_input DECIMAL(6,2),
    IN pid_input INT,
    OUT x_output DECIMAL(6,2),
    OUT y_output DECIMAL(6,2)
)
BEGIN
    DECLARE edge_id INT;

    -- Select the renderEdge to return and capture its ID
    SELECT x2, y2, id INTO x_output, y_output, edge_id
    FROM (
        SELECT x2, y2, id
        FROM renderEdges
        WHERE x1 = x_input AND y1 = y_input AND provinceId = pid_input AND consumed = FALSE
        UNION
        SELECT x1, y1, id
        FROM renderEdges
        WHERE x2 = x_input AND y2 = y_input AND provinceId = pid_input AND consumed = FALSE
    ) AS edges
    LIMIT 1;
    
    
	IF edge_id IS NOT NULL
		THEN
			-- Update the consumed status of the selected renderEdge if we were able to retrieve one
			UPDATE renderEdges
			SET consumed = TRUE
			WHERE id = edge_id;
		ELSE
			SELECT x1,y1 INTO x_output, y_output
            FROM renderEdges
            WHERE provinceId = pid_input AND consumed = false
            LIMIT 1;
		END IF;
END;$$

DELIMITER ;
;




