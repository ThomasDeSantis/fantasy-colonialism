DROP PROCEDURE IF EXISTS sp_TRUNCATE_POINTS;

CREATE OR REPLACE PROCEDURE sp_TRUNCATE_POINTS()
LANGUAGE plpgsql
AS $$
BEGIN
    TRUNCATE TABLE "Points";
    TRUNCATE TABLE "WorldPoints";
END;
$$;