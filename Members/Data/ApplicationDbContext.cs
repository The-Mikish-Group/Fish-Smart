using Members.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Members.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        // Existing Members DbSets
        public DbSet<UserProfile> UserProfile { get; set; }
        public DbSet<PDFCategory> PDFCategories { get; set; }
        public DbSet<CategoryFile> CategoryFiles { get; set; }
        public DbSet<Members.Models.File> Files { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<UserCredit> UserCredits { get; set; }
        public DbSet<BillableAsset> BillableAssets { get; set; }
        public DbSet<CreditApplication> CreditApplications { get; set; }
        public DbSet<ColorVar> ColorVars { get; set; }

        // Task System DbSets
        public DbSet<AdminTask> AdminTasks { get; set; }
        public DbSet<AdminTaskInstance> AdminTaskInstances { get; set; }
        public DbSet<TaskStatusMessage> TaskStatusMessages { get; set; }

        // Background Removal Usage Tracking
        public DbSet<BackgroundRemovalUsage> BackgroundRemovalUsage { get; set; }

        // Fish-Smart DbSets
        public DbSet<SmartCatchProfile> SmartCatchProfiles { get; set; }
        public DbSet<FishSpecies> FishSpecies { get; set; }
        public DbSet<UserAvatar> UserAvatars { get; set; }
        public DbSet<AvatarPose> AvatarPoses { get; set; }
        public DbSet<Background> Backgrounds { get; set; }
        public DbSet<Sponsor> Sponsors { get; set; }
        public DbSet<Outfit> Outfits { get; set; }
        public DbSet<FishingEquipment> FishingEquipment { get; set; }
        public DbSet<BaitsLures> BaitsLures { get; set; }
        public DbSet<FishingSession> FishingSessions { get; set; }
        public DbSet<Catch> Catches { get; set; }
        public DbSet<CatchAlbum> CatchAlbums { get; set; }
        public DbSet<AlbumCatches> AlbumCatches { get; set; }
        public DbSet<FishingBuddies> FishingBuddies { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Existing Members configurations
            
            // Configure PDFCategory - ensure CategoryID is auto-generated
            builder.Entity<PDFCategory>()
                .Property(p => p.CategoryID)
                .ValueGeneratedOnAdd();
            
            builder.Entity<BillableAsset>()
                .HasIndex(ba => ba.PlotID)
                .IsUnique();

            builder.Entity<BillableAsset>()
                .HasOne(ba => ba.User)
                .WithMany()
                .HasForeignKey(ba => ba.UserID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure CreditApplication relationships
            builder.Entity<CreditApplication>(entity =>
            {
                entity.HasOne(ca => ca.UserCredit)
                    .WithMany()
                    .HasForeignKey(ca => ca.UserCreditID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ca => ca.Invoice)
                    .WithMany()
                    .HasForeignKey(ca => ca.InvoiceID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure BackgroundRemovalUsage relationships and indexes
            builder.Entity<BackgroundRemovalUsage>(entity =>
            {
                entity.HasOne(bru => bru.User)
                    .WithMany()
                    .HasForeignKey(bru => bru.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(bru => bru.Invoice)
                    .WithMany()
                    .HasForeignKey(bru => bru.InvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Index for efficient monthly usage queries
                entity.HasIndex(bru => new { bru.UserId, bru.UsageYear, bru.UsageMonth })
                    .HasDatabaseName("IX_BackgroundRemovalUsage_UserMonthYear");

                // Index for billing queries
                entity.HasIndex(bru => new { bru.HasBeenInvoiced, bru.IsWithinFreeLimit })
                    .HasDatabaseName("IX_BackgroundRemovalUsage_Billing");
            });

            // Configure Task System relationships
            builder.Entity<AdminTaskInstance>(entity =>
            {
                entity.HasOne(ati => ati.AdminTask)
                    .WithMany(at => at.TaskInstances)
                    .HasForeignKey(ati => ati.TaskID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ati => ati.AssignedToUser)
                    .WithMany()
                    .HasForeignKey(ati => ati.AssignedToUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(ati => ati.CompletedByUser)
                    .WithMany()
                    .HasForeignKey(ati => ati.CompletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Ensure unique constraint for Task + Year + Month
                entity.HasIndex(ati => new { ati.TaskID, ati.Year, ati.Month })
                    .IsUnique();
            });

            builder.Entity<TaskStatusMessage>(entity =>
            {
                entity.HasOne(tsm => tsm.User)
                    .WithMany()
                    .HasForeignKey(tsm => tsm.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Fish-Smart Configurations

            // SmartCatchProfile - unique per user
            builder.Entity<SmartCatchProfile>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // Configure SmartCatchProfile relationship explicitly
            builder.Entity<SmartCatchProfile>()
                .HasMany(p => p.UserAvatars)
                .WithOne()
                .HasForeignKey(a => a.UserId)
                .HasPrincipalKey(p => p.UserId);

            builder.Entity<SmartCatchProfile>()
                .HasMany(p => p.FishingSessions)
                .WithOne()
                .HasForeignKey(s => s.UserId)
                .HasPrincipalKey(p => p.UserId);

            builder.Entity<SmartCatchProfile>()
                .HasMany(p => p.CatchAlbums)
                .WithOne()
                .HasForeignKey(a => a.UserId)
                .HasPrincipalKey(p => p.UserId);

            // AlbumCatches - composite primary key
            builder.Entity<AlbumCatches>()
                .HasKey(ac => new { ac.AlbumId, ac.CatchId });

            // FishingBuddies - unique buddy relationship
            builder.Entity<FishingBuddies>()
                .HasIndex(fb => new { fb.OwnerUserId, fb.BuddyUserId })
                .IsUnique();

            // Configure decimal precision for measurements
            builder.Entity<FishSpecies>()
                .Property(f => f.MinSize)
                .HasPrecision(5, 2);

            builder.Entity<FishSpecies>()
                .Property(f => f.MaxSize)
                .HasPrecision(5, 2);

            builder.Entity<Catch>()
                .Property(c => c.Size)
                .HasPrecision(5, 2);

            builder.Entity<Catch>()
                .Property(c => c.Weight)
                .HasPrecision(5, 2);

            // Configure weather data precision for Catch
            builder.Entity<Catch>()
                .Property(c => c.Temperature)
                .HasPrecision(5, 2);

            builder.Entity<Catch>()
                .Property(c => c.WindSpeed)
                .HasPrecision(5, 2);

            builder.Entity<Catch>()
                .Property(c => c.BarometricPressure)
                .HasPrecision(7, 2);

            // Configure location precision
            builder.Entity<FishingSession>()
                .Property(f => f.Latitude)
                .HasPrecision(10, 8);

            builder.Entity<FishingSession>()
                .Property(f => f.Longitude)
                .HasPrecision(11, 8);

            // Configure temperature and environmental data precision
            builder.Entity<FishingSession>()
                .Property(f => f.Temperature)
                .HasPrecision(5, 2);

            builder.Entity<FishingSession>()
                .Property(f => f.WindSpeed)
                .HasPrecision(5, 2);

            builder.Entity<FishingSession>()
                .Property(f => f.BarometricPressure)
                .HasPrecision(6, 2);

            // Configure string fields to prevent padding issues
            builder.Entity<Catch>()
                .Property(c => c.PhotoUrl)
                .HasColumnType("VARCHAR(500)");

            builder.Entity<CatchAlbum>()
                .Property(a => a.CoverImageUrl)
                .HasColumnType("VARCHAR(500)");

            builder.Entity<Background>()
                .Property(b => b.ImageUrl)
                .HasColumnType("VARCHAR(500)");

            // Performance indexes for Fish-Smart
            builder.Entity<FishSpecies>()
                .HasIndex(f => new { f.WaterType, f.Region });

            builder.Entity<FishSpecies>()
                .HasIndex(f => f.IsActive);

            builder.Entity<UserAvatar>()
                .HasIndex(a => a.UserId);

            builder.Entity<Background>()
                .HasIndex(b => new { b.WaterType, b.IsPremium });

            builder.Entity<FishingEquipment>()
                .HasIndex(e => new { e.Type, e.IsPremium });

            builder.Entity<FishingSession>()
                .HasIndex(s => new { s.UserId, s.SessionDate, s.WaterType });

            builder.Entity<Catch>()
                .HasIndex(c => c.SessionId);

            builder.Entity<Catch>()
                .HasIndex(c => c.FishSpeciesId);

            builder.Entity<Catch>()
                .HasIndex(c => c.IsShared);

            builder.Entity<CatchAlbum>()
                .HasIndex(a => a.UserId);

            // Configure Fish-Smart relationships with proper cascade behavior

            // Catches relationships - avoid cascade conflicts
            builder.Entity<Catch>()
                .HasOne(c => c.Session)
                .WithMany(s => s.Catches)
                .HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Catch>()
                .HasOne(c => c.Species)
                .WithMany(s => s.Catches)
                .HasForeignKey(c => c.FishSpeciesId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Catch>()
                .HasOne(c => c.Avatar)
                .WithMany(a => a.Catches)
                .HasForeignKey(c => c.AvatarId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Catch>()
                .HasOne(c => c.Pose)
                .WithMany(p => p.Catches)
                .HasForeignKey(c => c.PoseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Catch>()
                .HasOne(c => c.Background)
                .WithMany(b => b.Catches)
                .HasForeignKey(c => c.BackgroundId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Catch>()
                .HasOne(c => c.Outfit)
                .WithMany(o => o.Catches)
                .HasForeignKey(c => c.OutfitId)
                .OnDelete(DeleteBehavior.NoAction);

            // AlbumCatches relationships - avoid cascade conflicts
            builder.Entity<AlbumCatches>()
                .HasOne(ac => ac.Album)
                .WithMany(a => a.AlbumCatches)
                .HasForeignKey(ac => ac.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AlbumCatches>()
                .HasOne(ac => ac.Catch)
                .WithMany(c => c.AlbumCatches)
                .HasForeignKey(ac => ac.CatchId)
                .OnDelete(DeleteBehavior.NoAction);

            // Sponsor relationships
            builder.Entity<Outfit>()
                .HasOne(o => o.Sponsor)
                .WithMany(s => s.Outfits)
                .HasForeignKey(o => o.SponsorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<FishingEquipment>()
                .HasOne(e => e.Sponsor)
                .WithMany(s => s.FishingEquipment)
                .HasForeignKey(e => e.SponsorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<BaitsLures>()
                .HasOne(bl => bl.Sponsor)
                .WithMany(s => s.BaitsLures)
                .HasForeignKey(bl => bl.SponsorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Fishing Session equipment relationships
            builder.Entity<FishingSession>()
                .HasOne(s => s.RodReelSetup)
                .WithMany(e => e.FishingSessions)
                .HasForeignKey(s => s.RodReelSetupId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<FishingSession>()
                .HasOne(s => s.PrimaryBaitLure)
                .WithMany(bl => bl.FishingSessions)
                .HasForeignKey(s => s.PrimaryBaitLureId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}