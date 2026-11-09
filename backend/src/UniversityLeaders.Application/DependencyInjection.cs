using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityLeaders.Application.Announcements;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Application.Auth;
using UniversityLeaders.Application.Faculties;
using UniversityLeaders.Application.Notifications;
using UniversityLeaders.Application.Reports;
using UniversityLeaders.Application.Users;

namespace UniversityLeaders.Application;

public static class DependencyInjection
{
    /// <summary>Registers application services (business logic). Features add their services here.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFacultyService, FacultyService>();
        services.AddScoped<IPublicDirectoryService, PublicDirectoryService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ICompletedWorkService, CompletedWorkService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        return services;
    }
}
