using Microsoft.Extensions.DependencyInjection;
using SharpClient.Core.Persistence;
using SharpClient.Core.Platform;
using SharpClient.Core.Presentation;
using SharpClient.Core.Sessions;

namespace SharpClient.UI;

/// <summary>
/// Shared DI registration for the presentation view models, referenced by BOTH the MAUI host
/// (<c>SharpClient.App</c>) and the Web host (<c>SharpClient.Web</c>). Keeping the registrations in
/// one place prevents host drift — previously each host registered the view models independently and
/// they diverged (e.g. <see cref="TriggerAliasEditorViewModel"/> was registered on Web but not MAUI,
/// crashing the Rules page on Android).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all seven presentation view models. The four session/settings/compose view models are always
    /// singletons; the three per-view view models use <paramref name="perViewLifetime"/> (Transient
    /// in MAUI, Scoped in Web). All constructor dependencies must already be registered by the host.
    /// </summary>
    public static IServiceCollection AddSharpClientViewModels(
        this IServiceCollection services,
        ServiceLifetime perViewLifetime)
    {
        services.AddSingleton<SessionsViewModel>(sp =>
            new SessionsViewModel(sp.GetRequiredService<ISessionManager>()));
        services.AddSingleton<ProtocolPanelViewModel>(sp =>
            new ProtocolPanelViewModel(sp.GetRequiredService<ISessionManager>()));
        services.AddSingleton<SettingsViewModel>(sp =>
            new SettingsViewModel(sp.GetRequiredService<IPreferences>()));
        services.AddSingleton<ComposeViewModel>(sp =>
            new ComposeViewModel(
                sp.GetRequiredService<ISessionManager>(),
                sp.GetRequiredService<IPreferences>()));

        services.Add(new ServiceDescriptor(
            typeof(WorldManagerViewModel),
            sp => new WorldManagerViewModel(
                sp.GetRequiredService<IWorldStore>(),
                sp.GetRequiredService<ISecretStore>(),
                sp.GetRequiredService<ISessionManager>(),
                sp.GetRequiredService<ISessionLauncher>()),
            perViewLifetime));
        services.Add(new ServiceDescriptor(
            typeof(HistorySearchViewModel),
            sp => new HistorySearchViewModel(
                sp.GetRequiredService<ISessionHistory>(),
                sp.GetRequiredService<IWorldStore>()),
            perViewLifetime));
        services.Add(new ServiceDescriptor(
            typeof(TriggerAliasEditorViewModel),
            sp => new TriggerAliasEditorViewModel(
                sp.GetRequiredService<IWorldStore>()),
            perViewLifetime));

        return services;
    }
}
