using Microsoft.EntityFrameworkCore;

namespace IbKarte.Api.Daten;

public class KarteDb(DbContextOptions<KarteDb> options) : DbContext(options)
{
    public DbSet<Ort> Orte => Set<Ort>();
    public DbSet<Eintrag> Eintraege => Set<Eintrag>();
    public DbSet<Verbindung> Verbindungen => Set<Verbindung>();
    public DbSet<Quelle> Quellen => Set<Quelle>();
    public DbSet<EintragQuelle> EintragQuellen => Set<EintragQuelle>();
    public DbSet<VerbindungQuelle> VerbindungQuellen => Set<VerbindungQuelle>();
    public DbSet<OffeneFrage> OffeneFragen => Set<OffeneFrage>();
    public DbSet<Datenstand> Datenstand => Set<Datenstand>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Ort>(e =>
        {
            e.ToTable("orte");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(80);
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Adresse).HasColumnName("adresse");
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lon).HasColumnName("lon");
            e.Property(x => x.Genauigkeit).HasColumnName("genauigkeit").HasMaxLength(20);
            e.Property(x => x.Quelle).HasColumnName("quelle");
            e.Property(x => x.QuelleUrl).HasColumnName("quelle_url");
            e.Property(x => x.Land).HasColumnName("land");
            e.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(30);
        });

        b.Entity<Eintrag>(e =>
        {
            e.ToTable("eintraege");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(80);
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Typ).HasColumnName("typ").HasMaxLength(20);
            e.Property(x => x.Gruppe).HasColumnName("gruppe").HasMaxLength(20);
            e.Property(x => x.Umfeld).HasColumnName("umfeld").HasMaxLength(20);
            e.Property(x => x.Form).HasColumnName("form");
            e.Property(x => x.Info).HasColumnName("info");
            e.Property(x => x.Ordner).HasColumnName("ordner");
            e.Property(x => x.OrtKey).HasColumnName("ort_key");
            e.Property(x => x.OrtGrund).HasColumnName("ort_grund");
            e.Property(x => x.BildUrl).HasColumnName("bild_url");
            e.Property(x => x.BildQuelle).HasColumnName("bild_quelle");
            e.Property(x => x.BildX).HasColumnName("bild_x");
            e.Property(x => x.BildY).HasColumnName("bild_y");
            e.Property(x => x.BildZoom).HasColumnName("bild_zoom");
            e.HasOne(x => x.Ort).WithMany().HasForeignKey(x => x.OrtKey).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Name);
            e.ToTable(t => t.HasCheckConstraint("ck_eintraege_typ", "typ IN ('person','org','medium','ereignis')"));
        });

        b.Entity<Verbindung>(e =>
        {
            e.ToTable("verbindungen");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VonId).HasColumnName("von_id");
            e.Property(x => x.NachId).HasColumnName("nach_id");
            e.Property(x => x.Text).HasColumnName("text");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.Art).HasColumnName("art").HasMaxLength(20);
            e.HasOne(x => x.Von).WithMany().HasForeignKey(x => x.VonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Nach).WithMany().HasForeignKey(x => x.NachId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.VonId);
            e.HasIndex(x => x.NachId);
            e.ToTable(t => t.HasCheckConstraint("ck_verbindungen_status", "status IN ('belegt','vorwurf')"));
        });

        b.Entity<Quelle>(e =>
        {
            e.ToTable("quellen");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(20);
            e.Property(x => x.Titel).HasColumnName("titel");
            e.Property(x => x.Url).HasColumnName("url");
        });

        b.Entity<EintragQuelle>(e =>
        {
            e.ToTable("eintrag_quellen");
            e.HasKey(x => new { x.EintragId, x.QuelleId });
            e.Property(x => x.EintragId).HasColumnName("eintrag_id");
            e.Property(x => x.QuelleId).HasColumnName("quelle_id");
            e.HasOne<Eintrag>().WithMany(x => x.Quellen).HasForeignKey(x => x.EintragId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Quelle).WithMany().HasForeignKey(x => x.QuelleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VerbindungQuelle>(e =>
        {
            e.ToTable("verbindung_quellen");
            e.HasKey(x => new { x.VerbindungId, x.QuelleId });
            e.Property(x => x.VerbindungId).HasColumnName("verbindung_id");
            e.Property(x => x.QuelleId).HasColumnName("quelle_id");
            e.HasOne<Verbindung>().WithMany(x => x.Quellen).HasForeignKey(x => x.VerbindungId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Quelle).WithMany().HasForeignKey(x => x.QuelleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OffeneFrage>(e =>
        {
            e.ToTable("offene_fragen");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Text).HasColumnName("text");
        });

        b.Entity<Datenstand>(e =>
        {
            e.ToTable("datenstand");
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Stand).HasColumnName("stand");
            e.Property(x => x.ImportiertAm).HasColumnName("importiert_am");
            e.Property(x => x.Pruefsumme).HasColumnName("pruefsumme");
        });
    }
}
