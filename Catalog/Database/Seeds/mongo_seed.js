
/**
 * MongoDB Catalog Read Model Seed
 *
 * Matches mysql_seed.sql
 * Database: catalog_read
 * Collection: items
 *
 * Run after MongoDB setup.js.
 *
 * Intentionally excludes ItemVersion / IsDeleted,
 * which are not present in MySQL V1.
 */

const catalogDb = db.getSiblingDB("catalog_read");
const items = catalogDb.getCollection("items");

const docs = [

    // ----------------------------------
    // ITEM 101 - BOOK
    // ----------------------------------
    {
        _id: NumberInt(101),
        Name: "The Lantern Archive",
        MediaType: "book",
        ReleaseYear: NumberInt(2021),
        Description: "A librarian discovers a hidden map.",
        ReviewSummary: "Readers enjoy the mystery.",
        AverageStars: 4.5,
        Image: "lantern-archive.jpg",

        BookDetails: {
            ISBN: "9780000000101",
            No_Of_Pages: NumberInt(320),
            Version: "First edition"
        },

        BoardgameDetails: null,

        Language: {
            _id: NumberInt(11),
            Name: "English"
        },

        Publisher: {
            _id: NumberInt(11),
            Name: "Northstar Books"
        },

        Creators: [
            {
                _id: NumberInt(21),
                FirstName: "Mira",
                LastName: "Ellis",
                Birthday: ISODate("1982-04-08T00:00:00.000Z"),
                Description: "Fiction writer"
            },
            {
                _id: NumberInt(22),
                FirstName: "Jonah",
                LastName: "Reed",
                Birthday: null,
                Description: "Author and game designer"
            }
        ],

        Genres: [
            { _id: NumberInt(31), Name: "Fantasy" },
            { _id: NumberInt(33), Name: "Mystery" }
        ],

        Tags: [
            { _id: NumberInt(41), Name: "Adventure" },
            { _id: NumberInt(43), Name: "Puzzle" }
        ],

        Reviews: [
            {
                _id: "101:9001",
                Loaner_Id: NumberInt(9001),
                Loaner_Name: null,
                object_id: NumberInt(101),
                No_Of_Stars: 5.0,
                Text: "Excellent worldbuilding."
            },
            {
                _id: "101:9002",
                Loaner_Id: NumberInt(9002),
                Loaner_Name: null,
                object_id: NumberInt(101),
                No_Of_Stars: 4.0,
                Text: "Fun, but slow at first."
            }
        ]
    },

    // ----------------------------------
    // ITEM 102 - BOARDGAME
    // ----------------------------------
    {
        _id: NumberInt(102),
        Name: "Harbor Traders",
        MediaType: "boardgame",
        ReleaseYear: NumberInt(2022),
        Description: "A strategy board game about competing merchants.",
        ReviewSummary: "Players praise the strategic choices.",
        AverageStars: 4.0,
        Image: "harbor-traders.jpg",

        BookDetails: null,

        BoardgameDetails: {
            item_id: NumberInt(102),
            No_Of_Players: "2-4",
            Play_Time: "45-60 minutes",
            Age_Group: "10+"
        },

        Language: {
            _id: NumberInt(11),
            Name: "English"
        },

        Publisher: {
            _id: NumberInt(12),
            Name: "Lantern Games"
        },

        Creators: [
            {
                _id: NumberInt(22),
                FirstName: "Jonah",
                LastName: "Reed",
                Birthday: null,
                Description: "Author and game designer"
            },
            {
                _id: NumberInt(24),
                FirstName: "Kai",
                LastName: "Andersen",
                Birthday: null,
                Description: "Board-game designer"
            }
        ],

        Genres: [
            { _id: NumberInt(32), Name: "Strategy" }
        ],

        Tags: [
            { _id: NumberInt(42), Name: "Family" },
            { _id: NumberInt(43), Name: "Puzzle" }
        ],

        Reviews: [
            {
                _id: "102:9001",
                Loaner_Id: NumberInt(9001),
                Loaner_Name: null,
                object_id: NumberInt(102),
                No_Of_Stars: 4.0,
                Text: "Great replay value."
            }
        ]
    },

    // ----------------------------------
    // ITEM 103 - BOOK WITH NULL FIELDS
    // ----------------------------------
    {
        _id: NumberInt(103),
        Name: "Northern Lights",
        MediaType: "book",
        ReleaseYear: NumberInt(2020),
        Description: "A mystery set in a remote northern town.",
        ReviewSummary: null,
        AverageStars: null,
        Image: null,

        BookDetails: {
            ISBN: "9780000000118",
            No_Of_Pages: NumberInt(248),
            Version: null
        },

        BoardgameDetails: null,

        Language: {
            _id: NumberInt(12),
            Name: "Danish"
        },

        Publisher: {
            _id: NumberInt(11),
            Name: "Northstar Books"
        },

        Creators: [
            {
                _id: NumberInt(23),
                FirstName: "Freya",
                LastName: "Sol",
                Birthday: ISODate("1990-11-02T00:00:00.000Z"),
                Description: null
            }
        ],

        Genres: [
            { _id: NumberInt(33), Name: "Mystery" }
        ],

        Tags: [
            { _id: NumberInt(44), Name: "Nordic" }
        ],

        Reviews: []
    }
];

// ----------------------------------
// INSERT OR UPDATE SEED DOCUMENTS
// ----------------------------------

for (const doc of docs) {
    const result = items.replaceOne(
        { _id: doc._id },
        doc,
        { upsert: true }
    );

    if (!result.acknowledged) {
        throw new Error(
            "Failed to seed item " + doc._id
        );
    }

    print(
        "Seeded MongoDB item " +
        doc._id + ": " + doc.Name
    );
}

print(
    "Seed complete: " +
    docs.length + " demo item projections."
);

