
/**
 * Catalog MongoDB - Initial Setup
 *
 * CQRS Read Database
 *
 * MySQL is the source of truth.
 * MongoDB stores denormalized item documents.
 *
 * This script:
 * 1. Creates the items collection
 * 2. Defines document validation
 * 3. Creates indexes
 *
 * It does NOT insert data.
 * Data synchronization will be handled
 * by the future CQRS background worker.
 */

const catalogDb = db.getSiblingDB("catalog_read");
const collectionName = "items";


// --------------------------------------
// Reusable schema for lookup objects
// Used by Genres and Tags
// --------------------------------------

const lookupSchema = {
    bsonType: "object",
    required: ["_id", "Name"],
    properties: {
        _id: {
            bsonType: "int"
        },
        Name: {
            bsonType: ["string", "null"]
        }
    }
};


// --------------------------------------
// ITEMS COLLECTION VALIDATION
// --------------------------------------

const validation = {
    $jsonSchema: {
        bsonType: "object",

        title: "Catalog item read projection",

        description:
            "Denormalized representation of a Catalog item",

        required: [
            "_id",
            "Name",
            "MediaType",
            "BookDetails",
            "BoardgameDetails",
            "Language",
            "Publisher",
            "Creators",
            "Genres",
            "Tags",
            "Reviews"
        ],

        properties: {

            // ----------------------------------
            // ITEM - MYSQL: item
            // ----------------------------------

            // Same ID as MySQL item.id
            _id: {
                bsonType: "int"
            },

            Name: {
                bsonType: "string"
            },

            MediaType: {
                enum: ["book", "boardgame"]
            },

            ReleaseYear: {
                bsonType: ["int", "null"],
                minimum: 1000,
                maximum: 2100
            },

            Description: {
                bsonType: ["string", "null"]
            },

            ReviewSummary: {
                bsonType: ["string", "null"]
            },

            Image: {
                bsonType: ["string", "null"]
            },

            AverageStars: {
                bsonType: [
                    "double",
                    "decimal",
                    "int",
                    "null"
                ],
                minimum: 0,
                maximum: 5
            },


            // ----------------------------------
            // BOOK DETAILS - MYSQL: book
            // ----------------------------------

            BookDetails: {
                bsonType: ["object", "null"],

                properties: {

                    ISBN: {
                        bsonType: ["string", "null"]
                    },

                    No_Of_Pages: {
                        bsonType: ["int", "null"],
                        minimum: 0
                    },

                    Version: {
                        bsonType: ["string", "null"]
                    }
                }
            },


            // ----------------------------------
            // BOARDGAME DETAILS
            // MYSQL: boardgame
            // ----------------------------------

            BoardgameDetails: {
                bsonType: ["object", "null"],

                required: ["item_id"],

                properties: {

                    item_id: {
                        bsonType: "int"
                    },

                    No_Of_Players: {
                        bsonType: ["string", "null"]
                    },

                    Play_Time: {
                        bsonType: ["string", "null"]
                    },

                    Age_Group: {
                        bsonType: ["string", "null"]
                    }
                }
            },


            // ----------------------------------
            // LANGUAGE - MYSQL: language
            // ----------------------------------

            Language: {
                bsonType: "object",

                required: ["_id", "Name"],

                properties: {

                    _id: {
                        bsonType: "int"
                    },

                    Name: {
                        bsonType: "string"
                    }
                }
            },


            // ----------------------------------
            // PUBLISHER - MYSQL: publisher
            // ----------------------------------

            Publisher: {
                bsonType: "object",

                required: ["_id", "Name"],

                properties: {

                    _id: {
                        bsonType: "int"
                    },

                    Name: {
                        bsonType: "string"
                    }
                }
            },


            // ----------------------------------
            // CREATORS
            // MYSQL: creator + item_creator
            // ----------------------------------

            Creators: {
                bsonType: "array",

                items: {
                    bsonType: "object",

                    required: ["_id"],

                    properties: {

                        _id: {
                            bsonType: "int"
                        },

                        FirstName: {
                            bsonType: ["string", "null"]
                        },

                        LastName: {
                            bsonType: ["string", "null"]
                        },

                        Birthday: {
                            bsonType: ["date", "null"]
                        },

                        Description: {
                            bsonType: ["string", "null"]
                        }
                    }
                }
            },


            // ----------------------------------
            // GENRES
            // MYSQL: genre + item_genre
            // ----------------------------------

            Genres: {
                bsonType: "array",
                items: lookupSchema
            },


            // ----------------------------------
            // TAGS
            // MYSQL: tag + item_tag
            // ----------------------------------

            Tags: {
                bsonType: "array",
                items: lookupSchema
            },


            // ----------------------------------
            // REVIEWS - MYSQL: review
            // ----------------------------------

            Reviews: {
                bsonType: "array",

                items: {
                    bsonType: "object",

                    required: [
                        "_id",
                        "Loaner_Id",
                        "object_id",
                        "No_Of_Stars"
                    ],

                    properties: {

                        // Composite MySQL key:
                        // item_id + loaner_id
                        // Example: "42:7"
                        _id: {
                            bsonType: "string"
                        },

                        // External User microservice ID
                        // No foreign key required
                        Loaner_Id: {
                            bsonType: "int"
                        },

                        // Optional - not available
                        // in Catalog MySQL schema
                        Loaner_Name: {
                            bsonType: ["string", "null"]
                        },

                        // Corresponds to review.item_id
                        object_id: {
                            bsonType: "int"
                        },

                        No_Of_Stars: {
                            bsonType: [
                                "double",
                                "decimal",
                                "int"
                            ],
                            minimum: 1,
                            maximum: 5
                        },

                        Text: {
                            bsonType: ["string", "null"]
                        }
                    }
                }
            },


            // ----------------------------------
            // FUTURE CQRS SYNCHRONIZATION
            // ----------------------------------

            // Will be added to MySQL in V2
            ItemVersion: {
                bsonType: ["int", "long"]
            },

            // Tombstone / soft deletion
            IsDeleted: {
                bsonType: "bool"
            }

        }
    }
};


// --------------------------------------
// VALIDATION OPTIONS
// --------------------------------------

const options = {

    validator: validation,

    validationLevel: "strict",

    validationAction: "error"
};


// --------------------------------------
// CREATE OR UPDATE COLLECTION
// --------------------------------------

if (!catalogDb.getCollectionNames()
    .includes(collectionName)) {

    catalogDb.createCollection(
        collectionName,
        options
    );

    print(
        "Created catalog_read.items with validation"
    );

} else {

    const result = catalogDb.runCommand({
        collMod: collectionName,
        ...options
    });

    if (result.ok !== 1) {
        throw new Error(
            "Unable to update items validation: " +
            tojson(result)
        );
    }

    print(
        "Updated catalog_read.items validation"
    );
}


// --------------------------------------
// CREATE INDEXES
// --------------------------------------

const items = catalogDb.getCollection(
    collectionName
);


// Search by item name
items.createIndex(
    { Name: 1 },
    { name: "idx_name" }
);


// Filter by media type and name
items.createIndex(
    {
        MediaType: 1,
        Name: 1
    },
    { name: "idx_media_type_name" }
);


// Filter by genre
items.createIndex(
    { "Genres._id": 1 },
    { name: "idx_genre_id" }
);


// Filter by tag
items.createIndex(
    { "Tags._id": 1 },
    { name: "idx_tag_id" }
);


// Filter by creator
items.createIndex(
    { "Creators._id": 1 },
    { name: "idx_creator_id" }
);


// Find reviews by loaner/user
items.createIndex(
    { "Reviews.Loaner_Id": 1 },
    { name: "idx_review_loaner" }
);


// --------------------------------------
// SETUP COMPLETE
// --------------------------------------

print(
    "Catalog read model initialized: " +
    catalogDb.getName() +
    "." +
    collectionName
);

print(
    "Indexes: " +
    items.getIndexes()
        .map(i => i.name)
        .join(", ")
);

print(
    "No Catalog data was seeded."
);

print(
    "Items will be populated by the CQRS projector."
);
