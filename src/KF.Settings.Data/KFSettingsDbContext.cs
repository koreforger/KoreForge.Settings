using KF.Settings.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KF.Settings.Data;

public class KFSettingsDbContext : DbContext
{
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();
    public DbSet<SettingsHistoryEntity> SettingsHistory => Set<SettingsHistoryEntity>();

    public KFSettingsDbContext(DbContextOptions<KFSettingsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Level 5: global (app NULL, inst NULL, ver NULL)
        modelBuilder.Entity<SettingEntity>()
            .HasIndex(x => new { x.Key })
            .HasDatabaseName("UX_Settings_Global_Key")
            .HasFilter("[ApplicationId] IS NULL AND [InstanceId] IS NULL AND [ClientAppVersion] IS NULL")
            .IsUnique();
        // Level 4: app-only (app NOT NULL, inst NULL, ver NULL)
        modelBuilder.Entity<SettingEntity>()
            .HasIndex(x => new { x.ApplicationId, x.Key })
            .HasDatabaseName("UX_Settings_App_Key")
            .HasFilter("[ApplicationId] IS NOT NULL AND [InstanceId] IS NULL AND [ClientAppVersion] IS NULL")
            .IsUnique();
        // Level 3: app+instance (app NOT NULL, inst NOT NULL, ver NULL)
        modelBuilder.Entity<SettingEntity>()
            .HasIndex(x => new { x.ApplicationId, x.InstanceId, x.Key })
            .HasDatabaseName("UX_Settings_Instance_Key")
            .HasFilter("[ApplicationId] IS NOT NULL AND [InstanceId] IS NOT NULL AND [ClientAppVersion] IS NULL")
            .IsUnique();
        // Level 2: app+clientversion (app NOT NULL, inst NULL, ver NOT NULL)
        modelBuilder.Entity<SettingEntity>()
            .HasIndex(x => new { x.ApplicationId, x.ClientAppVersion, x.Key })
            .HasDatabaseName("UX_Settings_AppVersion_Key")
            .HasFilter("[ApplicationId] IS NOT NULL AND [InstanceId] IS NULL AND [ClientAppVersion] IS NOT NULL")
            .IsUnique();
        // Level 1: app+instance+clientversion (app NOT NULL, inst NOT NULL, ver NOT NULL)
        modelBuilder.Entity<SettingEntity>()
            .HasIndex(x => new { x.ApplicationId, x.InstanceId, x.ClientAppVersion, x.Key })
            .HasDatabaseName("UX_Settings_InstanceVersion_Key")
            .HasFilter("[ApplicationId] IS NOT NULL AND [InstanceId] IS NOT NULL AND [ClientAppVersion] IS NOT NULL")
            .IsUnique();

        modelBuilder.Entity<SettingEntity>()
            .HasIndex(x => new { x.ApplicationId, x.InstanceId, x.ClientAppVersion, x.Key })
            .HasDatabaseName("IX_Settings_Scope_Key");

        modelBuilder.Entity<SettingsHistoryEntity>()
            .HasIndex(x => x.SettingId)
            .HasDatabaseName("IX_SettingsHistory_SettingId");

        modelBuilder.Entity<SettingsHistoryEntity>()
            .HasIndex(x => new { x.ApplicationId, x.InstanceId, x.ClientAppVersion, x.Key, x.ChangedDate })
            .HasDatabaseName("IX_SettingsHistory_KeyScopeDate");
    }
}
