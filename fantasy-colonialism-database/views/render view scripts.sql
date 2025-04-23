CREATE VIEW renderNodes AS
SELECT x1 as x,y1 as y,provinceId FROM renderEdges
UNION
SELECT x2,y2,provinceId FROM renderEdges;