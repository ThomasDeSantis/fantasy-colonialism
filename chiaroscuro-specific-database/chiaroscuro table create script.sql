--Represents the hex that a point is located in.
CREATE TABLE "ChHexes" (
    id int NOT NULL PRIMARY KEY,
    xOrigin numeric NOT NULL, -- X coordinate of the point in the view.
    yOrigin numeric NOT NULL -- Y coordinate of the point in the view.
);

--Tables specific to the chiaroscuro subproject
--Should utilize all main logic from the general fantasy colonialism project
CREATE TABLE "ChPointExtension" (
    id int NOT NULL PRIMARY KEY,
    hexId int NOT NULL,
    FOREIGN KEY (id) REFERENCES "Points"(id),
    FOREIGN KEY (hexId) REFERENCES "ChHexes"(id)
);
