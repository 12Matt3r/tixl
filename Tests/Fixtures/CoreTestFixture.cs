// CoreTestFixture.cs
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace TiXL.Tests.Fixtures
{
    public abstract class TiXLFacts : IAsyncLifetime, IDisposable
    {
        protected IServiceProvider? ServiceProvider { get; private set; }
        protected IHost? Host { get; private set; }
        protected ILogger Logger { get; private set; } = null!;
        
        public virtual async Task InitializeAsync()
        {
            Host = CreateHostBuilder().Build();
            ServiceProvider = Host.Services;
            Logger = ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType().Name);
            
            await SetupAsync();
        }
        
        public virtual async Task DisposeAsync()
        {
            await CleanupAsync();
            await Host?.DisposeAsync();
        }
        
        protected virtual IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices);
        }
        
        protected virtual void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Override in derived fixtures
        }
        
        protected virtual Task SetupAsync()
        {
            return Task.CompletedTask;
        }
        
        protected virtual Task CleanupAsync()
        {
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
    
    // Core test fixture with TiXL dependencies
    public class CoreTestFixture : TiXLFacts
    {
        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            
            // Register TiXL Core services
            services.AddLogging(builder => builder.AddConsole());
            
            // Add mock services for testing
            services.AddSingleton<ITestCleanupService, TestCleanupService>();
        }
        
        protected override Task SetupAsync()
        {
            Logger.LogInformation("CoreTestFixture initialized");
            return Task.CompletedTask;
        }
    }
}

public interface ITestCleanupService
{
    void RegisterForCleanup(IDisposable disposable);
    void Cleanup();
}

public class TestCleanupService : ITestCleanupService
{
    private readonly List<IDisposable> _disposables = new();
    
    public void RegisterForCleanup(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
    
    public void Cleanup()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
        _disposables.Clear();
    }
}