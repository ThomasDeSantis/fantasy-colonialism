CREATE VIEW BorderPoints AS
SELECT centerPoint.id, centerPoint.x, centerPoint.y, centerPoint.provinceId as centerProvince, northPoint.provinceId as northProvince, eastPoint.provinceId as eastProvince, southPoint.provinceId as southProvince, westPoint.provinceId as westProvince
FROM Points centerPoint 
LEFT JOIN Points northPoint ON centerPoint.x = northPoint.x AND centerPoint.y = northPoint.y - 1
LEFT JOIN Points eastPoint ON centerPoint.x = eastPoint.x +1 AND centerPoint.y = eastPoint.y
LEFT JOIN Points southPoint ON centerPoint.x = southPoint.x AND centerPoint.y = southPoint.y + 1
LEFT JOIN Points westPoint ON centerPoint.x = westPoint.x -1 AND centerPoint.y = westPoint.y
WHERE centerPoint.provinceId != northPoint.provinceId OR northPoint.provinceId is null OR
centerPoint.provinceId != eastPoint.provinceId OR eastPoint.provinceId is null OR
centerPoint.provinceId != southPoint.provinceId OR southPoint.provinceId is null OR
centerPoint.provinceId != westPoint.provinceId OR westPoint.provinceId is null;