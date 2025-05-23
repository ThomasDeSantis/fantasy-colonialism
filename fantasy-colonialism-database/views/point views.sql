CREATE VIEW LandPoints AS
SELECT id, x, y, provinceId, type
FROM Points
WHERE land = true;

CREATE VIEW OceanPoints AS
SELECT id,x, y
FROM Points
WHERE type = 'ocean';

CREATE VIEW CoastPoints AS
SELECT id, x, y, provinceId, type
FROM Points p1
WHERE coastal = true;
    
CREATE VIEW BorderPoints AS
SELECT centerPoint.id, centerPoint.x, centerPoint.y, centerPoint.provinceId as centerProvince, northPoint.provinceId as northProvince, eastPoint.provinceId as eastProvince, southPoint.provinceId as southProvince, westPoint.provinceId as westProvince
FROM LandPoints centerPoint 
LEFT JOIN LandPoints northPoint ON centerPoint.x = northPoint.x AND centerPoint.y = northPoint.y - 1
LEFT JOIN LandPoints eastPoint ON centerPoint.x = eastPoint.x +1 AND centerPoint.y = eastPoint.y
LEFT JOIN LandPoints southPoint ON centerPoint.x = southPoint.x AND centerPoint.y = southPoint.y + 1
LEFT JOIN LandPoints westPoint ON centerPoint.x = westPoint.x -1 AND centerPoint.y = westPoint.y
WHERE (centerPoint.provinceId != northPoint.provinceId OR northPoint.provinceId is null OR
centerPoint.provinceId != eastPoint.provinceId OR eastPoint.provinceId is null OR
centerPoint.provinceId != southPoint.provinceId OR southPoint.provinceId is null OR
centerPoint.provinceId != westPoint.provinceId OR westPoint.provinceId is null);