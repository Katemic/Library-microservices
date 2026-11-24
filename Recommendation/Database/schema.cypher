CREATE CONSTRAINT item_id_unique IF NOT EXISTS
FOR (i:item)
REQUIRE i.id IS UNIQUE;

CREATE CONSTRAINT loaner_id_unique IF NOT EXISTS
FOR (u:Loaner)
REQUIRE u.id IS UNIQUE;

CREATE CONSTRAINT language_name_unique IF NOT EXISTS
FOR (l:language)
REQUIRE l.name IS UNIQUE;

CREATE CONSTRAINT publisher_id_unique IF NOT EXISTS
FOR (p:publisher)
REQUIRE p.id IS UNIQUE;

CREATE CONSTRAINT creator_id_unique IF NOT EXISTS
FOR (c:creator)
REQUIRE c.id IS UNIQUE;

CREATE CONSTRAINT genre_name_unique IF NOT EXISTS
FOR (g:genre)
REQUIRE g.name IS UNIQUE;


CREATE CONSTRAINT tag_name_unique IF NOT EXISTS
FOR (t:tag)
REQUIRE t.name IS UNIQUE;

CREATE CONSTRAINT book_id_unique IF NOT EXISTS
FOR (b:book)
REQUIRE b.id IS UNIQUE;

CREATE CONSTRAINT boardgame_id_unique IF NOT EXISTS
FOR (b:boardgame)
REQUIRE b.id IS UNIQUE;
