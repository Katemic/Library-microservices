using System;
using System.Collections.Generic;
using Catalog.Persistence.Write.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Persistence.Write;

public partial class CatalogWriteDbContext : DbContext
{
    public CatalogWriteDbContext(DbContextOptions<CatalogWriteDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Boardgame> Boardgames { get; set; }

    public virtual DbSet<Book> Books { get; set; }

    public virtual DbSet<Creator> Creators { get; set; }

    public virtual DbSet<FlywaySchemaHistory> FlywaySchemaHistories { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<Language> Languages { get; set; }

    public virtual DbSet<OutboxMessage> OutboxMessages { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Entities.Tag> Tags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Boardgame>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("boardgame");

            entity.HasIndex(e => e.ItemId, "item_id_idx").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgeGroup)
                .HasMaxLength(20)
                .HasColumnName("age_group");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.NoOfPlayers)
                .HasMaxLength(20)
                .HasColumnName("no_of_players");
            entity.Property(e => e.PlayTime)
                .HasMaxLength(20)
                .HasColumnName("play_time");

            entity.HasOne(d => d.Item).WithOne(p => p.Boardgame)
                .HasForeignKey<Boardgame>(d => d.ItemId)
                .HasConstraintName("fk_boardgame_item");
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("book");

            entity.HasIndex(e => e.Isbn, "ISBN_UNIQUE").IsUnique();

            entity.HasIndex(e => e.ItemId, "item_id_idx").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Isbn)
                .HasMaxLength(17)
                .HasColumnName("ISBN");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.NoOfPages).HasColumnName("no_of_pages");
            entity.Property(e => e.Version)
                .HasMaxLength(45)
                .HasColumnName("version");

            entity.HasOne(d => d.Item).WithOne(p => p.Book)
                .HasForeignKey<Book>(d => d.ItemId)
                .HasConstraintName("fk_book_item");
        });

        modelBuilder.Entity<Creator>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("creator");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Birthday).HasColumnName("birthday");
            entity.Property(e => e.Description)
                .HasColumnType("mediumtext")
                .HasColumnName("description");
            entity.Property(e => e.FirstName)
                .HasMaxLength(45)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .HasMaxLength(45)
                .HasColumnName("last_name");
        });

        modelBuilder.Entity<FlywaySchemaHistory>(entity =>
        {
            entity.HasKey(e => e.InstalledRank).HasName("PRIMARY");

            entity.ToTable("flyway_schema_history");

            entity.HasIndex(e => e.Success, "flyway_schema_history_s_idx");

            entity.Property(e => e.InstalledRank)
                .ValueGeneratedNever()
                .HasColumnName("installed_rank");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.Description)
                .HasMaxLength(200)
                .HasColumnName("description");
            entity.Property(e => e.ExecutionTime).HasColumnName("execution_time");
            entity.Property(e => e.InstalledBy)
                .HasMaxLength(100)
                .HasColumnName("installed_by");
            entity.Property(e => e.InstalledOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp")
                .HasColumnName("installed_on");
            entity.Property(e => e.Script)
                .HasMaxLength(1000)
                .HasColumnName("script");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasColumnName("type");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .HasColumnName("version");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("genre");

            entity.HasIndex(e => e.Name, "name_UNIQUE").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(45)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("item");

            entity.HasIndex(e => e.IsDeleted, "idx_item_is_deleted");

            entity.HasIndex(e => e.MediaType, "idx_item_media_type");

            entity.HasIndex(e => e.Name, "idx_item_name");

            entity.HasIndex(e => e.LanguageId, "language_id_idx");

            entity.HasIndex(e => e.PublisherId, "publisher_id_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AverageStars)
                .HasPrecision(2, 1)
                .HasColumnName("average_stars");
            entity.Property(e => e.DeletedAtUtc)
                .HasMaxLength(6)
                .HasColumnName("deleted_at_utc");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Image)
                .HasMaxLength(2048)
                .HasColumnName("image");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.MediaType)
                .HasColumnType("enum('book','boardgame')")
                .HasColumnName("media_type");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.PublisherId).HasColumnName("publisher_id");
            entity.Property(e => e.ReleaseYear).HasColumnName("release_year");
            entity.Property(e => e.ReviewSummary)
                .HasColumnType("text")
                .HasColumnName("review_summary");
            entity.Property(e => e.Version)
                .HasDefaultValueSql("'1'")
                .HasColumnName("version");

            entity.HasOne(d => d.Language).WithMany(p => p.Items)
                .HasForeignKey(d => d.LanguageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_item_language");

            entity.HasOne(d => d.Publisher).WithMany(p => p.Items)
                .HasForeignKey(d => d.PublisherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_item_publisher");

            entity.HasMany(d => d.Creators).WithMany(p => p.Items)
                .UsingEntity<Dictionary<string, object>>(
                    "ItemCreator",
                    r => r.HasOne<Creator>().WithMany()
                        .HasForeignKey("CreatorId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_itemcreator_creator"),
                    l => l.HasOne<Item>().WithMany()
                        .HasForeignKey("ItemId")
                        .HasConstraintName("fk_itemcreator_item"),
                    j =>
                    {
                        j.HasKey("ItemId", "CreatorId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("item_creator");
                        j.HasIndex(new[] { "CreatorId" }, "creator_id_idx");
                        j.IndexerProperty<int>("ItemId").HasColumnName("item_id");
                        j.IndexerProperty<int>("CreatorId").HasColumnName("creator_id");
                    });

            entity.HasMany(d => d.Genres).WithMany(p => p.Items)
                .UsingEntity<Dictionary<string, object>>(
                    "ItemGenre",
                    r => r.HasOne<Genre>().WithMany()
                        .HasForeignKey("GenreId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_itemgenre_genre"),
                    l => l.HasOne<Item>().WithMany()
                        .HasForeignKey("ItemId")
                        .HasConstraintName("fk_itemgenre_item"),
                    j =>
                    {
                        j.HasKey("ItemId", "GenreId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("item_genre");
                        j.HasIndex(new[] { "GenreId" }, "genre_id_idx");
                        j.IndexerProperty<int>("ItemId").HasColumnName("item_id");
                        j.IndexerProperty<int>("GenreId").HasColumnName("genre_id");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Items)
                .UsingEntity<Dictionary<string, object>>(
                    "ItemTag",
                    r => r.HasOne<Entities.Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_itemtag_tag"),
                    l => l.HasOne<Item>().WithMany()
                        .HasForeignKey("ItemId")
                        .HasConstraintName("fk_itemtag_item"),
                    j =>
                    {
                        j.HasKey("ItemId", "TagId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("item_tag");
                        j.HasIndex(new[] { "TagId" }, "tag_id_idx");
                        j.IndexerProperty<int>("ItemId").HasColumnName("item_id");
                        j.IndexerProperty<int>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("language");

            entity.HasIndex(e => e.Language1, "language_UNIQUE").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Language1)
                .HasMaxLength(45)
                .HasColumnName("language");
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("outbox_messages");

            entity.HasIndex(e => new { e.ItemId, e.ItemVersion }, "idx_outbox_item_version");

            entity.HasIndex(e => e.ProjectedAtUtc, "idx_outbox_projection");

            entity.HasIndex(e => e.PublishedAtUtc, "idx_outbox_publication");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventType)
                .HasMaxLength(100)
                .HasColumnName("event_type");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ItemVersion).HasColumnName("item_version");
            entity.Property(e => e.LastError)
                .HasColumnType("text")
                .HasColumnName("last_error");
            entity.Property(e => e.OccurredAtUtc)
                .HasMaxLength(6)
                .HasColumnName("occurred_at_utc");
            entity.Property(e => e.Payload).HasColumnName("payload");
            entity.Property(e => e.ProjectedAtUtc)
                .HasMaxLength(6)
                .HasColumnName("projected_at_utc");
            entity.Property(e => e.PublishedAtUtc)
                .HasMaxLength(6)
                .HasColumnName("published_at_utc");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("publisher");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(70)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => new { e.LoanerId, e.ItemId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("review");

            entity.HasIndex(e => e.ItemId, "item_id_idx");

            entity.Property(e => e.LoanerId).HasColumnName("loaner_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.NoOfStars)
                .HasPrecision(2, 1)
                .HasColumnName("no_of_stars");
            entity.Property(e => e.Text)
                .HasColumnType("text")
                .HasColumnName("text");

            entity.HasOne(d => d.Item).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("fk_review_item");
        });

        modelBuilder.Entity<Entities.Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("tag");

            entity.HasIndex(e => e.Name, "name_UNIQUE").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(45)
                .HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
