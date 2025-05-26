-- Used to delete all data from the points and world points tables.
USE `fantasy_colonialism_backend_dev`;
DROP procedure IF EXISTS `sp_TRUNCATE_POINTS`;

DELIMITER $$
USE `fantasy_colonialism_backend_dev`$$
CREATE PROCEDURE `sp_TRUNCATE_POINTS` ()
BEGIN
TRUNCATE TABLE Points;
ALTER TABLE Points DROP FOREIGN KEY `worldPointId_fk`;
TRUNCATE TABLE WorldPoints;
ALTER TABLE Points ADD CONSTRAINT `worldPointId_fk` FOREIGN KEY (worldPointId) REFERENCES WorldPoints(id) ON DELETE CASCADE;
END$$
CALL `fantasy_colonialism_backend_dev`.`sp_TRUNCATE_POINTS`();

DELIMITER ;
