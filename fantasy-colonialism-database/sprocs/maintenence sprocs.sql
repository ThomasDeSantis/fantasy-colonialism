-- Used to delete all data from the points and world points tables.
DROP procedure IF EXISTS `sp_TRUNCATE_POINTS`;

DELIMITER $$
CREATE PROCEDURE `sp_TRUNCATE_POINTS` ()
BEGIN
TRUNCATE TABLE Points;
ALTER TABLE Points DROP FOREIGN KEY `worldPointId_fk`;
TRUNCATE TABLE WorldPoints;
ALTER TABLE Points ADD CONSTRAINT `worldPointId_fk` FOREIGN KEY (worldPointId) REFERENCES WorldPoints(id) ON DELETE CASCADE;
END$$
CALL `sp_TRUNCATE_POINTS`();

DELIMITER ;
